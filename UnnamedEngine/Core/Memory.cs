using System;
using System.Collections.Generic;

using CSGL.Vulkan;
using Buffer = CSGL.Vulkan.Buffer;

using UnnamedEngine.Resources;
using UnnamedEngine.Utilities;

namespace UnnamedEngine.Core {
    public class Memory : IDisposable {
        Engine engine;

        MemoryHeap deviceHeap;    //GPU memory
        MemoryHeap fastHostHeap;    //GPU memory that's writable by the CPU
        MemoryHeap hostHeap;    //CPU memory
        MemoryHeap hostReadHeap;    //cached CPU memory that's writable by the GPU

        public TransferNode TransferNode { get; private set; }

        HeapAllocator deviceAllocator;
        LinearAllocator stagingAllocator;
        HeapAllocator uniformAllocator;
        HeapAllocator hostReadAllocator;
        
        List<IDisposable> frontStagingBuffers;
        List<IDisposable> backStagingBuffers;

        const int devicePageSize = 256 * 1024 * 1024;
        const int fastHostPageSize = 32 * 1024 * 1024;
        const int hostPageSize = 256 * 1024 * 1024;
        const int hostReadPageSize = 256 * 1024 * 1024;

        internal Memory(Engine engine) {
            this.engine = engine;
            
            frontStagingBuffers = new List<IDisposable>();
            backStagingBuffers = new List<IDisposable>();

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

            lock (backStagingBuffers) {
                backStagingBuffers.Add(buffer);
            }

            return buffer;
        }

        public Image AllocStaging(ImageCreateInfo info) {
            Image image = AllocStagingInternal(info);

            lock (backStagingBuffers) {
                backStagingBuffers.Add(image);
            }

            return image;
        }

        public void ResetStaging() {
            lock (frontStagingBuffers) {
                for (int i = 0; i < frontStagingBuffers.Count; i++) {
                    frontStagingBuffers[i].Dispose();
                }

                frontStagingBuffers.Clear();
            }

            lock (frontStagingBuffers)
            lock (backStagingBuffers) {
                List<IDisposable> temp = frontStagingBuffers;
                frontStagingBuffers = backStagingBuffers;
                backStagingBuffers = temp;
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

        void FindHeaps() {
            PhysicalDevice physicalDevice = engine.Graphics.PhysicalDevice;
            VkPhysicalDeviceMemoryProperties props = physicalDevice.MemoryProperties;
            
            FindDevice(props);
            FindFastHost(props);
            FindHostRead(props);
            FindHost(props);

            TransferNode = new StagingNode(engine);

            deviceAllocator = new HeapAllocator(new List<MemoryHeap> {
                deviceHeap,
                fastHostHeap,
                hostHeap,
                hostReadHeap
            });

            stagingAllocator = new LinearAllocator(new List<MemoryHeap> {
                fastHostHeap,
                hostHeap,
                hostReadHeap
            });

            uniformAllocator = new HeapAllocator(new List<MemoryHeap> {
                fastHostHeap,
                hostHeap,
                hostReadHeap
            });

            hostReadAllocator = new HeapAllocator(new List<MemoryHeap> {
                hostReadHeap,
                hostHeap
            });
        }

        void FindDevice(VkPhysicalDeviceMemoryProperties props) {
            int candidate = -1;

            //try to find largest device local heap that has only DeviceLocalBit
            //this avoids selecting main RAM as device heap when VRAM heap is available

            MatchStrict(props, VkMemoryPropertyFlags.DeviceLocalBit, out candidate);

            if (candidate != -1) {
                deviceHeap = new MemoryHeap(engine.Graphics.Device, candidate, props, devicePageSize);
                return;
            }

            //select whichever heap is largest
            Match(props, VkMemoryPropertyFlags.DeviceLocalBit, out candidate);
            
            if (candidate != -1) {
                deviceHeap = new MemoryHeap(engine.Graphics.Device, candidate, props, devicePageSize);
            }

        }

        void FindFastHost(VkPhysicalDeviceMemoryProperties props) {
            //try to find heap that is device local and can be written from the host

            VkMemoryPropertyFlags flags = VkMemoryPropertyFlags.DeviceLocalBit | VkMemoryPropertyFlags.HostVisibleBit | VkMemoryPropertyFlags.HostCoherentBit;
            int candidate;

            Match(props, flags, out candidate);

            if (candidate != -1) {
                fastHostHeap = new MemoryHeap(engine.Graphics.Device, candidate, props, fastHostPageSize);
            }
        }

        void FindHostRead(VkPhysicalDeviceMemoryProperties props) {
            //try to find heap that is host visible and cached

            VkMemoryPropertyFlags flags = VkMemoryPropertyFlags.HostVisibleBit | VkMemoryPropertyFlags.HostCoherentBit | VkMemoryPropertyFlags.HostCachedBit;
            int candidate;

            Match(props, flags, out candidate);

            if (candidate != -1) {
                hostReadHeap = new MemoryHeap(engine.Graphics.Device, candidate, props, hostReadPageSize);
            }
        }

        void FindHost(VkPhysicalDeviceMemoryProperties props) {
            //find host heap

            VkMemoryPropertyFlags optimal = VkMemoryPropertyFlags.HostVisibleBit | VkMemoryPropertyFlags.HostCoherentBit;
            int candidate = -1;

            Match(props, optimal, out candidate);

            if (candidate != -1) {
                hostHeap = new MemoryHeap(engine.Graphics.Device, candidate, props, hostPageSize);

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
