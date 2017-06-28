using System;
using System.Collections.Generic;
using System.Numerics;

using CSGL;
using CSGL.Vulkan1;
using Buffer = CSGL.Vulkan1.Buffer;

using UnnamedEngine.Core;
using UnnamedEngine.Resources;

namespace UnnamedEngine.Rendering {
    public class CameraManager : IDisposable {
        bool disposed;

        Engine engine;

        List<Camera> cameras;
        int lastCapacity;
        DescriptorPool pool;
        UniformBuffer<Matrix4x4> buffer;
        WriteDescriptorSet write;

        public DescriptorSetLayout Layout { get; private set; }
        public DescriptorSet Descriptor { get; private set; }

        public Camera MainCamera { get; set; }

        public CameraManager(Engine engine) {
            if (engine == null) throw new ArgumentNullException(nameof(engine));

            this.engine = engine;

            cameras = new List<Camera>(4);
            lastCapacity = cameras.Capacity;

            CreateDescriptor();
            CreateBuffer();
        }

        public void AddCamera(Camera camera) {
            camera.Manager = this;
            cameras.Add(camera);
            buffer.Add();
            int index = cameras.Count - 1;
            camera.Index = index;
            camera.Offset = (uint)(index * buffer.AlignedSize);

            if (MainCamera == null) MainCamera = camera;
        }

        public void RemoveCamera(Camera camera) {
            camera.Manager = null;
            cameras.Remove(camera);
            camera.Index = -1;
            camera.Offset = 0;
            buffer.Remove();
        }

        public void Update() {
            for (int i = 0; i < cameras.Count; i++) {
                cameras[i].Index = i;
                cameras[i].InternalUpdate();
                buffer[i] = cameras[i].ProjectionView;
            }
            buffer.Update();
        }

        void CreateDescriptor() {
            DescriptorSetLayoutCreateInfo layoutInfo = new DescriptorSetLayoutCreateInfo();
            layoutInfo.bindings = new List<VkDescriptorSetLayoutBinding> {
                new VkDescriptorSetLayoutBinding {
                    binding = 0,
                    descriptorType = VkDescriptorType.UniformBufferDynamic,
                    descriptorCount = 1,
                    stageFlags = VkShaderStageFlags.VertexBit | VkShaderStageFlags.FragmentBit
                }
            };

            Layout = new DescriptorSetLayout(engine.Graphics.Device, layoutInfo);

            DescriptorPoolCreateInfo poolInfo = new DescriptorPoolCreateInfo();
            poolInfo.maxSets = 1;
            poolInfo.poolSizes = new List<VkDescriptorPoolSize> {
                new VkDescriptorPoolSize {
                    type = VkDescriptorType.UniformBufferDynamic,
                    descriptorCount = 1
                }
            };

            pool = new DescriptorPool(engine.Graphics.Device, poolInfo);

            DescriptorSetAllocateInfo setInfo = new DescriptorSetAllocateInfo();
            setInfo.descriptorSetCount = 1;
            setInfo.setLayouts = new List<DescriptorSetLayout> { Layout };

            Descriptor = pool.Allocate(setInfo)[0];
        }

        void CreateBuffer() {
            write = new WriteDescriptorSet {
                descriptorType = VkDescriptorType.UniformBufferDynamic,
                dstArrayElement = 0,
                dstBinding = 0,
                dstSet = Descriptor,
            };

            buffer = new UniformBuffer<Matrix4x4>(engine, 2, VkBufferUsageFlags.None, Descriptor, write);
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposing) {
            if (disposed) return;

            Layout.Dispose();
            pool.Dispose();
            buffer.Dispose();

            disposed = true;
        }

        ~CameraManager() {
            Dispose(false);
        }
    }
}
