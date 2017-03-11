using System;
using System.Collections.Generic;

using CSGL.Vulkan;

namespace UnnamedEngine.Utilities {
    public struct Allocation {
        public DeviceMemory memory;
        public ulong offset;
        public ulong size;

        public Allocation(DeviceMemory memory, ulong offset, ulong size) {
            this.memory = memory;
            this.offset = offset;
            this.size = size;
        }
    }
}
