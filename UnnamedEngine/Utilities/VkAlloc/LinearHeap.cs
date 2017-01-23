using System;
using System.Collections.Generic;

using CSGL.Vulkan;

namespace UnnamedEngine.Utilities {
    public partial class VkAllocator {
        class LinearHeap {
            Device device;

            List<LinearPage> pages;
            object locker;
            List<MemoryType> memoryTypes;
            int numTypes;
            ulong pageSize;

            public LinearHeap(Device device, int heapIndex, VkPhysicalDeviceMemoryProperties props, ulong pageSize) {
                this.device = device;
                pages = new List<LinearPage>();
                locker = new object();
                numTypes = (int)props.memoryTypeCount;
                this.pageSize = pageSize;

                memoryTypes = new List<MemoryType>();

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
                    for (int j = 0; j < 32; i++) {
                        if ((memoryTypes[i].typeIndex & (1 << j)) != 0 && memoryTypes[i].flags == flags) {
                            typeIndex = memoryTypes[i].typeIndex;
                            return true;
                        }
                    }
                }

                return false;
            }

            public VkaAllocation Alloc(VkMemoryRequirements requirements, int typeIndex) {
                lock (locker) {
                    for (int i = 0; i < pages.Count; i++) {
                        if (pages[i].Match(typeIndex)) {
                            VkaAllocation result = pages[i].AttemptAlloc(requirements);
                            if (result.memory != null) {
                                return result;
                            }
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
