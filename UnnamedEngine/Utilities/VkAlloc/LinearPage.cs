﻿using System;
using System.Collections.Generic;

using CSGL.Vulkan;

namespace UnnamedEngine.Utilities {
    public partial class VkAllocator {
        class LinearPage {
            DeviceMemory memory;
            object locker;
            ulong size;
            ulong used;

            public LinearPage(Device device, ulong size, int typeIndex) {
                memory = new DeviceMemory(device, size, (uint)typeIndex);
                locker = new object();
                this.size = size;
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
                used = 0;
            }
        }
    }
}
