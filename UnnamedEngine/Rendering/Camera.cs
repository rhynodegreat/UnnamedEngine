using System;
using System.Collections.Generic;
using System.Numerics;

using CSGL;
using CSGL.Vulkan;
using Buffer = CSGL.Vulkan.Buffer;

using UnnamedEngine.Core;
using UnnamedEngine.Utilities;

namespace UnnamedEngine.Rendering {
    public class Camera :IDisposable {
        bool disposed;
        Engine engine;
        Device device;
        Window window;

        DescriptorSetLayout layout;
        DescriptorPool pool;
        DescriptorSet set;
        Buffer buffer;
        VkaAllocation alloc;

        Matrix4x4 projection;
        Matrix4x4 viewMatrix;

        public Transform Transform { get; private set; }
        public float FOV { get; set; }
        public float Near { get; set; }
        public float Far { get; set; }

        public Camera(Engine engine, Window window, float fov, float near, float far) {
            if (engine == null) throw new ArgumentNullException(nameof(engine));
            if (window == null) throw new ArgumentNullException(nameof(window));

            this.engine = engine;
            device = engine.Renderer.Device;
            this.window = window;

            FOV = fov;
            Near = near;
            Far = far;

            Transform = new Transform();

            CreateBuffer();
            CreateDescriptor();

            Update();
        }

        internal void Update() {
            projection = Matrix4x4.CreatePerspectiveFieldOfView(FOV * (float)(Math.PI / 180), (float)window.Width / window.Height, Near, Far);
            viewMatrix = Matrix4x4.CreateLookAt(Transform.Position, Transform.Position + Transform.Forward, Transform.Up);

            IntPtr ptr = alloc.memory.Map(alloc.offset, alloc.size);
            Interop.Copy(projection * viewMatrix, ptr);
            alloc.memory.Unmap();
        }

        void CreateDescriptor() {
            VkDescriptorSetLayoutBinding binding = new VkDescriptorSetLayoutBinding();
            binding.binding = 0;
            binding.descriptorCount = 1;
            binding.descriptorType = VkDescriptorType.UniformBuffer;
            binding.stageFlags = VkShaderStageFlags.VertexBit;

            DescriptorSetLayoutCreateInfo layoutInfo = new DescriptorSetLayoutCreateInfo();
            layoutInfo.bindings = new List<VkDescriptorSetLayoutBinding> { binding };

            layout = new DescriptorSetLayout(device, layoutInfo);

            VkDescriptorPoolSize size = new VkDescriptorPoolSize();
            size.descriptorCount = 1;
            size.type = VkDescriptorType.UniformBuffer;

            DescriptorPoolCreateInfo poolInfo = new DescriptorPoolCreateInfo();
            poolInfo.maxSets = 1;
            poolInfo.poolSizes = new List<VkDescriptorPoolSize> { size };

            pool = new DescriptorPool(device, poolInfo);

            DescriptorSetAllocateInfo setInfo = new DescriptorSetAllocateInfo();
            setInfo.descriptorSetCount = 1;
            setInfo.setLayouts = new List<DescriptorSetLayout> { layout };

            set = pool.Allocate(setInfo)[0];

            WriteDescriptorSet write = new WriteDescriptorSet();
            var bufferInfo = new DescriptorBufferInfo();
            bufferInfo.buffer = buffer;
            bufferInfo.offset = alloc.offset;
            bufferInfo.range = alloc.size;

            write.bufferInfo = new List<DescriptorBufferInfo> { bufferInfo };
            write.descriptorType = VkDescriptorType.UniformBuffer;
            write.dstArrayElement = 0;
            write.dstBinding = 0;
            write.dstSet = set;

            DescriptorSet.Update(device, new List<WriteDescriptorSet> { write });
        }

        void CreateBuffer() {
            BufferCreateInfo info = new BufferCreateInfo();
            info.size = (uint)Interop.SizeOf<Matrix4x4>();
            info.usage = VkBufferUsageFlags.UniformBufferBit;
            info.sharingMode = VkSharingMode.Exclusive;
            info.queueFamilyIndices = new List<uint> { engine.Renderer.GraphicsQueue.FamilyIndex };

            buffer = new Buffer(device, info);

            alloc = engine.Renderer.Allocator.Alloc(buffer.Requirements, VkMemoryPropertyFlags.HostVisibleBit | VkMemoryPropertyFlags.HostCoherentBit);
            buffer.Bind(alloc.memory, alloc.offset);
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposing) {
            if (disposed) return;


            pool.Dispose();
            layout.Dispose();
            buffer.Dispose();

            engine.Renderer.Allocator.Free(alloc);

            disposed = true;
        }

        ~Camera() {
            Dispose(false);
        }
    }
}
