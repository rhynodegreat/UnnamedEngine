using System;
using System.Collections.Generic;
using System.Numerics;

using CSGL;
using CSGL.Vulkan;
using Buffer = CSGL.Vulkan.Buffer;

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
            camera.Descriptor = Descriptor;
            camera.Layout = Layout;
            cameras.Add(camera);

            if (MainCamera == null) MainCamera = camera;
        }

        public void RemoveCamera(Camera camera) {
            camera.Descriptor = null;
            camera.Layout = null;
            cameras.Remove(camera);
        }

        public void Update() {
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
            buffer = new UniformBuffer<Matrix4x4>(engine, 1, VkBufferUsageFlags.None);

            Descriptor.Update(new List<WriteDescriptorSet> {
                new WriteDescriptorSet {
                    descriptorType = VkDescriptorType.UniformBufferDynamic,
                    dstArrayElement = 0,
                    dstBinding = 0,
                    dstSet = Descriptor,
                    bufferInfo = new List<DescriptorBufferInfo> {
                        new DescriptorBufferInfo {
                            buffer = buffer.Buffer,
                            offset = buffer.Buffer.Offset,
                            range = (uint)Interop.SizeOf<Matrix4x4>()
                        }
                    }
                }
            });
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
