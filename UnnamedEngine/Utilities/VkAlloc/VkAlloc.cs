using System;
using System.Collections.Generic;
using System.Threading;

using CSGL;
using CSGL.Vulkan;

namespace UnnamedEngine.Utilities {
    public class VkAlloc : IDisposable {
        bool disposed;
        Device device;

        ulong pageSize;
        List<Heap> heaps;
        Dictionary<DeviceMemory, Page> pageMap;

        public VkAlloc(Device device, ulong pageSize) {
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

        ~VkAlloc() {
            Dispose(false);
        }

        public class Heap : IDisposable {
            List<Page> pages;
            object locker;
            Device device;
            List<VkMemoryPropertyFlags> heapFlags;
            HashSet<int> typeIndices;
            int numTypes;
            ulong pageSize;
            Dictionary<DeviceMemory, Page> pageMap;

            public Heap(Device device, int heapIndex, VkPhysicalDeviceMemoryProperties props, ulong pageSize, Dictionary<DeviceMemory, Page> pageMap) {
                this.device = device;
                pages = new List<Page>();
                locker = new object();
                numTypes = (int)props.memoryTypeCount;
                this.pageSize = pageSize;
                this.pageMap = pageMap;

                heapFlags = new List<VkMemoryPropertyFlags>();
                typeIndices = new HashSet<int>();

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

                for (int i = 0; i < numTypes; i++) {
                    if ((memoryBits & (1 << i)) != 0 && typeIndices.Contains(i)) {
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

        public class Page : IDisposable {
            DeviceMemory memory;
            Node head;
            object locker;
            ulong size;

            public Page(Device device, ulong size, int typeIndex, Dictionary<DeviceMemory, Page> pageMap) {
                memory = new DeviceMemory(device, size, (uint)typeIndex);
                pageMap.Add(memory, this);
                locker = new object();
                this.size = size;

                head = new Node(0, size);
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

        class Node {
            public Node next;
            public ulong offset;
            public ulong size;
            public bool free;

            public Node(ulong offset, ulong size) {
                this.offset = offset;
                this.size = size;
                free = true;
            }

            public void Split(ulong start, ulong size) {
                //split a node and mark the correct one as not free
                //this node can potentially be split into three if start and size defines a space in the middle of the node

                if (start == offset && this.size == size) {
                    //entire node was taken, so mark this a not free
                    free = false;
                } else if (start > offset) {
                    //some space was left in the beginning, so use this node for that and mark as new one a not free
                    ulong startSpace = start - offset;
                    this.size = startSpace;

                    Node middle = new Node(start, size);
                    middle.next = next;
                    next = middle;

                    //new node might need to be split
                    middle.Split(start, size);
                } else {
                    //only some space left at the back
                    free = false;
                    ulong endOffset = start + size;
                    ulong endSpace = (offset + this.size) - endOffset;

                    Node end = new Node(endOffset, endSpace);
                    end.next = next;
                    next = end;
                }
            }

            public void Merge() {
                if (free) {
                    Node next = this.next;
                    while (next != null && next.free) {
                        size += next.size;
                        this.next = next.next;
                        next = next.next;
                    }
                }
            }
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
