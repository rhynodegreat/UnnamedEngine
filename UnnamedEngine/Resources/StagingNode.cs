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
        Engine engine;
        VkAllocator allocator;

        CommandPool pool;
        CommandBuffer buffer;
        List<TransferOp> transfers;
        List<TransferOp> completed;
        List<CommandBuffer> submitBuffers;
        CommandBufferBeginInfo beginInfo;

        struct TransferOp {
            public Buffer staging;
            public Buffer dest;

            public TransferOp(Buffer staging, Buffer dest) {
                this.staging = staging;
                this.dest = dest;
            }
        }

        public StagingNode(Engine engine) : base(engine.Graphics.Device, VkPipelineStageFlags.TransferBit) {
            if (engine == null) throw new ArgumentNullException(nameof(engine));
            allocator = engine.Graphics.Allocator;

            this.engine = engine;

            CommandPoolCreateInfo info = new CommandPoolCreateInfo();
            info.queueFamilyIndex = engine.Graphics.GraphicsQueue.FamilyIndex;
            info.flags = VkCommandPoolCreateFlags.ResetCommandBufferBit;

            pool = new CommandPool(engine.Graphics.Device, info);
            buffer = pool.Allocate(VkCommandBufferLevel.Primary);

            transfers = new List<TransferOp>();
            completed = new List<TransferOp>();
            submitBuffers = new List<CommandBuffer> { buffer };

            beginInfo = new CommandBufferBeginInfo();
            beginInfo.flags = VkCommandBufferUsageFlags.SimultaneousUseBit | VkCommandBufferUsageFlags.OneTimeSubmitBit;
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
                    VkBufferCopy region = new VkBufferCopy();
                    region.srcOffset = 0;
                    region.dstOffset = 0;
                    region.size = transfers[i].dest.Size;

                    buffer.CopyBuffer(transfers[i].staging, transfers[i].dest);
                    completed.Add(transfers[i]);
                }
                transfers.Clear();
            }

            buffer.End();

            return submitBuffers;
        }

        public override void Transfer<T>(T[] data, Buffer buffer) {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));

            if ((buffer.Usage & VkBufferUsageFlags.TransferDstBit) == 0) throw new TransferException("Buffer.Usage must include TransferDstBit");
            if (!buffer.Bound) throw new TransferException("Buffer must be bound to a VkDeviceMemory object");

            var info = new BufferCreateInfo();
            info.size = (uint)Interop.SizeOf(data);
            info.usage = VkBufferUsageFlags.TransferSrcBit;
            info.sharingMode = VkSharingMode.Exclusive;

            Buffer staging = new Buffer(device, info);
            VkaAllocation alloc = allocator.AllocTemp(staging.Requirements, VkMemoryPropertyFlags.HostVisibleBit | VkMemoryPropertyFlags.HostCoherentBit);
            staging.Bind(alloc.memory, alloc.offset);

            IntPtr ptr = alloc.memory.Map(alloc.offset, alloc.size);
            Interop.Copy(data, ptr);
            alloc.memory.Unmap();

            lock (transfers) {
                transfers.Add(new TransferOp(staging, buffer));
            }
        }

        public override void Transfer<T>(List<T> data, Buffer buffer) {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));

            if ((buffer.Usage & VkBufferUsageFlags.TransferDstBit) == 0) throw new TransferException("Buffer.Usage must include TransferDstBit");
            if (!buffer.Bound) throw new TransferException("Buffer must be bound to a VkDeviceMemory object");

            var info = new BufferCreateInfo();
            info.size = (uint)Interop.SizeOf(data);
            info.usage = VkBufferUsageFlags.TransferSrcBit;
            info.sharingMode = VkSharingMode.Exclusive;

            Buffer staging = new Buffer(device, info);
            VkaAllocation alloc = allocator.AllocTemp(staging.Requirements, VkMemoryPropertyFlags.HostVisibleBit | VkMemoryPropertyFlags.HostCoherentBit);
            staging.Bind(alloc.memory, alloc.offset);

            IntPtr ptr = alloc.memory.Map(alloc.offset, alloc.size);
            Interop.Copy(data, ptr);
            alloc.memory.Unmap();

            lock (transfers) {
                transfers.Add(new TransferOp(staging, buffer));
            }
        }

        public override void Transfer(IntPtr data, uint size, Buffer buffer) {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));

            if ((buffer.Usage & VkBufferUsageFlags.TransferDstBit) == 0) throw new TransferException("Buffer.Usage must include TransferDstBit");
            if (!buffer.Bound) throw new TransferException("Buffer must be bound to a VkDeviceMemory object");

            var info = new BufferCreateInfo();
            info.size = size;
            info.usage = VkBufferUsageFlags.TransferSrcBit;
            info.sharingMode = VkSharingMode.Exclusive;

            Buffer staging = new Buffer(device, info);
            VkaAllocation alloc = allocator.AllocTemp(staging.Requirements, VkMemoryPropertyFlags.HostVisibleBit | VkMemoryPropertyFlags.HostCoherentBit);
            staging.Bind(alloc.memory, alloc.offset);

            IntPtr ptr = alloc.memory.Map(alloc.offset, alloc.size);
            Interop.Copy(data, ptr);
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
