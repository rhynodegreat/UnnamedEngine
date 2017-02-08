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
        List<TransferOp> transfers;
        List<TransferOp> completed;
        List<CommandBuffer> submitBuffers;
        CommandBufferBeginInfo beginInfo;

        Pool<BufferMemoryBarrier> bufferBarrierPool;
        List<BufferMemoryBarrier> bufferBarriers;

        struct TransferOp {
            public Buffer staging;
            public Buffer dest;

            public TransferOp(Buffer staging, Buffer dest) {
                this.staging = staging;
                this.dest = dest;
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

            transfers = new List<TransferOp>();
            completed = new List<TransferOp>();
            submitBuffers = new List<CommandBuffer> { buffer };

            beginInfo = new CommandBufferBeginInfo();
            beginInfo.flags = VkCommandBufferUsageFlags.SimultaneousUseBit | VkCommandBufferUsageFlags.OneTimeSubmitBit;

            bufferBarrierPool = new Pool<BufferMemoryBarrier>(() => new BufferMemoryBarrier() {
                size = ulong.MaxValue   //VK_WHOLE_SIZE
            });
            bufferBarriers = new List<BufferMemoryBarrier>();
        }

        public override void PreRender() {
            for (int i = 0; i < completed.Count; i++) {
                completed[i].staging.Dispose();
            }

            completed.Clear();
        }

        public override List<CommandBuffer> GetCommands() {
            buffer.Reset(VkCommandBufferResetFlags.None);

            buffer.Begin(beginInfo);

            lock (transfers) {
                for (int i = 0; i < transfers.Count; i++) {
                    buffer.CopyBuffer(transfers[i].staging, transfers[i].dest);
                    completed.Add(transfers[i]);

                    BufferMemoryBarrier barrier = bufferBarrierPool.Get();
                    barrier.buffer = transfers[i].dest;
                    barrier.dstQueueFamilyIndex = graphics.GraphicsQueue.FamilyIndex;
                    barrier.srcQueueFamilyIndex = graphics.TransferQueue.FamilyIndex;

                    bufferBarriers.Add(barrier);
                }
                transfers.Clear();
            }

            buffer.PipelineBarrier(VkPipelineStageFlags.TransferBit, VkPipelineStageFlags.TransferBit, VkDependencyFlags.None, null, bufferBarriers, null);

            buffer.End();

            return submitBuffers;
        }

        public override void PostRender() {
            for (int i = 0; i < bufferBarriers.Count; i++) {
                bufferBarrierPool.Free(bufferBarriers[i]);
            }

            bufferBarriers.Clear();
        }

        Buffer CreateStaging(uint size, Buffer dest, out VkaAllocation alloc) {
            if (dest == null) throw new ArgumentNullException(nameof(dest));
            if ((dest.Usage & VkBufferUsageFlags.TransferDstBit) == 0) throw new TransferException("Buffer.Usage must include TransferDstBit");

            var info = new BufferCreateInfo();
            info.size = size;
            info.usage = VkBufferUsageFlags.TransferSrcBit;
            info.sharingMode = VkSharingMode.Exclusive;

            Buffer staging = new Buffer(device, info);
            alloc = allocator.AllocTemp(staging.Requirements, VkMemoryPropertyFlags.HostVisibleBit | VkMemoryPropertyFlags.HostCoherentBit);
            staging.Bind(alloc.memory, alloc.offset);

            return staging;
        }

        public override void Transfer<T>(T[] data, Buffer buffer) {
            if (data == null) throw new ArgumentNullException(nameof(data));

            VkaAllocation alloc;
            Buffer staging = CreateStaging((uint)Interop.SizeOf(data), buffer, out alloc);

            IntPtr ptr = alloc.memory.Map(alloc.offset, alloc.size);
            Interop.Copy(data, ptr);
            alloc.memory.Unmap();

            lock (transfers) {
                transfers.Add(new TransferOp(staging, buffer));
            }
        }

        public override void Transfer<T>(List<T> data, Buffer buffer) {
            if (data == null) throw new ArgumentNullException(nameof(data));

            VkaAllocation alloc;
            Buffer staging = CreateStaging((uint)Interop.SizeOf(data), buffer, out alloc);

            IntPtr ptr = alloc.memory.Map(alloc.offset, alloc.size);
            Interop.Copy(data, ptr);
            alloc.memory.Unmap();

            lock (transfers) {
                transfers.Add(new TransferOp(staging, buffer));
            }
        }

        public override void Transfer(IntPtr data, uint size, Buffer buffer) {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));

            VkaAllocation alloc;
            Buffer staging = CreateStaging(size, buffer, out alloc);

            IntPtr ptr = alloc.memory.Map(alloc.offset, alloc.size);
            Interop.Copy(data, ptr, size);
            alloc.memory.Unmap();

            lock (transfers) {
                transfers.Add(new TransferOp(staging, buffer));
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
