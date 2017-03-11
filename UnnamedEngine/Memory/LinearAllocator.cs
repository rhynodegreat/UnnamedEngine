using System;
using System.Collections.Generic;

using CSGL.Vulkan;

namespace UnnamedEngine.Memory {
    public class LinearAllocator {
        List<Heap> heaps;
        object locker;
        bool persistentMap;

        public LinearAllocator(List<Memory.Heap> heaps, bool persistentMap) {
            this.heaps = new List<Heap>();
            HashSet<Memory.Heap> set = new HashSet<Memory.Heap>();
            locker = new object();
            this.persistentMap = persistentMap;

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

                        LinearPage page = new LinearPage(memory);
                        heaps[i].pages.Add(page);

                        if (persistentMap) memory.Map(0, memory.Memory.Size);  //map this page immediately

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

        class LinearPage {
            Page memory;
            ulong size;
            ulong used;
            object locker;

            public LinearPage(Page memory) {
                this.memory = memory;
                size = memory.Memory.Size;
                locker = new object();
            }

            public bool Match(int memoryTypeIndex) {
                return memory.Memory.MemoryTypeIndex == (uint)memoryTypeIndex;
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
                        return new Allocation(memory.Memory, start, requirements.size);
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
            public Memory.Heap heap;
            public List<LinearPage> pages;

            public Heap(Memory.Heap heap) {
                this.heap = heap;
                pages = new List<LinearPage>();
            }
        }
    }
}
