using System;
using System.Collections.Generic;

using CSGL.Vulkan;

namespace UnnamedEngine.Utilities {
    public partial class VkAllocator {
        class LinearHeap {
            Device device;

            List<LinearPage> pages;
            object locker;
            List<VkMemoryPropertyFlags> heapFlags;
            List<int> typeIndices;
            int numTypes;
            ulong pageSize;

            public LinearHeap(Device device, int heapIndex, VkPhysicalDeviceMemoryProperties props, ulong pageSize) {
                this.device = device;
                pages = new List<LinearPage>();
                locker = new object();
                numTypes = (int)props.memoryTypeCount;
                this.pageSize = pageSize;

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
                    LinearPage newPage = new LinearPage(device, size, typeIndex);
                    pages.Add(newPage);

                    return newPage.AttemptAlloc(requirements);
                }
            }

            public void Reset() {
                lock (locker) {
                    for (int i = 0; i < pages.Count; i++) {
                        pages[i].Reset();
                    }
                }
            }
        }
    }
}
