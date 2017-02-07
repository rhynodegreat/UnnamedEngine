using System;
using System.IO;
using System.Numerics;
using System.Collections.Generic;

using CSGL;
using CSGL.Graphics;
using CSGL.Vulkan;
using Buffer = CSGL.Vulkan.Buffer;

using UnnamedEngine.Core;
using UnnamedEngine.Rendering;
using UnnamedEngine.Utilities;

namespace Test {
    public class Directional : ISubpass {
        bool disposed;

        Engine engine;
        DeferredNode deferred;
        Camera camera;
        GBuffer gbuffer;
        RenderPass renderPass;
        uint subpassIndex;

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
        bool dirty = true;

        struct LightData {
            public Color color;
            public Vector4 direction;
        }

        public Directional(Engine engine, DeferredNode deferred, Camera camera) {
            if (engine == null) throw new ArgumentNullException(nameof(engine));
            if (deferred == null) throw new ArgumentNullException(nameof(deferred));
            if (camera == null) throw new ArgumentNullException(nameof(camera));

            this.engine = engine;
            this.deferred = deferred;
            this.camera = camera;
            gbuffer = deferred.GBuffer;

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

            deferred.OnFramebufferChanged += () => {
                CreatePipeline();
            };
        }

        public void AddLight(Light light) {
            if (light == null) throw new ArgumentNullException(nameof(light));
            if (lights.Contains(light)) return;

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
                    stageFlags = VkShaderStageFlags.FragmentBit
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
            info.size = 1024;
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
                            range = 1024
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
                lightData[i] = new LightData { color = lights[i].Color, direction = new Vector4(lights[i].Transform.Forward, 0) };
            }

            IntPtr ptr = uniformAllocation.memory.Map(uniformAllocation.offset, uniformAllocation.size);
            Interop.Copy(lightData, ptr);
            uniformAllocation.memory.Unmap();
        }

        ShaderModule CreateShaderModule(Device device, byte[] code) {
            var info = new ShaderModuleCreateInfo(code);
            return new ShaderModule(device, info);
        }

        void CreatePipeline() {
            var vert = CreateShaderModule(engine.Graphics.Device, File.ReadAllBytes("directional_vert.spv"));
            var frag = CreateShaderModule(engine.Graphics.Device, File.ReadAllBytes("directional_frag.spv"));

            var vertInfo = new PipelineShaderStageCreateInfo();
            vertInfo.stage = VkShaderStageFlags.VertexBit;
            vertInfo.module = vert;
            vertInfo.name = "main";

            var fragInfo = new PipelineShaderStageCreateInfo();
            fragInfo.stage = VkShaderStageFlags.FragmentBit;
            fragInfo.module = frag;
            fragInfo.name = "main";

            var shaderStages = new List<PipelineShaderStageCreateInfo> { vertInfo, fragInfo };

            var vertexInputInfo = new PipelineVertexInputStateCreateInfo();

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
            rasterizer.cullMode = VkCullModeFlags.None;
            rasterizer.frontFace = VkFrontFace.Clockwise;

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
            pipelineLayoutInfo.setLayouts = new List<DescriptorSetLayout> { gbuffer.InputLayout, descriptorLayout };
            pipelineLayoutInfo.pushConstantRanges = new List<VkPushConstantRange> {
                new VkPushConstantRange {
                    offset = 0,
                    size = 32,
                    stageFlags = VkShaderStageFlags.FragmentBit
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

            for (uint i = 0; i < lights.Count; i++) {
                commandBuffer.BindDescriptorSets(VkPipelineBindPoint.Graphics, pipelineLayout, 1, set, i * 16);
                commandBuffer.Draw(6, 1, 0, 0);
            }

            commandBuffer.End();
        }

        public CommandBuffer GetCommandBuffer() {
            UpdateUniform();
            if (dirty) {
                RecordCommands();
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

            deferred.OnFramebufferChanged -= CreatePipeline;

            disposed = true;
        }

        ~Directional() {
            Dispose(false);
        }
    }
}
