using System;
using System.IO;
using System.Collections.Generic;

using CSGL.Vulkan;

using UnnamedEngine.Core;
using UnnamedEngine.Rendering;

namespace Test {
    public class LightingNode : RenderNode, IDisposable {
        bool disposed;

        Engine engine;
        CommandPool pool;
        GBuffer gbuffer;
        RenderPass renderPass;
        uint subpassIndex;

        PipelineLayout pipelineLayout;
        Pipeline pipeline;
        List<CommandBuffer> submitBuffers;

        public LightingNode(Engine engine, GBuffer gbuffer, CommandPool pool) {
            this.engine = engine;
            this.pool = pool;
            this.gbuffer = gbuffer;
        }

        ShaderModule CreateShaderModule(Device device, byte[] code) {
            var info = new ShaderModuleCreateInfo(code);
            return new ShaderModule(device, info);
        }

        void CreatePipeline() {
            var vert = CreateShaderModule(engine.Graphics.Device, File.ReadAllBytes("deferred_vert.spv"));
            var frag = CreateShaderModule(engine.Graphics.Device, File.ReadAllBytes("deferred_frag.spv"));

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

            var depthState = new PipelineDepthStencilStateCreateInfo();
            depthState.depthTestEnable = false;
            depthState.depthWriteEnable = false;

            var pipelineLayoutInfo = new PipelineLayoutCreateInfo();
            pipelineLayoutInfo.setLayouts = new List<DescriptorSetLayout> { gbuffer.InputLayout };

            pipelineLayout?.Dispose();

            pipelineLayout = new PipelineLayout(engine.Graphics.Device, pipelineLayoutInfo);

            var info = new GraphicsPipelineCreateInfo();
            info.stages = shaderStages;
            info.vertexInputState = vertexInputInfo;
            info.inputAssemblyState = inputAssembly;
            info.viewportState = viewportState;
            info.rasterizationState = rasterizer;
            info.multisampleState = multisampling;
            info.colorBlendState = colorBlending;
            info.depthStencilState = depthState;
            info.layout = pipelineLayout;
            info.renderPass = renderPass;
            info.subpass = subpassIndex;
            info.basePipeline = null;
            info.basePipelineIndex = -1;

            pipeline?.Dispose();

            pipeline = new Pipeline(engine.Graphics.Device, info, null);

            vert.Dispose();
            frag.Dispose();
        }

        public void Init(Framebuffer framebuffer) {
            CommandBuffer commandBuffer = pool.Allocate(VkCommandBufferLevel.Secondary);

            CommandBufferInheritanceInfo inheritance = new CommandBufferInheritanceInfo();
            inheritance.renderPass = renderPass;
            inheritance.subpass = subpassIndex;
            inheritance.framebuffer = framebuffer;

            CommandBufferBeginInfo beginInfo = new CommandBufferBeginInfo();
            beginInfo.flags = VkCommandBufferUsageFlags.RenderPassContinueBit | VkCommandBufferUsageFlags.SimultaneousUseBit;
            beginInfo.inheritanceInfo = inheritance;

            commandBuffer.Begin(beginInfo);

            commandBuffer.BindPipeline(VkPipelineBindPoint.Graphics, pipeline);
            commandBuffer.BindDescriptorSets(VkPipelineBindPoint.Graphics, pipelineLayout, 0, new List<DescriptorSet> { gbuffer.InputDescriptor }, null);
            commandBuffer.Draw(3, 1, 0, 0);

            commandBuffer.End();

            submitBuffers = new List<CommandBuffer> { commandBuffer };
        }

        protected override void Bake(RenderPass renderPass, uint subpassIndex) {
            this.renderPass = renderPass;
            this.subpassIndex = subpassIndex;

            CreatePipeline();
        }

        public override List<CommandBuffer> GetCommands() {
            return submitBuffers;
        }

        public new void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected override void Dispose(bool disposing) {
            if (disposed) return;

            pipeline.Dispose();
            pipelineLayout.Dispose();

            base.Dispose(disposing);

            disposed = true;
        }
    }
}
