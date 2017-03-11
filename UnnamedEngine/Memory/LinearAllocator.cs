using System;
using System.Collections.Generic;

using CSGL.Vulkan;

namespace UnnamedEngine.Memory {
    public class LinearAllocator {
        List<Heap> heaps;
        object locker;

        public LinearAllocator(List<MemoryHeap> heaps) {
            this.heaps = new List<Heap>();
            HashSet<MemoryHeap> set = new HashSet<MemoryHeap>();
            locker = new object();

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
                        DeviceMemory memory = heaps[i].heap.Alloc(requirements.size, (uint)typeIndex);
                        if (memory == null) continue;

                        Page page = new Page(memory);
                        heaps[i].pages.Add(page);
                        return page.Alloc(requirements);
                    }
                }
            }

            return new Allocation();
        }

        public void Reset() {
            for (int i = 0; i < heaps.Count; i++) {
                for (int j = 0; j < heaps[i].pages.Count; j++) {
                    heaps[i].pages[j].Reset();
                }
            }
        }

        class Page {
            DeviceMemory memory;
            ulong size;
            ulong used;
            object locker;

            public Page(DeviceMemory memory) {
                this.memory = memory;
                size = memory.Size;
                locker = new object();
            }

            public bool Match(int memoryTypeIndex) {
                return memory.MemoryTypeIndex == (uint)memoryTypeIndex;
            }

            public Allocation Alloc(VkMemoryRequirements requirements) {
                if (requirements.size > size) return new Allocation();

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
                        return new Allocation(memory, start, requirements.size);
                    }
                }

                return new Allocation();
            }

            public void Reset() {
                lock (locker) {
                    used = 0;
                }
            }
        }

        class Heap {
            public MemoryHeap heap;
            public List<Page> pages;

            public Heap(MemoryHeap heap) {
                this.heap = heap;
                pages = new List<Page>();
            }
        }
    }
}
