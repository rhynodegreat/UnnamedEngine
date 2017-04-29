using System;
using System.Collections.Generic;

using CSGL.Vulkan;
using Buffer = CSGL.Vulkan.Buffer;

using UnnamedEngine.Resources;
using UnnamedEngine.Memory;

namespace UnnamedEngine.Core {
    public class Memory : IDisposable {
        Engine engine;

        Heap deviceHeap;    //GPU memory
        Heap fastHostHeap;    //GPU memory that's writable by the CPU
        Heap hostHeap;    //CPU memory
        Heap hostReadHeap;    //cached CPU memory that's writable by the GPU

        public TransferNode TransferNode { get; private set; }

        HeapAllocator deviceAllocator;
        LinearAllocator stagingAllocator;
        HeapAllocator uniformAllocator;
        HeapAllocator hostReadAllocator;
        
        List<IDisposable> stagingBuffers;

        const int devicePageSize = 256 * 1024 * 1024;
        const int fastHostPageSize = 32 * 1024 * 1024;
        const int hostPageSize = 256 * 1024 * 1024;
        const int hostReadPageSize = 256 * 1024 * 1024;

        internal Memory(Engine engine) {
            this.engine = engine;
            
            stagingBuffers = new List<IDisposable>();

            TransferNode = new StagingNode(engine);

            FindHeaps();
        }

        public Buffer AllocDevice(BufferCreateInfo info) {
            return Alloc(info, deviceAllocator, VkMemoryPropertyFlags.None);    //None because the allocator handles the priority
        }

        public void FreeDevice(Buffer buffer) {
            Free(buffer, deviceAllocator);
        }

        public Image AllocDevice(ImageCreateInfo info) {
            return Alloc(info, deviceAllocator, VkMemoryPropertyFlags.None);    //None
        }

        public void FreeDevice(Image image) {
            Free(image, deviceAllocator);
        }

        public Buffer AllocStaging(BufferCreateInfo info) {
            Buffer buffer = AllocStagingInternal(info);

            lock (stagingBuffers) {
                stagingBuffers.Add(buffer);
            }

            return buffer;
        }

        public Image AllocStaging(ImageCreateInfo info) {
            Image image = AllocStagingInternal(info);

            lock (stagingBuffers) {
                stagingBuffers.Add(image);
            }

            return image;
        }

        public void ResetStaging() {
            lock (stagingBuffers) {
                for (int i = 0; i < stagingBuffers.Count; i++) {
                    stagingBuffers[i].Dispose();
                }

                stagingBuffers.Clear();
            }

            stagingAllocator.Reset();
        }

        public Buffer AllocUniform(BufferCreateInfo info) {
            return Alloc(info, uniformAllocator, VkMemoryPropertyFlags.HostVisibleBit | VkMemoryPropertyFlags.HostCoherentBit);
        }

        public void FreeUniform(Buffer buffer) {
            Free(buffer, uniformAllocator);
        }

        Buffer Alloc(BufferCreateInfo info, HeapAllocator allocator, VkMemoryPropertyFlags flags) {
            Buffer buffer = new Buffer(engine.Graphics.Device, info);
            Allocation alloc = allocator.Alloc(buffer.Requirements, flags);
            buffer.Bind(alloc.memory, alloc.offset);

            return buffer;
        }

        void Free(Buffer buffer, HeapAllocator allocator) {
            if (buffer == null) return;
            Allocation alloc = new Allocation(buffer.Memory, buffer.Offset, buffer.Size);
            allocator.Free(alloc);

            buffer.Dispose();
        }

        Image Alloc(ImageCreateInfo info, HeapAllocator allocator, VkMemoryPropertyFlags flags) {
            Image image = new Image(engine.Graphics.Device, info);
            Allocation alloc = allocator.Alloc(image.Requirements, flags);
            image.Bind(alloc.memory, alloc.offset);

            return image;
        }

        void Free(Image image, HeapAllocator allocator) {
            if (image == null) return;
            Allocation alloc = new Allocation(image.Memory, image.Offset, image.Size);
            allocator.Free(alloc);

            image.Dispose();
        }

        Buffer AllocStagingInternal(BufferCreateInfo info) {
            Buffer buffer = new Buffer(engine.Graphics.Device, info);
            Allocation alloc = stagingAllocator.Alloc(buffer.Requirements, VkMemoryPropertyFlags.HostVisibleBit | VkMemoryPropertyFlags.HostCoherentBit);
            buffer.Bind(alloc.memory, alloc.offset);

            return buffer;
        }

        Image AllocStagingInternal(ImageCreateInfo info) {
            Image image = new Image(engine.Graphics.Device, info);
            Allocation alloc = stagingAllocator.Alloc(image.Requirements, VkMemoryPropertyFlags.HostVisibleBit | VkMemoryPropertyFlags.HostCoherentBit);
            image.Bind(alloc.memory, alloc.offset);

            return image;
        }

        public Page GetUniformPage(DeviceMemory memory) {
            return uniformAllocator.GetPage(memory);
        }

        public Page GetStagingPage(DeviceMemory memory) {
            return stagingAllocator.GetPage(memory);
        }

        void FindHeaps() {
            PhysicalDevice physicalDevice = engine.Graphics.PhysicalDevice;
            VkPhysicalDeviceMemoryProperties props = physicalDevice.MemoryProperties;
            
            FindDevice(props);
            FindFastHost(props);
            FindHostRead(props);
            FindHost(props);

            deviceAllocator = new HeapAllocator(new List<Heap> {
                deviceHeap,
                fastHostHeap,
                hostHeap,
                hostReadHeap
            }, false);

            stagingAllocator = new LinearAllocator(new List<Heap> {
                fastHostHeap,
                hostHeap,
                hostReadHeap
            }, true);

            uniformAllocator = new HeapAllocator(new List<Heap> {
                fastHostHeap,
                hostHeap,
                hostReadHeap
            }, true);

            hostReadAllocator = new HeapAllocator(new List<Heap> {
                hostReadHeap,
                hostHeap
            }, true);
        }

        void FindDevice(VkPhysicalDeviceMemoryProperties props) {
            int candidate = -1;

            //try to find largest device local heap that has only DeviceLocalBit
            //this avoids selecting main RAM as device heap when VRAM heap is available

            MatchStrict(props, VkMemoryPropertyFlags.DeviceLocalBit, out candidate);

            if (candidate != -1) {
                deviceHeap = new Heap(engine.Graphics.Device, candidate, props, devicePageSize);
                return;
            }

            //select whichever heap is largest
            Match(props, VkMemoryPropertyFlags.DeviceLocalBit, out candidate);
            
            if (candidate != -1) {
                deviceHeap = new Heap(engine.Graphics.Device, candidate, props, devicePageSize);
            }

        }

        void FindFastHost(VkPhysicalDeviceMemoryProperties props) {
            //try to find heap that is device local and can be written from the host

            VkMemoryPropertyFlags flags = VkMemoryPropertyFlags.DeviceLocalBit | VkMemoryPropertyFlags.HostVisibleBit | VkMemoryPropertyFlags.HostCoherentBit;
            int candidate;

            Match(props, flags, out candidate);

            if (candidate != -1) {
                fastHostHeap = new Heap(engine.Graphics.Device, candidate, props, fastHostPageSize);
            }
        }

        void FindHostRead(VkPhysicalDeviceMemoryProperties props) {
            //try to find heap that is host visible and cached

            VkMemoryPropertyFlags flags = VkMemoryPropertyFlags.HostVisibleBit | VkMemoryPropertyFlags.HostCoherentBit | VkMemoryPropertyFlags.HostCachedBit;
            int candidate;

            Match(props, flags, out candidate);

            if (candidate != -1) {
                hostReadHeap = new Heap(engine.Graphics.Device, candidate, props, hostReadPageSize);
            }
        }

        void FindHost(VkPhysicalDeviceMemoryProperties props) {
            //find host heap

            VkMemoryPropertyFlags optimal = VkMemoryPropertyFlags.HostVisibleBit | VkMemoryPropertyFlags.HostCoherentBit;
            int candidate = -1;

            Match(props, optimal, out candidate);

            if (candidate != -1) {
                hostHeap = new Heap(engine.Graphics.Device, candidate, props, hostPageSize);

                //if other heaps have not been found, use host as fallback
                if (fastHostHeap == null) fastHostHeap = hostHeap;
                if (hostReadHeap == null) hostReadHeap = hostHeap;
            }
        }

        void Match(VkPhysicalDeviceMemoryProperties props, VkMemoryPropertyFlags flags, out int candidate) {
            candidate = -1;
            ulong candidateSize = 0;

            for (int i = 0; i < props.memoryTypeCount; i++) {
                var type = props.GetMemoryTypes(i);
                if ((type.propertyFlags & flags) == flags) {
                    var heap = props.GetMemoryHeaps((int)type.heapIndex);
                    if (heap.size > candidateSize) {
                        candidate = (int)type.heapIndex;
                        candidateSize = heap.size;
                    }
                }
            }
        }

        void MatchStrict(VkPhysicalDeviceMemoryProperties props, VkMemoryPropertyFlags flags, out int candidate) {
            candidate = -1;
            ulong candidateSize = 0;

            for (int i = 0; i < props.memoryTypeCount; i++) {
                var type = props.GetMemoryTypes(i);
                if (type.propertyFlags == flags) {  //exact match
                    var heap = props.GetMemoryHeaps((int)type.heapIndex);
                    if (heap.size > candidateSize) {
                        candidate = (int)type.heapIndex;
                        candidateSize = heap.size;
                    }
                }
            }
        }

        public void Dispose() {
            deviceHeap.Dispose();
            fastHostHeap.Dispose();
            hostHeap.Dispose();
            hostReadHeap.Dispose();
            TransferNode.Dispose();
        }
    }
}
