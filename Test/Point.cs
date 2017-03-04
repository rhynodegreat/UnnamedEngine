using System;
using System.IO;
using System.Numerics;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using CSGL;
using CSGL.Graphics;
using CSGL.Vulkan;
using Buffer = CSGL.Vulkan.Buffer;

using UnnamedEngine.Core;
using UnnamedEngine.Rendering;
using UnnamedEngine.Resources;
using UnnamedEngine.Utilities;

namespace Test {
    public class Point : ISubpass {
        bool disposed;

        Engine engine;
        Deferred deferred;
        GBuffer gbuffer;
        RenderPass renderPass;
        uint subpassIndex;
        Camera camera;

        int lightCount;

        List<Light> lights;
        List<uint> lightIndices;
        List<LightData> lightData;
        CommandPool pool;
        CommandBuffer commandBuffer;
        PipelineLayout pipelineLayout;
        Pipeline pipeline;
        DescriptorSetLayout descriptorLayout;
        DescriptorPool descriptorPool;
        DescriptorSet set;
        Buffer uniform;
        VkaAllocation uniformAllocation;
        Mesh mesh;
        bool dirty = true;

        [StructLayout(LayoutKind.Explicit, Size = 80)]
        struct LightData {
            [FieldOffset(0)]
            public Color4 color;
            [FieldOffset(16)]
            public Matrix4x4 transform;
        }

        public Point(Engine engine, Deferred deferred, Camera camera, int lightCount) {
            if (engine == null) throw new ArgumentNullException(nameof(engine));
            if (deferred == null) throw new ArgumentNullException(nameof(deferred));
            if (lightCount < 0) throw new ArgumentOutOfRangeException(nameof(lightCount));

            this.engine = engine;
            this.deferred = deferred;
            gbuffer = deferred.GBuffer;
            this.camera = camera;

            this.lightCount = lightCount;
            lights = new List<Light>();
            lightIndices = new List<uint>();
            lightData = new List<LightData>();

            CommandPoolCreateInfo poolInfo = new CommandPoolCreateInfo();
            poolInfo.flags = VkCommandPoolCreateFlags.ResetCommandBufferBit;
            poolInfo.queueFamilyIndex = engine.Graphics.GraphicsQueue.FamilyIndex;

            pool = new CommandPool(engine.Graphics.Device, poolInfo);

            commandBuffer = pool.Allocate(VkCommandBufferLevel.Secondary);

            CreateDescriptor();
            CreateBuffer();
            CreateMesh();

            deferred.OnFramebufferChanged += () => {
                CreatePipeline();
                dirty = true;
            };
        }

        public void AddLight(Light light) {
            if (light == null) throw new ArgumentNullException(nameof(light));
            if (lights.Contains(light)) return;
            if (lights.Count == lightCount) throw new PointException(nameof(lightCount));

            lights.Add(light);
            lightData.Add(new LightData());
            dirty = true;
        }

        public void RemoveLight(Light light) {
            dirty = lights.Remove(light);
            lightData.RemoveAt(lightData.Count - 1);
        }

        void CreateDescriptor() {
            DescriptorSetLayoutCreateInfo layoutInfo = new DescriptorSetLayoutCreateInfo();
            layoutInfo.bindings = new List<VkDescriptorSetLayoutBinding> {
                new VkDescriptorSetLayoutBinding {
                    binding = 0,
                    descriptorCount = 1,
                    descriptorType = VkDescriptorType.UniformBufferDynamic,
                    stageFlags = VkShaderStageFlags.VertexBit | VkShaderStageFlags.FragmentBit
                }
            };

            descriptorLayout = new DescriptorSetLayout(engine.Graphics.Device, layoutInfo);

            DescriptorPoolCreateInfo poolInfo = new DescriptorPoolCreateInfo();
            poolInfo.maxSets = 1;
            poolInfo.poolSizes = new List<VkDescriptorPoolSize> {
                new VkDescriptorPoolSize {
                    descriptorCount = 1,
                    type = VkDescriptorType.UniformBufferDynamic
                }
            };

            descriptorPool = new DescriptorPool(engine.Graphics.Device, poolInfo);

            DescriptorSetAllocateInfo setInfo = new DescriptorSetAllocateInfo();
            setInfo.descriptorSetCount = 1;
            setInfo.setLayouts = new List<DescriptorSetLayout> { descriptorLayout };

            set = descriptorPool.Allocate(setInfo)[0];
        }

        void CreateBuffer() {
            BufferCreateInfo info = new BufferCreateInfo();
            info.usage = VkBufferUsageFlags.UniformBufferBit;
            info.size = (uint)(Interop.SizeOf<LightData>() * lightCount);
            info.sharingMode = VkSharingMode.Exclusive;

            uniform = new Buffer(engine.Graphics.Device, info);

            uniformAllocation = engine.Graphics.Allocator.Alloc(uniform.Requirements, VkMemoryPropertyFlags.HostVisibleBit | VkMemoryPropertyFlags.HostCoherentBit);
            uniform.Bind(uniformAllocation.memory, uniformAllocation.offset);

            DescriptorSet.Update(engine.Graphics.Device, new List<WriteDescriptorSet> {
                new WriteDescriptorSet {
                    bufferInfo = new List<DescriptorBufferInfo> {
                        new DescriptorBufferInfo {
                            buffer = uniform,
                            offset = 0,
                            range = (uint)Interop.SizeOf<LightData>()
                        }
                    },
                    descriptorType = VkDescriptorType.UniformBufferDynamic,
                    dstArrayElement = 0,
                    dstBinding = 0,
                    dstSet = set
                }
            });
        }

        void UpdateUniform() {
            for (int i = 0; i < lights.Count; i++) {
                var color = lights[i].Color;
                float brightness = Math.Max(color.r, Math.Max(color.g, color.b));
                float radius = 16f * (float)Math.Sqrt(brightness);  //(1/256) = brightness / (r^2)

                lightData[i] = new LightData {
                    color = color,
                    transform = Matrix4x4.CreateScale(radius) * Matrix4x4.CreateTranslation(lights[i].Transform.Position) };
            }

            IntPtr ptr = uniformAllocation.memory.Map(uniformAllocation.offset, uniformAllocation.size);
            Interop.Copy(lightData, ptr);
            uniformAllocation.memory.Unmap();
        }

        void CreateMesh() {
            using (var stream = File.OpenRead("pointLight.mesh")) {
                mesh = new Mesh(engine, stream);
            }
        }

        ShaderModule CreateShaderModule(Device device, byte[] code) {
            var info = new ShaderModuleCreateInfo(code);
            return new ShaderModule(device, info);
        }

        void CreatePipeline() {
            var vert = CreateShaderModule(engine.Graphics.Device, File.ReadAllBytes("shaders/point_vert.spv"));
            var frag = CreateShaderModule(engine.Graphics.Device, File.ReadAllBytes("shaders/point_frag.spv"));

            var vertInfo = new PipelineShaderStageCreateInfo();
            vertInfo.stage = VkShaderStageFlags.VertexBit;
            vertInfo.module = vert;
            vertInfo.name = "main";

            var fragInfo = new PipelineShaderStageCreateInfo();
            fragInfo.stage = VkShaderStageFlags.FragmentBit;
            fragInfo.module = frag;
            fragInfo.name = "main";

            SpecializationInfo specialization = new SpecializationInfo();
            specialization.mapEntries = new List<VkSpecializationMapEntry> {
                new VkSpecializationMapEntry {
                    constantID = 0,
                    offset = 0,
                    size = 4,
                },
                new VkSpecializationMapEntry {
                    constantID = 1,
                    offset = 4,
                    size = 4
                }
            };

            byte[] specializationData = new byte[8];
            Interop.Copy(gbuffer.Width, specializationData, 0);
            Interop.Copy(gbuffer.Height, specializationData, 4);

            specialization.data = specializationData;

            fragInfo.specializationInfo = specialization;

            var shaderStages = new List<PipelineShaderStageCreateInfo> { vertInfo, fragInfo };

            var vertexInputInfo = new PipelineVertexInputStateCreateInfo();
            vertexInputInfo.vertexBindingDescriptions = mesh.VertexData.Bindings;
            vertexInputInfo.vertexAttributeDescriptions = mesh.VertexData.Attributes;

            var inputAssembly = new PipelineInputAssemblyStateCreateInfo();
            inputAssembly.topology = VkPrimitiveTopology.TriangleList;

            var viewport = new VkViewport();
            viewport.width = engine.Window.SwapchainExtent.width;
            viewport.height = engine.Window.SwapchainExtent.height;
            viewport.minDepth = 0f;
            viewport.maxDepth = 1f;

            var scissor = new VkRect2D();
            scissor.extent = engine.Window.SwapchainExtent;

            var viewportState = new PipelineViewportStateCreateInfo();
            viewportState.viewports = new List<VkViewport> { viewport };
            viewportState.scissors = new List<VkRect2D> { scissor };

            var rasterizer = new PipelineRasterizationStateCreateInfo();
            rasterizer.polygonMode = VkPolygonMode.Fill;
            rasterizer.lineWidth = 1f;
            rasterizer.cullMode = VkCullModeFlags.FrontBit;
            rasterizer.frontFace = VkFrontFace.CounterClockwise;

            var multisampling = new PipelineMultisampleStateCreateInfo();
            multisampling.rasterizationSamples = VkSampleCountFlags._1Bit;
            multisampling.minSampleShading = 1f;

            var colorBlendAttachment = new PipelineColorBlendAttachmentState();
            colorBlendAttachment.colorWriteMask = VkColorComponentFlags.RBit
                                                | VkColorComponentFlags.GBit
                                                | VkColorComponentFlags.BBit
                                                | VkColorComponentFlags.ABit;
            colorBlendAttachment.blendEnable = true;
            colorBlendAttachment.srcColorBlendFactor = VkBlendFactor.One;
            colorBlendAttachment.dstColorBlendFactor = VkBlendFactor.One;
            colorBlendAttachment.colorBlendOp = VkBlendOp.Add;
            colorBlendAttachment.srcAlphaBlendFactor = VkBlendFactor.One;
            colorBlendAttachment.dstAlphaBlendFactor = VkBlendFactor.Zero;
            colorBlendAttachment.alphaBlendOp = VkBlendOp.Add;

            var colorBlending = new PipelineColorBlendStateCreateInfo();
            colorBlending.logicOp = VkLogicOp.Copy;
            colorBlending.attachments = new List<PipelineColorBlendAttachmentState> { colorBlendAttachment };

            var pipelineLayoutInfo = new PipelineLayoutCreateInfo();
            pipelineLayoutInfo.setLayouts = new List<DescriptorSetLayout> { gbuffer.InputLayout, descriptorLayout, camera.Layout };
            pipelineLayoutInfo.pushConstantRanges = new List<VkPushConstantRange> {
                new VkPushConstantRange {
                    offset = 0,
                    size = 4,
                    stageFlags = VkShaderStageFlags.VertexBit | VkShaderStageFlags.FragmentBit
                }
            };

            pipelineLayout?.Dispose();

            pipelineLayout = new PipelineLayout(engine.Graphics.Device, pipelineLayoutInfo);

            var oldPipeline = pipeline;

            var info = new GraphicsPipelineCreateInfo();
            info.stages = shaderStages;
            info.vertexInputState = vertexInputInfo;
            info.inputAssemblyState = inputAssembly;
            info.viewportState = viewportState;
            info.rasterizationState = rasterizer;
            info.multisampleState = multisampling;
            info.colorBlendState = colorBlending;
            info.layout = pipelineLayout;
            info.renderPass = renderPass;
            info.subpass = subpassIndex;
            info.basePipeline = oldPipeline;
            info.basePipelineIndex = -1;

            pipeline = new Pipeline(engine.Graphics.Device, info, null);

            oldPipeline?.Dispose();

            vert.Dispose();
            frag.Dispose();
        }

        public void Bake(RenderPass renderPass, uint subpassIndex) {
            this.renderPass = renderPass;
            this.subpassIndex = subpassIndex;

            CreatePipeline();
        }

        void RecordCommands() {
            commandBuffer.Reset(VkCommandBufferResetFlags.None);

            CommandBufferInheritanceInfo inheritance = new CommandBufferInheritanceInfo();
            inheritance.renderPass = renderPass;
            inheritance.subpass = subpassIndex;
            inheritance.framebuffer = deferred.Framebuffer;

            CommandBufferBeginInfo beginInfo = new CommandBufferBeginInfo();
            beginInfo.flags = VkCommandBufferUsageFlags.RenderPassContinueBit;
            beginInfo.inheritanceInfo = inheritance;

            commandBuffer.Begin(beginInfo);

            commandBuffer.BindPipeline(VkPipelineBindPoint.Graphics, pipeline);
            commandBuffer.BindDescriptorSets(VkPipelineBindPoint.Graphics, pipelineLayout, 0, gbuffer.InputDescriptor);
            commandBuffer.BindDescriptorSets(VkPipelineBindPoint.Graphics, pipelineLayout, 2, camera.Descriptor);
            commandBuffer.BindVertexBuffer(0, mesh.VertexBuffer, 0);
            commandBuffer.BindIndexBuffer(mesh.IndexBuffer, 0, mesh.IndexData.IndexType);

            for (uint i = 0; i < lights.Count; i++) {
                commandBuffer.BindDescriptorSets(VkPipelineBindPoint.Graphics, pipelineLayout, 1, set, i * 80);
                commandBuffer.DrawIndexed((uint)mesh.IndexData.IndexCount, 1, 0, 0, 0);
            }

            commandBuffer.End();
        }

        public CommandBuffer GetCommandBuffer() {
            UpdateUniform();
            if (dirty) {
                RecordCommands();
                dirty = false;
            }
            return commandBuffer;
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposing) {
            if (disposed) return;

            pipeline.Dispose();
            pipelineLayout.Dispose();
            pool.Dispose();
            descriptorPool.Dispose();
            descriptorLayout.Dispose();
            uniform.Dispose();
            engine.Graphics.Allocator.Free(uniformAllocation);
            mesh.Dispose();

            deferred.OnFramebufferChanged -= CreatePipeline;

            disposed = true;
        }

        ~Point() {
            Dispose(false);
        }
    }

    public class PointException : Exception {
        public PointException(string message) : base(message) { }
    }
}
