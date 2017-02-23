using System;
using System.IO;
using System.Numerics;
using System.Collections.Generic;

using CSGL;
using CSGL.Vulkan;

using UnnamedEngine.Core;
using UnnamedEngine.UI.Text;

namespace Test {
    public class TextRenderer : CommandNode {
        struct FontMetrics {
            Vector4 color;
            float bias;
            float scale;

            public FontMetrics(Vector4 color, float bias, float scale) {
                this.color = color;
                this.bias = bias;
                this.scale = scale;
            }
        }

        bool disposed;
        Engine engine;
        Device device;
        Renderer renderer;
        GlyphCache glyphCache;

        RenderPass renderPass;
        List<ImageView> imageViews;
        List<Framebuffer> framebuffers;
        CommandPool commandPool;
        CommandBuffer commandBuffer;
        CommandBufferBeginInfo beginInfo;
        RenderPassBeginInfo renderPassBeginInfo;
        PipelineLayout pipelineLayout;
        Pipeline pipeline;

        public TextRenderer(Engine engine, Renderer renderer, GlyphCache glyphCache) : base(engine.Graphics.Device) {
            if (engine == null) throw new ArgumentNullException(nameof(engine));
            if (renderer == null) throw new ArgumentNullException(nameof(renderer));
            if (glyphCache == null) throw new ArgumentNullException(nameof(glyphCache));

            this.engine = engine;
            this.renderer = renderer;
            this.glyphCache = glyphCache;
            device = engine.Graphics.Device;

            SrcStage = VkPipelineStageFlags.ColorAttachmentOutputBit;
            DstStage = VkPipelineStageFlags.ColorAttachmentOutputBit;
            EventStage = VkPipelineStageFlags.ColorAttachmentOutputBit;

            imageViews = new List<ImageView>();
            framebuffers = new List<Framebuffer>();

            beginInfo = new CommandBufferBeginInfo();
            beginInfo.flags = VkCommandBufferUsageFlags.OneTimeSubmitBit;

            CreateRenderPass();

            renderPassBeginInfo = new RenderPassBeginInfo();
            renderPassBeginInfo.renderPass = renderPass;
            renderPassBeginInfo.renderArea.extent = engine.Window.SwapchainExtent;

            CreateCommandPool();
            CreateFramebuffers();
            CreatePipeline();

            engine.Window.OnSizeChanged += Recreate;
        }

        protected override void OnBake() {

        }

        void Recreate(int x, int y) {
            CreateFramebuffers();
            CreatePipeline();
        }

        void CreateRenderPass() {
            var colorAttachment = new AttachmentDescription();
            colorAttachment.format = engine.Window.SwapchainImageFormat;
            colorAttachment.samples = VkSampleCountFlags._1Bit;
            colorAttachment.loadOp = VkAttachmentLoadOp.Load;
            colorAttachment.storeOp = VkAttachmentStoreOp.Store;
            colorAttachment.stencilLoadOp = VkAttachmentLoadOp.DontCare;
            colorAttachment.stencilStoreOp = VkAttachmentStoreOp.DontCare;
            colorAttachment.initialLayout = VkImageLayout.ColorAttachmentOptimal;
            colorAttachment.finalLayout = VkImageLayout.PresentSrcKhr;

            var colorAttachmentRef = new AttachmentReference();
            colorAttachmentRef.attachment = 0;
            colorAttachmentRef.layout = VkImageLayout.ColorAttachmentOptimal;

            var subpass = new SubpassDescription();
            subpass.pipelineBindPoint = VkPipelineBindPoint.Graphics;
            subpass.colorAttachments = new List<AttachmentReference> { colorAttachmentRef };

            var dependency = new SubpassDependency();
            dependency.srcSubpass = uint.MaxValue;  //VK_SUBPASS_EXTERNAL
            dependency.dstSubpass = 0;
            dependency.srcStageMask = VkPipelineStageFlags.ColorAttachmentOutputBit;
            dependency.srcAccessMask = VkAccessFlags.MemoryReadBit;
            dependency.dstStageMask = VkPipelineStageFlags.ColorAttachmentOutputBit;
            dependency.dstAccessMask = VkAccessFlags.ColorAttachmentReadBit
                                    | VkAccessFlags.ColorAttachmentWriteBit;

            var info = new RenderPassCreateInfo();
            info.attachments = new List<AttachmentDescription> { colorAttachment };
            info.subpasses = new List<SubpassDescription> { subpass };
            info.dependencies = new List<SubpassDependency> { dependency };
            
            renderPass = new RenderPass(engine.Graphics.Device, info);
        }

        void CreateFramebuffers() {
            foreach (var iv in imageViews) iv.Dispose();
            foreach (var fb in framebuffers) fb.Dispose();
            imageViews.Clear();
            framebuffers.Clear();

            for (int i = 0; i < engine.Window.SwapchainImages.Count; i++) {
                ImageViewCreateInfo ivInfo = new ImageViewCreateInfo(engine.Window.SwapchainImages[i]);
                ivInfo.viewType = VkImageViewType._2d;
                ivInfo.format = engine.Window.SwapchainImageFormat;
                ivInfo.subresourceRange.aspectMask = VkImageAspectFlags.ColorBit;
                ivInfo.subresourceRange.baseMipLevel = 0;
                ivInfo.subresourceRange.levelCount = 1;
                ivInfo.subresourceRange.baseArrayLayer = 0;
                ivInfo.subresourceRange.layerCount = 1;

                imageViews.Add(new ImageView(engine.Graphics.Device, ivInfo));

                FramebufferCreateInfo fbInfo = new FramebufferCreateInfo();
                fbInfo.renderPass = renderPass;
                fbInfo.attachments = new List<ImageView> { imageViews[i] };
                fbInfo.height = engine.Window.SwapchainExtent.height;
                fbInfo.width = engine.Window.SwapchainExtent.width;
                fbInfo.layers = 1;

                framebuffers.Add(new Framebuffer(engine.Graphics.Device, fbInfo));
            }
        }

        void CreateCommandPool() {
            CommandPoolCreateInfo poolInfo = new CommandPoolCreateInfo();
            poolInfo.queueFamilyIndex = engine.Graphics.GraphicsQueue.FamilyIndex;
            poolInfo.flags = VkCommandPoolCreateFlags.ResetCommandBufferBit;

            commandPool = new CommandPool(engine.Graphics.Device, poolInfo);

            commandBuffer = commandPool.Allocate(VkCommandBufferLevel.Primary);
        }

        ShaderModule CreateShaderModule(Device device, byte[] code) {
            var info = new ShaderModuleCreateInfo(code);
            return new ShaderModule(device, info);
        }

        void CreatePipeline() {
            var vert = CreateShaderModule(engine.Graphics.Device, File.ReadAllBytes("text_vert.spv"));
            var frag = CreateShaderModule(engine.Graphics.Device, File.ReadAllBytes("text_frag.spv"));

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
            colorBlendAttachment.colorBlendOp = VkBlendOp.Add;
            colorBlendAttachment.srcColorBlendFactor = VkBlendFactor.SrcAlpha;
            colorBlendAttachment.dstColorBlendFactor = VkBlendFactor.OneMinusSrcAlpha;
            colorBlendAttachment.alphaBlendOp = VkBlendOp.Add;
            colorBlendAttachment.srcAlphaBlendFactor = VkBlendFactor.One;
            colorBlendAttachment.dstAlphaBlendFactor = VkBlendFactor.Zero;

            var colorBlending = new PipelineColorBlendStateCreateInfo();
            colorBlending.logicOp = VkLogicOp.Copy;
            colorBlending.attachments = new List<PipelineColorBlendAttachmentState> { colorBlendAttachment };

            var pipelineLayoutInfo = new PipelineLayoutCreateInfo();
            pipelineLayoutInfo.setLayouts = new List<DescriptorSetLayout> { glyphCache.DescriptorLayout };
            pipelineLayoutInfo.pushConstantRanges = new List<VkPushConstantRange> {
                new VkPushConstantRange {
                    offset = 0,
                    size = (uint)Interop.SizeOf<FontMetrics>(),
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
            info.subpass = 0;
            info.basePipeline = pipeline;
            info.basePipelineIndex = -1;

            pipeline = new Pipeline(engine.Graphics.Device, info, null);

            oldPipeline?.Dispose();

            vert.Dispose();
            frag.Dispose();
        }

        public override CommandBuffer GetCommands() {
            RecordCommands();
            return commandBuffer;
        }

        void RecordCommands() {
            commandBuffer.Reset(VkCommandBufferResetFlags.None);
            commandBuffer.Begin(beginInfo);

            WaitEvents(commandBuffer);

            renderPassBeginInfo.framebuffer = framebuffers[(int)renderer.ImageIndex];
            commandBuffer.BeginRenderPass(renderPassBeginInfo, VkSubpassContents.Inline);

            commandBuffer.BindPipeline(VkPipelineBindPoint.Graphics, pipeline);
            commandBuffer.BindDescriptorSets(VkPipelineBindPoint.Graphics, pipelineLayout, 0, glyphCache.Descriptor);
            commandBuffer.PushConstants(pipelineLayout, VkShaderStageFlags.FragmentBit, 0, new FontMetrics(new Vector4(0, 0, 0, 1), 0.375f, 2f));
            commandBuffer.Draw(6, 1, 0, 0);
            commandBuffer.PushConstants(pipelineLayout, VkShaderStageFlags.FragmentBit, 0, new FontMetrics(new Vector4(1, 1, 1, 1), 0.5f, 2f));
            commandBuffer.Draw(6, 1, 0, 0);

            commandBuffer.EndRenderPass();

            SetEvents(commandBuffer);
            commandBuffer.End();
        }

        public new void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected override void Dispose(bool disposing) {
            if (disposed) return;
            base.Dispose(disposing);

            commandPool.Dispose();
            renderPass.Dispose();
            foreach (var fb in framebuffers) fb.Dispose();
            foreach (var iv in imageViews) iv.Dispose();
            pipeline.Dispose();
            pipelineLayout.Dispose();

            disposed = true;
        }
    }
}
