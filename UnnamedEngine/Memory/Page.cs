using System;
using System.Collections.Generic;

using CSGL.Vulkan;

namespace UnnamedEngine.Memory {
    public class Page : IDisposable {
        bool disposed;

        public DeviceMemory Memory { get; private set; }
        public bool Mapped { get; private set; }
        public IntPtr Mapping { get; private set; }
        public ulong Offset { get; private set; }
        public ulong Range { get; private set; }

        internal Page(DeviceMemory memory) {
            Memory = memory;
        }

        public IntPtr Map(ulong offset, ulong range) {
            Mapping = Memory.Map(offset, range);
            Mapped = true;
            Offset = offset;
            Range = range;
            return Mapping;
        }

        public void Unmap() {
            Memory.Unmap();
            Mapping = IntPtr.Zero;
            Mapped = false;
            Offset = 0;
            Range = 0;
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposing) {
            if (disposed) return;

            Memory.Dispose();
            if (Mapped) Unmap();

            disposed = true;
        }

        ~Page() {
            Dispose(false);
        }
    }
}
