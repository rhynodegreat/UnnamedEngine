using System;
using System.IO;
using System.Numerics;
using System.Collections.Generic;

using CSGL.Vulkan;

using UnnamedEngine.Core;
using UnnamedEngine.Rendering;

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
        bool dirty = true;
        CommandPool pool;
        CommandBuffer commandBuffer;
        PipelineLayout pipelineLayout;
        Pipeline pipeline;

        struct LightData {
            public Vector4 color;
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

            CommandPoolCreateInfo poolInfo = new CommandPoolCreateInfo();
            poolInfo.flags = VkCommandPoolCreateFlags.ResetCommandBufferBit;
            poolInfo.queueFamilyIndex = engine.Graphics.GraphicsQueue.FamilyIndex;

            pool = new CommandPool(engine.Graphics.Device, poolInfo);

            commandBuffer = pool.Allocate(VkCommandBufferLevel.Secondary);

            deferred.OnFramebufferChanged += () => {
                CreatePipeline();
                dirty = true;
            };
        }

        public void AddLight(Light light) {
            if (light == null) throw new ArgumentNullException(nameof(light));
            if (lights.Contains(light)) return;

            lights.Add(light);
            dirty = true;
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
            pipelineLayoutInfo.setLayouts = new List<DescriptorSetLayout> { gbuffer.InputLayout, camera.Layout };
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
            commandBuffer.BindDescriptorSets(VkPipelineBindPoint.Graphics, pipelineLayout, 1, camera.Desciptor);

            for (int i = 0; i < lights.Count; i++) {
                var forward = lights[i].Transform.Forward;
                var color = lights[i].Color;
                commandBuffer.PushConstants(pipelineLayout, VkShaderStageFlags.FragmentBit, 0, new LightData {
                    color = new Vector4(color.r, color.g, color.b, 0),
                    direction = new Vector4(forward.X, forward.Y, forward.Z, 0)
                });
                commandBuffer.Draw(6, 1, 0, 0);
            }

            commandBuffer.End();
        }

        public CommandBuffer GetCommandBuffer() {
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

            deferred.OnFramebufferChanged -= CreatePipeline;

            disposed = true;
        }

        ~Directional() {
            Dispose(false);
        }
    }
}
