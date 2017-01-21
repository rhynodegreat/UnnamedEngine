using System;
using System.Collections.Generic;
using System.Threading;

using CSGL;
using CSGL.Vulkan;

namespace UnnamedEngine.Utilities {
    public partial class VkAllocator : IDisposable {
        bool disposed;
        Device device;

        ulong pageSize;
        List<Heap> heaps;
        Dictionary<DeviceMemory, Page> pageMap;

        public VkAllocator(Device device, ulong pageSize) {
            this.device = device;
            this.pageSize = pageSize;
            heaps = new List<Heap>();
            pageMap = new Dictionary<DeviceMemory, Page>();

            for (int i = 0; i < device.PhysicalDevice.MemoryProperties.memoryHeapCount; i++) {
                Heap heap = new Heap(device, i, device.PhysicalDevice.MemoryProperties, pageSize, pageMap);
                heaps.Add(heap);
            }
        }

        public VkaAllocation Alloc(VkMemoryRequirements requirements, VkMemoryPropertyFlags flags) {
            if (requirements.size == 0) return default(VkaAllocation);
            for (int i = 0; i < heaps.Count; i++) {
                int typeIndex;
                if (heaps[i].Match(requirements.memoryTypeBits, flags, out typeIndex)) {
                    VkaAllocation result = heaps[i].Alloc(requirements, typeIndex);
                    if (result.memory != null) {
                        return result;
                    }
                }
            }

            throw new VkAllocOutOfMemoryException("Could not allocate memory");
        }

        public void Free(VkaAllocation allocation) {
            if (!pageMap.ContainsKey(allocation.memory)) return;
            Page page = pageMap[allocation.memory];
            page.Free(allocation);
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposing) {
            if (disposed) return;

            for (int i = 0; i < heaps.Count; i++) {
                heaps[i].Dispose();
            }

            disposed = true;
        }

        ~VkAllocator() {
            Dispose(false);
        }
    }

    public struct VkaAllocation {
        public readonly DeviceMemory memory;
        public readonly ulong offset;
        public readonly ulong size;

        internal VkaAllocation(DeviceMemory memory, ulong offset, ulong size) {
            this.memory = memory;
            this.offset = offset;
            this.size = size;
        }
    }

    public class VkAllocOutOfMemoryException : Exception {
        public VkAllocOutOfMemoryException(string message) : base(message) { }
        public VkAllocOutOfMemoryException(string format, params object[] args) : base(string.Format(format, args)) { }
    }
}
