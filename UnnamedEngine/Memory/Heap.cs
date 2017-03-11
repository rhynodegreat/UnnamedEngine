using System;
using System.Collections.Generic;

using CSGL.Vulkan;

using UnnamedEngine.Core;

namespace UnnamedEngine.Memory {
    public class Heap : IDisposable {
        bool disposed;

        Device device;

        List<MemoryType> memoryTypes;
        List<Page> pages;

        public ulong TotalSize { get; private set; }
        public ulong PageSize { get; private set; }
        ulong allocated;
        object locker;

        public Heap(Device device, int heapIndex, VkPhysicalDeviceMemoryProperties props, ulong pageSize) {
            if (device == null) throw new ArgumentNullException(nameof(device));

            this.device = device;

            memoryTypes = new List<MemoryType>();
            pages = new List<Page>();
            locker = new object();

            TotalSize = props.GetMemoryHeaps(heapIndex).size;
            PageSize = pageSize;

            for (int i = 0; i < props.memoryTypeCount; i++) {
                var type = props.GetMemoryTypes(i);
                if (type.heapIndex == heapIndex) {
                    memoryTypes.Add(new MemoryType(i, type.propertyFlags));
                }
            }
        }

        public bool Match(uint memoryBits, VkMemoryPropertyFlags flags, out int typeIndex) {
            typeIndex = -1;

            for (int i = 0; i < memoryTypes.Count; i++) {
                if ((memoryBits & (1u << memoryTypes[i].typeIndex)) != 0 && (memoryTypes[i].flags & flags) == flags) {
                    typeIndex = memoryTypes[i].typeIndex;
                    return true;
                }
            }

            return false;
        }

        public Page Alloc(ulong size, uint memoryTypeIndex) {
            ulong allocSize = PageSize;
            while (allocSize < size) allocSize += PageSize; //ensure allocated size is multiple of PageSize

            lock (locker) {
                if (allocated + allocSize > TotalSize) return null;

                DeviceMemory memory = new DeviceMemory(device, allocSize, memoryTypeIndex);
                Page page = new Page(memory);
                pages.Add(page);
                allocated += allocSize;
                return page;
            }
        }

        public void Free(Page page) {
            pages.Remove(page);
            page.Dispose();
            allocated -= page.Memory.Size;
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposing) {
            if (disposed) return;

            foreach (var page in pages) page.Dispose();

            disposed = true;
        }

        ~Heap() {
            Dispose(false);
        }

        struct MemoryType {
            public int typeIndex;
            public VkMemoryPropertyFlags flags;

            public MemoryType(int typeIndex, VkMemoryPropertyFlags flags) {
                this.typeIndex = typeIndex;
                this.flags = flags;
            }
        }
    }
}
