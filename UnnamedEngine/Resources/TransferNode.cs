using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using CSGL;
using CSGL.Graphics;
using CSGL.Vulkan;
using Buffer = CSGL.Vulkan.Buffer;

using UnnamedEngine.Core;

namespace UnnamedEngine.Resources {
    public abstract class TransferNode : QueueNode {
        protected TransferNode(Device device, Queue queue, VkPipelineStageFlags flags) : base(device, queue, flags) {
            SignalStage = flags;
        }
        
        public abstract void Transfer(IntPtr data, ulong size, Buffer buffer);

        void Transfer<T>(T[] data, int count, Buffer buffer) where T : struct {
            GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            ulong size = (ulong)(Interop.SizeOf<T>() * count);
            Transfer(handle.AddrOfPinnedObject(), size, buffer);
            handle.Free();
        }

        public void Transfer<T>(T[] data, Buffer buffer) where T : struct {
            Transfer(data, data.Length, buffer);
        }

        public void Transfer<T>(List<T> data, Buffer buffer) where T : struct {
            Transfer(Interop.GetInternalArray(data), data.Count, buffer);
        }

        public abstract void Transfer(IntPtr data, uint width, uint height, ulong size, Image image, VkImageCopy region, VkImageLayout destLayout);

        public void Transfer<T>(Bitmap<T> bitmap, Image image, VkImageCopy region, VkImageLayout destLayout) where T : struct {
            if (bitmap == null) throw new ArgumentNullException(nameof(bitmap));

            uint width = (uint)bitmap.Width;
            uint height = (uint)bitmap.Height;
            ulong size = width * height * (uint)Interop.SizeOf<T>();

            GCHandle handle = GCHandle.Alloc(bitmap.Data, GCHandleType.Pinned);

            Transfer(handle.AddrOfPinnedObject(), width, height, size, image, region, destLayout);

            handle.Free();
        }
    }

    public class TransferException : Exception {
        public TransferException(string message) : base(message) { }
        public TransferException(string format, params object[] args) : base(string.Format(format, args)) { }
    }
}
