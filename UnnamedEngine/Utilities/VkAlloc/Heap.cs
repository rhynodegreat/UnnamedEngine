using System;
using System.Collections.Generic;

using CSGL.Vulkan;

namespace UnnamedEngine.Utilities {
    public partial class VkAllocator {
        public class Heap : IDisposable {
            List<Page> pages;
            object locker;
            Device device;
            List<VkMemoryPropertyFlags> heapFlags;
            List<int> typeIndices;
            ulong pageSize;
            Dictionary<DeviceMemory, Page> pageMap;

            public Heap(Device device, int heapIndex, VkPhysicalDeviceMemoryProperties props, ulong pageSize, Dictionary<DeviceMemory, Page> pageMap) {
                this.device = device;
                pages = new List<Page>();
                locker = new object();
                this.pageSize = pageSize;
                this.pageMap = pageMap;

                heapFlags = new List<VkMemoryPropertyFlags>();
                typeIndices = new List<int>();

                for (int i = 0; i < props.memoryTypeCount; i++) {
                    var type = props.GetMemoryTypes(i);
                    if (type.heapIndex == heapIndex) {
                        typeIndices.Add(i);
                        heapFlags.Add(type.propertyFlags);
                    }
                }
            }

            public bool Match(uint memoryBits, VkMemoryPropertyFlags flags, out int typeIndex) {
                bool typeMatch = false;
                typeIndex = -1;

                for (int i = 0; i < typeIndices.Count; i++) {
                    if ((memoryBits & (1 << typeIndices[i])) != 0) {
                        typeMatch = true;
                        typeIndex = i;
                        break;
                    }
                }

                bool flagMatch = false;

                for (int i = 0; i < heapFlags.Count; i++) {
                    if ((heapFlags[i] & flags) == flags) {
                        flagMatch = true;
                        break;
                    }
                }

                return typeMatch && flagMatch;
            }

            public VkaAllocation Alloc(VkMemoryRequirements requirements, int typeIndex) {
                lock (locker) {
                    for (int i = 0; i < pages.Count; i++) {
                        VkaAllocation result = pages[i].AttemptAlloc(requirements);
                        if (result.memory != null) {
                            return result;
                        }
                    }

                    ulong size = Math.Max(requirements.size, pageSize);
                    Page newPage = new Page(device, size, typeIndex, pageMap);
                    pages.Add(newPage);

                    return newPage.AttemptAlloc(requirements);
                }
            }

            public void Dispose() {
                for (int i = 0; i < pages.Count; i++) {
                    pages[i].Dispose();
                }
            }
        }
    }
}
