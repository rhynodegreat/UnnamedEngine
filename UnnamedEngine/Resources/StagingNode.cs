using System;
using System.Collections.Generic;

using CSGL;
using CSGL.Vulkan;
using Buffer = CSGL.Vulkan.Buffer;

using UnnamedEngine.Core;
using UnnamedEngine.Utilities;

namespace UnnamedEngine.Resources {
    public class StagingNode : TransferNode, IDisposable {
        bool disposed;
        Graphics graphics;
        VkAllocator allocator;

        CommandPool pool;
        CommandBuffer buffer;
        List<CommandBuffer> submitBuffers;
        CommandBufferBeginInfo beginInfo;

        List<BufferTransfer> bufferTransfers;
        List<BufferTransfer> bufferCompleted;

        Pool<BufferMemoryBarrier> bufferBarrierPool;
        List<BufferMemoryBarrier> bufferBarriers;

        List<ImageTransfer> imageTransfers;
        List<ImageTransfer> imageCompleted;

        Pool<ImageMemoryBarrier> imageBarriersPool;
        List<ImageMemoryBarrier> imageBarriers;

        struct BufferTransfer {
            public Buffer staging;
            public Buffer dest;

            public BufferTransfer(Buffer staging, Buffer dest) {
                this.staging = staging;
                this.dest = dest;
            }
        }

        struct ImageTransfer {
            public Image staging;
            public Image dest;
            public VkImageCopy region;
            public VkImageLayout destLayout;

            public ImageTransfer(Image staging, Image dest, VkImageCopy region, VkImageLayout destLayout) {
                this.staging = staging;
                this.dest = dest;
                this.region = region;
                this.destLayout = destLayout;
            }
        }

        public StagingNode(Graphics graphics) : base(graphics.Device, graphics.TransferQueue, VkPipelineStageFlags.TransferBit) {
            if (graphics == null) throw new ArgumentNullException(nameof(graphics));

            allocator = graphics.Allocator;
            this.graphics = graphics;

            CommandPoolCreateInfo info = new CommandPoolCreateInfo();
            info.queueFamilyIndex = graphics.TransferQueue.FamilyIndex;
            info.flags = VkCommandPoolCreateFlags.ResetCommandBufferBit;

            pool = new CommandPool(graphics.Device, info);
            buffer = pool.Allocate(VkCommandBufferLevel.Primary);
            submitBuffers = new List<CommandBuffer> { buffer };

            beginInfo = new CommandBufferBeginInfo();
            beginInfo.flags = VkCommandBufferUsageFlags.SimultaneousUseBit | VkCommandBufferUsageFlags.OneTimeSubmitBit;

            bufferTransfers = new List<BufferTransfer>();
            bufferCompleted = new List<BufferTransfer>();

            bufferBarrierPool = new Pool<BufferMemoryBarrier>(() => new BufferMemoryBarrier() {
                size = ulong.MaxValue   //VK_WHOLE_SIZE
            });
            bufferBarriers = new List<BufferMemoryBarrier>();

            imageTransfers = new List<ImageTransfer>();
            imageCompleted = new List<ImageTransfer>();

            imageBarriersPool = new Pool<ImageMemoryBarrier>(() => new ImageMemoryBarrier());
            imageBarriers = new List<ImageMemoryBarrier>();
        }

        public override void PreRender() {
            for (int i = 0; i < bufferCompleted.Count; i++) {
                bufferCompleted[i].staging.Dispose();
            }

            bufferCompleted.Clear();

            for (int i = 0; i < imageCompleted.Count; i++) {
                imageCompleted[i].staging.Dispose();
            }

            imageCompleted.Clear();
        }

        public override List<CommandBuffer> GetCommands() {
            buffer.Reset(VkCommandBufferResetFlags.None);

            buffer.Begin(beginInfo);

            lock (imageTransfers) {
                //first, all staging images must be transitioned to TransferSrcOptimal
                //and all dest images must be transitioned to TransferDstOptimal
                for (int i = 0; i < imageTransfers.Count; i++) {
                    ImageMemoryBarrier stagingBarrier = imageBarriersPool.Get();
                    stagingBarrier.srcAccessMask = VkAccessFlags.HostWriteBit;
                    stagingBarrier.dstAccessMask = VkAccessFlags.TransferReadBit;
                    stagingBarrier.oldLayout = VkImageLayout.Preinitialized;
                    stagingBarrier.newLayout = VkImageLayout.TransferSrcOptimal;
                    stagingBarrier.srcQueueFamilyIndex = uint.MaxValue; //VK_QUEUE_FAMILY_IGNORED
                    stagingBarrier.dstQueueFamilyIndex = uint.MaxValue;
                    stagingBarrier.image = imageTransfers[i].staging;
                    stagingBarrier.subresourceRange.aspectMask = VkImageAspectFlags.ColorBit;
                    stagingBarrier.subresourceRange.baseMipLevel = 0;
                    stagingBarrier.subresourceRange.levelCount = 1;
                    stagingBarrier.subresourceRange.baseArrayLayer = 0;
                    stagingBarrier.subresourceRange.layerCount = 1;

                    imageBarriers.Add(stagingBarrier);

                    Image dest = imageTransfers[i].dest;

                    ImageMemoryBarrier destBarrier = imageBarriersPool.Get();
                    destBarrier.srcAccessMask = VkAccessFlags.HostWriteBit;
                    destBarrier.dstAccessMask = VkAccessFlags.TransferWriteBit;
                    destBarrier.oldLayout = VkImageLayout.Undefined;
                    destBarrier.newLayout = VkImageLayout.TransferDstOptimal;
                    destBarrier.srcQueueFamilyIndex = uint.MaxValue;   //VK_QUEUE_FAMILY_IGNORED
                    destBarrier.dstQueueFamilyIndex = uint.MaxValue;
                    destBarrier.image = imageTransfers[i].dest;
                    destBarrier.subresourceRange.aspectMask = VkImageAspectFlags.ColorBit;
                    destBarrier.subresourceRange.baseMipLevel = 0;
                    destBarrier.subresourceRange.levelCount = dest.MipLevels;
                    destBarrier.subresourceRange.baseArrayLayer = 0;
                    destBarrier.subresourceRange.layerCount = dest.ArrayLayers;

                    imageBarriers.Add(destBarrier);
                }

                buffer.PipelineBarrier(VkPipelineStageFlags.TopOfPipeBit, VkPipelineStageFlags.TopOfPipeBit, VkDependencyFlags.None, null, null, imageBarriers);

                for (int i = 0; i < imageBarriers.Count; i++) {
                    imageBarriersPool.Free(imageBarriers[i]);
                }

                imageBarriers.Clear();

                //copy images
                for (int i = 0; i < imageTransfers.Count; i++) {
                    VkImageCopy region = imageTransfers[i].region;
                    region.srcSubresource.aspectMask = VkImageAspectFlags.ColorBit;
                    region.srcSubresource.mipLevel = 0;
                    region.srcSubresource.baseArrayLayer = 0;
                    region.srcSubresource.layerCount = 1;

                    ImageTransfer imageTransfer = imageTransfers[i];
                    buffer.CopyImage(
                        imageTransfer.staging, VkImageLayout.TransferSrcOptimal,
                        imageTransfer.dest, VkImageLayout.TransferDstOptimal,
                        region
                    );

                    imageCompleted.Add(imageTransfer);
                }

                //transition dest images to their dest layouts
                for (int i = 0; i < imageTransfers.Count; i++) {
                    Image image = imageTransfers[i].dest;

                    ImageMemoryBarrier barrier = imageBarriersPool.Get();
                    barrier.srcAccessMask = VkAccessFlags.TransferWriteBit;
                    barrier.dstAccessMask = VkAccessFlags.ShaderReadBit;
                    barrier.oldLayout = VkImageLayout.TransferDstOptimal;
                    barrier.newLayout = imageTransfers[i].destLayout;
                    barrier.srcQueueFamilyIndex = graphics.TransferQueue.FamilyIndex;
                    barrier.dstQueueFamilyIndex = graphics.GraphicsQueue.FamilyIndex;
                    barrier.image = imageTransfers[i].dest;
                    barrier.subresourceRange.aspectMask = VkImageAspectFlags.ColorBit;
                    barrier.subresourceRange.baseMipLevel = 0;
                    barrier.subresourceRange.levelCount = image.MipLevels;
                    barrier.subresourceRange.baseArrayLayer = 0;
                    barrier.subresourceRange.layerCount = image.ArrayLayers;

                    imageBarriers.Add(barrier); //barrier submitted below
                }
            }
            imageTransfers.Clear();

            lock (bufferTransfers) {
                for (int i = 0; i < bufferTransfers.Count; i++) {
                    buffer.CopyBuffer(bufferTransfers[i].staging, bufferTransfers[i].dest);
                    bufferCompleted.Add(bufferTransfers[i]);

                    BufferMemoryBarrier barrier = bufferBarrierPool.Get();
                    barrier.buffer = bufferTransfers[i].dest;
                    barrier.dstQueueFamilyIndex = graphics.GraphicsQueue.FamilyIndex;
                    barrier.srcQueueFamilyIndex = graphics.TransferQueue.FamilyIndex;

                    bufferBarriers.Add(barrier);
                }
                bufferTransfers.Clear();
            }

            buffer.PipelineBarrier(VkPipelineStageFlags.TransferBit, VkPipelineStageFlags.TransferBit, VkDependencyFlags.None, null, bufferBarriers, imageBarriers);

            buffer.End();

            return submitBuffers;
        }

        public override void PostRender() {
            for (int i = 0; i < bufferBarriers.Count; i++) {
                bufferBarrierPool.Free(bufferBarriers[i]);
            }

            for (int i = 0; i < imageBarriers.Count; i++) {
                imageBarriersPool.Free(imageBarriers[i]);
            }

            bufferBarriers.Clear();
            imageBarriers.Clear();
        }

        Buffer CreateStaging(ulong size, Buffer dest, out VkaAllocation alloc) {
            var info = new BufferCreateInfo();
            info.size = size;
            info.usage = VkBufferUsageFlags.TransferSrcBit;
            info.sharingMode = VkSharingMode.Exclusive;

            Buffer staging = new Buffer(device, info);
            alloc = allocator.AllocTemp(staging.Requirements, VkMemoryPropertyFlags.HostVisibleBit | VkMemoryPropertyFlags.HostCoherentBit);
            staging.Bind(alloc.memory, alloc.offset);

            return staging;
        }

        public override void Transfer(IntPtr data, ulong size, Buffer buffer) {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if ((buffer.Usage & VkBufferUsageFlags.TransferDstBit) == 0) throw new TransferException("Buffer.Usage must include TransferDstBit");

            VkaAllocation alloc;
            Buffer staging = CreateStaging(size, buffer, out alloc);

            IntPtr ptr = alloc.memory.Map(alloc.offset, alloc.size);
            Interop.Copy(data, ptr, (long)size);
            alloc.memory.Unmap();

            lock (bufferTransfers) {
                bufferTransfers.Add(new BufferTransfer(staging, buffer));
            }
        }

        Image CreateStaging(ulong size, uint width, uint height, Image dest, out VkaAllocation alloc) {

            var info = new ImageCreateInfo();
            info.imageType = dest.ImageType;
            info.format = dest.Format;
            info.extent.width = width;
            info.extent.height = height;
            info.extent.depth = 1;
            info.mipLevels = 1;
            info.arrayLayers = 1;
            info.samples = dest.Samples;
            info.tiling = VkImageTiling.Linear;
            info.usage = VkImageUsageFlags.TransferSrcBit;
            info.sharingMode = VkSharingMode.Exclusive;
            info.initialLayout = VkImageLayout.Preinitialized;

            Image staging = new Image(device, info);
            alloc = allocator.AllocTemp(staging.MemoryRequirements, VkMemoryPropertyFlags.HostVisibleBit | VkMemoryPropertyFlags.HostCoherentBit);
            staging.Bind(alloc.memory, alloc.offset);

            return staging;
        }

        public override void Transfer(IntPtr data, uint width, uint height, ulong size, Image image, VkImageCopy region, VkImageLayout destLayout) {
            if (image == null) throw new ArgumentNullException(nameof(image));
            if ((image.Usage & VkImageUsageFlags.TransferDstBit) == 0) throw new TransferException("Image.Usage must include TransferDstBit");

            VkaAllocation alloc;
            Image staging = CreateStaging(size, width, height, image, out alloc);

            IntPtr ptr = alloc.memory.Map(alloc.offset, alloc.size);
            Interop.Copy(data, ptr, (long)size);
            alloc.memory.Unmap();

            lock (imageTransfers) {
                imageTransfers.Add(new ImageTransfer(staging, image, region, destLayout));
            }
        }

        public new void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected override void Dispose(bool disposing) {
            if (disposed) return;
            base.Dispose(disposing);

            pool.Dispose();

            disposed = true;
        }

        ~StagingNode() {
            Dispose(false);
        }
    }
}
