using System;
using System.IO;
using System.Collections.Generic;

using CSGL.Vulkan;

using UnnamedEngine.Core;
using UnnamedEngine.Rendering;

namespace Test {
    public class ToneMapNode : CommandNode, IDisposable {
        bool disposed;

        Engine engine;
        AcquireImageNode acquire;
        uint imageIndex;
        GBuffer gbuffer;

        RenderPass renderPass;
        List<ImageView> imageViews;
        List<Framebuffer> framebuffers;
        PipelineLayout pipelineLayout;
        Pipeline pipeline;
        CommandPool pool;
        List<CommandBuffer> commandBuffers;
        List<CommandBuffer> submitBuffers;

        public ToneMapNode(Engine engine, AcquireImageNode acquire, GBuffer gbuffer) : base(engine.Graphics.Device, engine.Graphics.GraphicsQueue, VkPipelineStageFlags.ColorAttachmentOutputBit) {
            if (engine == null) throw new ArgumentNullException(nameof(engine));
            if (acquire == null) throw new ArgumentNullException(nameof(acquire));
            if (gbuffer == null) throw new ArgumentNullException(nameof(gbuffer));

            this.engine = engine;
            this.acquire = acquire;
            this.gbuffer = gbuffer;

            AddInput(acquire);

            CommandPoolCreateInfo poolInfo = new CommandPoolCreateInfo();
            poolInfo.queueFamilyIndex = engine.Graphics.GraphicsQueue.FamilyIndex;

            pool = new CommandPool(engine.Graphics.Device, poolInfo);

            commandBuffers = new List<CommandBuffer>();
            imageViews = new List<ImageView>();
            framebuffers = new List<Framebuffer>();

            CreateRenderPass();
            CreatePipeline();
            CreateFramebuffers(engine.Window);
            CreateCommandBuffer();

            gbuffer.OnSizeChanged += (int x, int y) => { Recreate(); };
        }

        void Recreate() {
            CreateFramebuffers(engine.Window);
            CreatePipeline();
            CreateCommandBuffer();
        }

        void CreateRenderPass() {
            var colorAttachment = new AttachmentDescription();
            colorAttachment.format = engine.Window.SwapchainImageFormat;
            colorAttachment.samples = VkSampleCountFlags._1Bit;
            colorAttachment.loadOp = VkAttachmentLoadOp.Load;
            colorAttachment.storeOp = VkAttachmentStoreOp.Store;
            colorAttachment.stencilLoadOp = VkAttachmentLoadOp.DontCare;
            colorAttachment.stencilStoreOp = VkAttachmentStoreOp.DontCare;
            colorAttachment.initialLayout = VkImageLayout.Undefined;
            colorAttachment.finalLayout = VkImageLayout.ColorAttachmentOptimal;

            var colorAttachmentRef = new AttachmentReference();
            colorAttachmentRef.attachment = 0;
            colorAttachmentRef.layout = VkImageLayout.ColorAttachmentOptimal;

            var subpass = new SubpassDescription();
            subpass.pipelineBindPoint = VkPipelineBindPoint.Graphics;
            subpass.colorAttachments = new List<AttachmentReference> { colorAttachmentRef };

            var dependency = new SubpassDependency();
            dependency.srcSubpass = uint.MaxValue;  //VK_SUBPASS_EXTERNAL
            dependency.dstSubpass = 0;
            dependency.srcStageMask = VkPipelineStageFlags.BottomOfPipeBit;
            dependency.srcAccessMask = VkAccessFlags.MemoryReadBit;
            dependency.dstStageMask = VkPipelineStageFlags.ColorAttachmentOutputBit;
            dependency.dstAccessMask = VkAccessFlags.ColorAttachmentReadBit
                                    | VkAccessFlags.ColorAttachmentWriteBit;

            var info = new RenderPassCreateInfo();
            info.attachments = new List<AttachmentDescription> { colorAttachment };
            info.subpasses = new List<SubpassDescription> { subpass };
            info.dependencies = new List<SubpassDependency> { dependency };

            renderPass?.Dispose();
            renderPass = new RenderPass(device, info);
        }

        void CreateFramebuffers(Window window) {
            foreach (var iv in imageViews) iv.Dispose();
            foreach (var fb in framebuffers) fb.Dispose();
            imageViews.Clear();
            framebuffers.Clear();

            for (int i = 0; i < window.SwapchainImages.Count; i++) {
                ImageViewCreateInfo ivInfo = new ImageViewCreateInfo(window.SwapchainImages[i]);
                ivInfo.viewType = VkImageViewType._2d;
                ivInfo.format = window.SwapchainImageFormat;
                ivInfo.subresourceRange.aspectMask = VkImageAspectFlags.ColorBit;
                ivInfo.subresourceRange.baseMipLevel = 0;
                ivInfo.subresourceRange.levelCount = 1;
                ivInfo.subresourceRange.baseArrayLayer = 0;
                ivInfo.subresourceRange.layerCount = 1;

                imageViews.Add(new ImageView(device, ivInfo));

                FramebufferCreateInfo fbInfo = new FramebufferCreateInfo();
                fbInfo.renderPass = renderPass;
                fbInfo.attachments = new List<ImageView> { imageViews[i] };
                fbInfo.height = window.SwapchainExtent.height;
                fbInfo.width = window.SwapchainExtent.width;
                fbInfo.layers = 1;

                framebuffers.Add(new Framebuffer(device, fbInfo));
            }
        }

        ShaderModule CreateShaderModule(Device device, byte[] code) {
            var info = new ShaderModuleCreateInfo(code);
            return new ShaderModule(device, info);
        }

        void CreatePipeline() {
            var vert = CreateShaderModule(device, File.ReadAllBytes("tonemap_vert.spv"));
            var frag = CreateShaderModule(device, File.ReadAllBytes("tonemap_frag.spv"));

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
            colorBlendAttachment.srcColorBlendFactor = VkBlendFactor.One;
            colorBlendAttachment.dstColorBlendFactor = VkBlendFactor.Zero;
            colorBlendAttachment.colorBlendOp = VkBlendOp.Add;
            colorBlendAttachment.srcAlphaBlendFactor = VkBlendFactor.One;
            colorBlendAttachment.dstAlphaBlendFactor = VkBlendFactor.Zero;
            colorBlendAttachment.alphaBlendOp = VkBlendOp.Add;

            var colorBlending = new PipelineColorBlendStateCreateInfo();
            colorBlending.logicOp = VkLogicOp.Copy;
            colorBlending.attachments = new List<PipelineColorBlendAttachmentState> { colorBlendAttachment };

            var pipelineLayoutInfo = new PipelineLayoutCreateInfo();
            pipelineLayoutInfo.setLayouts = new List<DescriptorSetLayout> { gbuffer.LightLayout };

            pipelineLayout?.Dispose();

            pipelineLayout = new PipelineLayout(device, pipelineLayoutInfo);

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

            pipeline = new Pipeline(device, info, null);

            oldPipeline?.Dispose();

            vert.Dispose();
            frag.Dispose();
        }

        void CreateCommandBuffer() {
            if (commandBuffers.Count > 0) pool.Free(commandBuffers);

            commandBuffers = new List<CommandBuffer>(pool.Allocate(VkCommandBufferLevel.Primary, engine.Window.SwapchainImages.Count));

            for (int i = 0; i < commandBuffers.Count; i++) {
                CommandBufferBeginInfo beginInfo = new CommandBufferBeginInfo();
                beginInfo.flags = VkCommandBufferUsageFlags.SimultaneousUseBit;

                RenderPassBeginInfo renderPassInfo = new RenderPassBeginInfo();
                renderPassInfo.renderPass = renderPass;
                renderPassInfo.framebuffer = framebuffers[i];
                renderPassInfo.renderArea.extent = engine.Window.SwapchainExtent;

                commandBuffers[i].Begin(beginInfo);
                commandBuffers[i].BeginRenderPass(renderPassInfo, VkSubpassContents.Inline);

                commandBuffers[i].BindPipeline(VkPipelineBindPoint.Graphics, pipeline);
                commandBuffers[i].BindDescriptorSets(VkPipelineBindPoint.Graphics, pipelineLayout, 0, new List<DescriptorSet> { gbuffer.LightDescriptor });
                commandBuffers[i].Draw(6, 1, 0, 0);

                commandBuffers[i].EndRenderPass();
                commandBuffers[i].End();
            }

            submitBuffers = new List<CommandBuffer> { null };
        }

        public override void PreRender() {
            imageIndex = acquire.ImageIndex;
        }

        public override List<CommandBuffer> GetCommands() {
            submitBuffers[0] = commandBuffers[(int)imageIndex];
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
            foreach (var fb in framebuffers) fb.Dispose();
            foreach (var iv in imageViews) iv.Dispose();
            renderPass.Dispose();
            pool.Dispose();

            base.Dispose(disposing);

            disposed = true;
        }

        ~ToneMapNode() {
            Dispose(false);
        }
    }
}
