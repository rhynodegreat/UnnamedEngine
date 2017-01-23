using System;
using System.Collections.Generic;

using CSGL.Vulkan;

namespace UnnamedEngine.Utilities {
    public partial class VkAllocator {
        class LinearPage : IDisposable {
            DeviceMemory memory;
            object locker;
            ulong size;
            ulong used;
            int typeIndex;

            public LinearPage(Device device, ulong size, int typeIndex) {
                memory = new DeviceMemory(device, size, (uint)typeIndex);
                locker = new object();
                this.size = size;
                this.typeIndex = typeIndex;
            }

            public bool Match(int typeIndex) {
                return this.typeIndex == typeIndex;
            }

            public VkaAllocation AttemptAlloc(VkMemoryRequirements requirements) {
                lock (locker) {
                    ulong start = used;
                    ulong available = size - used;

                    ulong unalign = start % requirements.alignment;
                    ulong align;

                    if (unalign == 0) {
                        align = 0;
                    } else {
                        align = requirements.alignment - unalign;
                    }

                    start += align;
                    available -= align;

                    if (available >= requirements.size) {
                        used = start + requirements.size;
                        return new VkaAllocation(memory, start, requirements.size);
                    }
                }

                return default(VkaAllocation);
            }

            public void Reset() {
                lock (locker) {
                    used = 0;
                }
            }

            public void Dispose() {
                memory.Dispose();
            }
        }
    }
}
