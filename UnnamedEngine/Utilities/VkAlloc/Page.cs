using System;
using System.Collections.Generic;

using CSGL.Vulkan;

namespace UnnamedEngine.Utilities {
    public partial class VkAllocator {
        public class Page : IDisposable {
            DeviceMemory memory;
            Node head;
            object locker;
            ulong size;
            int typeIndex;

            public ulong Size {
                get {
                    return size;
                }
            }

            public Page(Device device, ulong size, int typeIndex, Dictionary<DeviceMemory, Page> pageMap) {
                memory = new DeviceMemory(device, size, (uint)typeIndex);
                pageMap.Add(memory, this);
                locker = new object();
                this.size = size;
                this.typeIndex = typeIndex;

                head = new Node(0, size);
            }

            public bool Match(int typeIndex) {
                return this.typeIndex == typeIndex;
            }

            public VkaAllocation AttemptAlloc(VkMemoryRequirements requirements) {
                if (requirements.size > size) return default(VkaAllocation);
                lock (locker) {
                    Node current = head;
                    while (current != null) {
                        if (current.free && current.size >= requirements.size) {
                            ulong start = current.offset;
                            ulong available = current.size;

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
                                current.Split(start, requirements.size);
                                VkaAllocation result = new VkaAllocation(memory, start, requirements.size);
                                return result;
                            }
                        }

                        current = current.next;
                    }

                    return default(VkaAllocation);
                }
            }

            public void Free(VkaAllocation allocation) {
                lock (locker) {
                    Node current = head;

                    while (current != null) {
                        if (current.free && current.offset == allocation.offset && current.size == allocation.size) {
                            current.free = true;
                            break;
                        }

                        current = current.next;
                    }

                    current = head;

                    while (current != null) {
                        current.Merge();
                        current = current.next;
                    }
                }
            }

            public void Dispose() {
                memory.Dispose();
            }
        }
    }
}
