using System;
using System.Collections.Generic;

using CSGL;
using CSGL.Vulkan;
using Buffer = CSGL.Vulkan.Buffer;

using UnnamedEngine.Core;
using UnnamedEngine.Utilities;
using UnnamedEngine.Memory;

namespace UnnamedEngine.Resources {
    public class StagingNode : TransferNode, IDisposable {
        bool disposed;
        Engine engine;

        CommandPool pool;
        CommandBuffer buffer;
        List<CommandBuffer> submitBuffers;
        CommandBufferBeginInfo beginInfo;

        List<BufferTransfer> bufferTransfers;
        Pool<BufferMemoryBarrier> bufferBarrierPool;
        List<BufferMemoryBarrier> bufferBarriers;

        List<ImageTransfer> imageTransfers;
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

        public StagingNode(Engine engine) : base(engine.Graphics.Device, engine.Graphics.GraphicsQueue, VkPipelineStageFlags.TransferBit) {
            if (engine == null) throw new ArgumentNullException(nameof(engine));

            this.engine = engine;

            CommandPoolCreateInfo info = new CommandPoolCreateInfo();
            info.queueFamilyIndex = engine.Graphics.GraphicsQueue.FamilyIndex;
            info.flags = VkCommandPoolCreateFlags.ResetCommandBufferBit;

            pool = new CommandPool(engine.Graphics.Device, info);
            buffer = pool.Allocate(VkCommandBufferLevel.Primary);
            submitBuffers = new List<CommandBuffer> { buffer };

            beginInfo = new CommandBufferBeginInfo();
            beginInfo.flags = VkCommandBufferUsageFlags.SimultaneousUseBit | VkCommandBufferUsageFlags.OneTimeSubmitBit;

            bufferTransfers = new List<BufferTransfer>();

            bufferBarrierPool = new Pool<BufferMemoryBarrier>(() => new BufferMemoryBarrier() {
                size = ulong.MaxValue   //VK_WHOLE_SIZE
            });
            bufferBarriers = new List<BufferMemoryBarrier>();

            imageTransfers = new List<ImageTransfer>();

            imageBarriersPool = new Pool<ImageMemoryBarrier>(() => new ImageMemoryBarrier());
            imageBarriers = new List<ImageMemoryBarrier>();
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
                    stagingBarrier.srcQueueFamilyIndex = uint.MaxValue;
                    stagingBarrier.dstQueueFamilyIndex = uint.MaxValue;
                    stagingBarrier.image = imageTransfers[i].staging;
                    stagingBarrier.subresourceRange.aspectMask = VkImageAspectFlags.ColorBit;
                    stagingBarrier.subresourceRange.baseMipLevel = 0;
                    stagingBarrier.subresourceRange.levelCount = 1;
                    stagingBarrier.subresourceRange.baseArrayLayer = 0;
                    stagingBarrier.subresourceRange.layerCount = 1;

                    imageBarriers.Add(stagingBarrier);

                    ImageTransfer transfer = imageTransfers[i];

                    ImageMemoryBarrier destBarrier = imageBarriersPool.Get();
                    destBarrier.srcAccessMask = VkAccessFlags.None;
                    destBarrier.dstAccessMask = VkAccessFlags.TransferWriteBit;
                    destBarrier.oldLayout = VkImageLayout.Undefined;
                    destBarrier.newLayout = VkImageLayout.TransferDstOptimal;
                    destBarrier.srcQueueFamilyIndex = uint.MaxValue;
                    destBarrier.dstQueueFamilyIndex = uint.MaxValue;
                    destBarrier.image = transfer.dest;
                    destBarrier.subresourceRange.aspectMask = VkImageAspectFlags.ColorBit;
                    destBarrier.subresourceRange.baseMipLevel = transfer.region.dstSubresource.mipLevel;
                    destBarrier.subresourceRange.levelCount = 1;
                    destBarrier.subresourceRange.baseArrayLayer = transfer.region.dstSubresource.baseArrayLayer;
                    destBarrier.subresourceRange.layerCount = transfer.region.dstSubresource.layerCount;

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
                }

                //transition dest images to their dest layouts
                for (int i = 0; i < imageTransfers.Count; i++) {
                    ImageTransfer transfer = imageTransfers[i];

                    ImageMemoryBarrier barrier = imageBarriersPool.Get();
                    barrier.srcAccessMask = VkAccessFlags.TransferWriteBit;
                    barrier.dstAccessMask = VkAccessFlags.ShaderReadBit;
                    barrier.oldLayout = VkImageLayout.TransferDstOptimal;
                    barrier.newLayout = imageTransfers[i].destLayout;
                    barrier.srcQueueFamilyIndex = uint.MaxValue;
                    barrier.dstQueueFamilyIndex = uint.MaxValue;
                    barrier.image = transfer.dest;
                    barrier.subresourceRange.aspectMask = VkImageAspectFlags.ColorBit;
                    barrier.subresourceRange.baseMipLevel = transfer.region.dstSubresource.mipLevel;
                    barrier.subresourceRange.levelCount = 1;
                    barrier.subresourceRange.baseArrayLayer = transfer.region.dstSubresource.baseArrayLayer;
                    barrier.subresourceRange.layerCount = transfer.region.dstSubresource.layerCount;

                    imageBarriers.Add(barrier); //barrier submitted below
                }
                imageTransfers.Clear();
            }

            lock (bufferTransfers) {
                for (int i = 0; i < bufferTransfers.Count; i++) {
                    buffer.CopyBuffer(bufferTransfers[i].staging, bufferTransfers[i].dest);

                    BufferMemoryBarrier barrier = bufferBarrierPool.Get();
                    barrier.buffer = bufferTransfers[i].dest;
                    barrier.dstQueueFamilyIndex = uint.MaxValue;
                    barrier.srcQueueFamilyIndex = uint.MaxValue;

                    bufferBarriers.Add(barrier);
                }
                bufferTransfers.Clear();
            }

            buffer.PipelineBarrier(VkPipelineStageFlags.TransferBit, VkPipelineStageFlags.BottomOfPipeBit, VkDependencyFlags.None, null, bufferBarriers, imageBarriers);

            buffer.End();

            return submitBuffers;
        }

        public override void PostSubmit() {
            for (int i = 0; i < bufferBarriers.Count; i++) {
                bufferBarrierPool.Free(bufferBarriers[i]);
            }

            for (int i = 0; i < imageBarriers.Count; i++) {
                imageBarriersPool.Free(imageBarriers[i]);
            }

            bufferBarriers.Clear();
            imageBarriers.Clear();
        }

        Buffer CreateStaging(ulong size, Buffer dest) {
            var info = new BufferCreateInfo();
            info.size = size;
            info.usage = VkBufferUsageFlags.TransferSrcBit;
            info.sharingMode = VkSharingMode.Exclusive;

            Buffer staging = engine.Memory.AllocStaging(info);

            return staging;
        }

        public override void Transfer(IntPtr data, ulong size, Buffer buffer) {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if ((buffer.Usage & VkBufferUsageFlags.TransferDstBit) == 0) throw new TransferException("Buffer.Usage must include TransferDstBit");
            
            Buffer staging = CreateStaging(size, buffer);

            Page page = engine.Memory.GetStagingPage(staging.Memory);
            IntPtr ptr = page.Mapping + (int)staging.Offset;
            Interop.Copy(data, ptr, (long)size);

            lock (bufferTransfers) {
                bufferTransfers.Add(new BufferTransfer(staging, buffer));
            }
        }

        Image CreateStaging(ulong size, uint width, uint height, Image dest) {
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

            Image staging = engine.Memory.AllocStaging(info);

            return staging;
        }

        public override void Transfer(IntPtr data, uint width, uint height, ulong size, Image image, VkImageCopy region, VkImageLayout destLayout) {
            if (image == null) throw new ArgumentNullException(nameof(image));
            if ((image.Usage & VkImageUsageFlags.TransferDstBit) == 0) throw new TransferException("Image.Usage must include TransferDstBit");
            
            Image staging = CreateStaging(size, width, height, image);

            Page page = engine.Memory.GetStagingPage(staging.Memory);
            IntPtr ptr = page.Mapping + (int)staging.Offset;
            Interop.Copy(data, ptr, (long)size);

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
