using System;
using System.Collections.Generic;
using System.Numerics;

using CSGL;
using CSGL.Vulkan;
using Buffer = CSGL.Vulkan.Buffer;

using UnnamedEngine.Core;
using UnnamedEngine.Utilities;

namespace UnnamedEngine.Rendering {
    public class Camera : IDisposable {
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
        Matrix4x4 view;

        public Transform Transform { get; private set; }
        public float FOV { get; set; }
        public float Near { get; set; }
        public float Far { get; set; }
        public bool Infinite { get; set; }

        public DescriptorSetLayout Layout {
            get {
                return layout;
            }
        }

        public DescriptorSet Desciptor {
            get {
                return set;
            }
        }

        public Camera(Engine engine, Window window, float fov, float near, float far, bool infinite) {
            if (engine == null) throw new ArgumentNullException(nameof(engine));
            if (window == null) throw new ArgumentNullException(nameof(window));

            this.engine = engine;
            device = engine.Graphics.Device;
            this.window = window;

            FOV = fov;
            Near = near;
            Far = far;
            Infinite = infinite;

            Transform = new Transform();

            CreateBuffer();
            CreateDescriptor();

            Update();
        }

        public Camera(Engine engine, Window window, float fov, float near) : this(engine, window, fov, near, 0, true) { }
        public Camera(Engine engine, Window window, float fov, float near, float far) : this(engine, window, fov, near, far, false) { }

        internal void Update() {
            if (Infinite) {
                projection = CreatePerspectiveInfinite(FOV, window.Width / (float)window.Height, Near);
            } else {
                projection = CreatePerspective(FOV, window.Width / (float)window.Height, Near, Far);
            }

            projection.M22 *= -1;
            view = Matrix4x4.CreateLookAt(Transform.Position, Transform.Position + Transform.Forward, Transform.Up);

            IntPtr ptr = alloc.memory.Map(alloc.offset, alloc.size);
            Interop.Copy(view * projection, ptr);   //for some reason, the view has to be post multiplied by the projection
            alloc.memory.Unmap();
        }

        Matrix4x4 CreatePerspectiveInfinite(float fov, float aspect, float near) {
            //creates projection matrix with reversed Z and infinite far plane
            //https://nlguillemot.wordpress.com/2016/12/07/reversed-z-in-opengl/
            //http://dev.theomader.com/depth-precision/

            float fovRad = fov * (float)(Math.PI / 180);
            float f = 1f / (float)Math.Tan(fovRad / 2);

            return new Matrix4x4(
                f / aspect, 0, 0, 0,
                0, f, 0, 0,
                0, 0, 0, -1f,
                0, 0, near, 0);
        }

        Matrix4x4 CreatePerspective(float fov, float aspect, float near, float far) {
            //creates projection matrix with reversed Z and infinite far plane
            //http://dev.theomader.com/depth-precision/

            float fovRad = fov * (float)(Math.PI / 180);
            float f = 1f / (float)Math.Tan(fovRad / 2);

            float a = -(Far / (Near - Far));

            return new Matrix4x4(
                f / aspect, 0, 0, 0,
                0, f, 0, 0,
                0, 0, a - 1, -1f,
                0, 0, near * a, 0);
        }

        void CreateDescriptor() {
            VkDescriptorSetLayoutBinding binding = new VkDescriptorSetLayoutBinding();
            binding.binding = 0;
            binding.descriptorCount = 1;
            binding.descriptorType = VkDescriptorType.UniformBuffer;
            binding.stageFlags = VkShaderStageFlags.VertexBit | VkShaderStageFlags.FragmentBit;

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

            buffer = new Buffer(device, info);

            alloc = engine.Graphics.Allocator.Alloc(buffer.Requirements, VkMemoryPropertyFlags.HostVisibleBit | VkMemoryPropertyFlags.HostCoherentBit);
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

            engine.Graphics.Allocator.Free(alloc);

            disposed = true;
        }

        ~Camera() {
            Dispose(false);
        }
    }
}
