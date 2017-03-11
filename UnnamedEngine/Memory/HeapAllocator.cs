using System;
using System.Collections.Generic;

using CSGL.Vulkan;

namespace UnnamedEngine.Memory {
    public class HeapAllocator {
        List<Heap> heaps;
        object locker;
        Dictionary<DeviceMemory, HeapPage> pageMap;

        public HeapAllocator(List<Memory.Heap> heaps) {
            this.heaps = new List<Heap>();
            HashSet<Memory.Heap> set = new HashSet<Memory.Heap>();
            locker = new object();
            pageMap = new Dictionary<DeviceMemory, HeapPage>();

            foreach (var heap in heaps) {
                if (!set.Contains(heap)) {
                    set.Add(heap);
                    this.heaps.Add(new Heap(heap));
                }
            }
        }

        public Allocation Alloc(VkMemoryRequirements requirements, VkMemoryPropertyFlags flags) {
            //check existing pages
            for (int i = 0; i < heaps.Count; i++) {
                var heap = heaps[i];
                int typeIndex;
                if (!heap.heap.Match(requirements.memoryTypeBits, flags, out typeIndex)) continue;

                for (int j = 0; j < heap.pages.Count; j++) {
                    var page = heap.pages[j];
                    if (!page.Match(typeIndex)) continue;

                    Allocation result = page.Alloc(requirements);
                    if (result.memory != null) return result;
                }
            }

            //go through all heaps in priority order and try to allocate a new page
            lock (locker) {
                for (int i = 0; i < heaps.Count; i++) {
                    int typeIndex;
                    if (heaps[i].heap.Match(requirements.memoryTypeBits, flags, out typeIndex)) {
                        Page memory = heaps[i].heap.Alloc(requirements.size, (uint)typeIndex);
                        if (memory == null) continue;

                        HeapPage page = new HeapPage(memory);
                        heaps[i].pages.Add(page);
                        pageMap.Add(memory.Memory, page);
                        return page.Alloc(requirements);
                    }
                }
            }

            return new Allocation();
        }

        public void Free(Allocation allocation) {
            if (allocation.memory == null) return;

            if (pageMap.ContainsKey(allocation.memory)) {
                pageMap[allocation.memory].Free(allocation);
            }
        }

        class HeapPage {
            public Page memory;
            Node head;
            ulong size;
            object locker;

            public HeapPage(Page memory) {
                this.memory = memory;
                head = new Node(0, memory.Memory.Size);
                size = memory.Memory.Size;
                locker = new object();
            }

            public bool Match(int memoryTypeIndex) {
                return memory.Memory.MemoryTypeIndex == (uint)memoryTypeIndex;
            }
            
            public Allocation Alloc(VkMemoryRequirements requirements) {
                if (requirements.size > size) return new Allocation();
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
                                Allocation result = new Allocation(memory.Memory, start, requirements.size);
                                return result;
                            }
                        }

                        current = current.next;
                    }

                    return new Allocation();
                }
            }

            public void Free(Allocation allocation) {
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
        }

        class Heap {
            public Memory.Heap heap;
            public List<HeapPage> pages;

            public Heap(Memory.Heap heap) {
                this.heap = heap;
                pages = new List<HeapPage>();
            }
        }
    }
}
