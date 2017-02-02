using System;
using System.IO;
using System.Collections.Generic;
using System.Numerics;

using CSGL;
using CSGL.Vulkan;
using Buffer = CSGL.Vulkan.Buffer;

using UnnamedEngine.Core;
using UnnamedEngine.Resources;
using UnnamedEngine.Utilities;
using UnnamedEngine.Rendering;

namespace Test {
    public class TriangleNode : CommandNode, IDisposable {
        bool disposed;
        Engine engine;
        AcquireImageNode acquireImageNode;
        TransferNode transferNode;
        Camera camera;

        RenderGraph renderGraph;
        List<ImageView> imageViews;
        List<Framebuffer> framebuffers;
        CommandPool commandPool;

        List<CommandBuffer> commandBuffers;
        List<CommandBuffer> submitCommands;
        uint index;

        public TriangleNode(Engine engine, AcquireImageNode acquireImageNode, TransferNode transferNode, Camera camera) : base(engine.Graphics.Device, VkPipelineStageFlags.TopOfPipeBit) {
            if (engine == null) throw new ArgumentNullException(nameof(engine));
            if (engine.Window == null) throw new ArgumentNullException(nameof(engine.Window));
            if (acquireImageNode == null) throw new ArgumentNullException(nameof(acquireImageNode));
            if (transferNode == null) throw new ArgumentNullException(nameof(transferNode));

            this.engine = engine;
            this.acquireImageNode = acquireImageNode;
            this.transferNode = transferNode;
            this.camera = camera;
            
            submitCommands = new List<CommandBuffer> { null };

            CreateCommandPool(engine);
            CreateRenderGraph();
            CreateFramebuffers(engine.Window);
            CreateCommandBuffers(engine.Graphics.Device, engine.Window);

            AddInput(transferNode);
            AddInput(acquireImageNode);
        }

        void CreateRenderGraph() {
            renderGraph = new RenderGraph(engine);

            TriangleRenderNode renderNode = new TriangleRenderNode(engine, commandPool, transferNode);

            var colorAttachment = new AttachmentDescription();
            colorAttachment.format = engine.Window.SwapchainImageFormat;
            colorAttachment.samples = VkSampleCountFlags._1Bit;
            colorAttachment.loadOp = VkAttachmentLoadOp.Load;
            colorAttachment.storeOp = VkAttachmentStoreOp.Store;
            colorAttachment.stencilLoadOp = VkAttachmentLoadOp.DontCare;
            colorAttachment.stencilStoreOp = VkAttachmentStoreOp.DontCare;
            colorAttachment.initialLayout = VkImageLayout.Undefined;
            colorAttachment.finalLayout = VkImageLayout.ColorAttachmentOptimal;

            //var colorAttachmentRef = new AttachmentReference();
            //colorAttachmentRef.attachment = 0;
            //colorAttachmentRef.layout = VkImageLayout.ColorAttachmentOptimal;

            //var subpass = new SubpassDescription();
            //subpass.pipelineBindPoint = VkPipelineBindPoint.Graphics;
            //subpass.colorAttachments = new List<AttachmentReference> { colorAttachmentRef };

            var dependency = new SubpassDependency();
            //dependency.srcSubpass = uint.MaxValue;  //VK_SUBPASS_EXTERNAL
            //dependency.dstSubpass = 0;
            dependency.srcStageMask = VkPipelineStageFlags.BottomOfPipeBit;
            dependency.srcAccessMask = VkAccessFlags.MemoryReadBit;
            dependency.dstStageMask = VkPipelineStageFlags.ColorAttachmentOutputBit;
            dependency.dstAccessMask = VkAccessFlags.ColorAttachmentReadBit
                                    | VkAccessFlags.ColorAttachmentWriteBit;



            renderNode.AddColor(colorAttachment, VkImageLayout.ColorAttachmentOptimal);
            renderGraph.AddAttachment(colorAttachment);
            renderGraph.AddDependency(null, renderNode, dependency);
            renderGraph.AddNode(renderNode);

            //var info = new RenderPassCreateInfo();
            //info.attachments = new List<AttachmentDescription> { colorAttachment };
            //info.subpasses = new List<SubpassDescription> { subpass };
            //info.dependencies = new List<SubpassDependency> { dependency };

            renderGraph.Bake();
        }

        void CreateFramebuffers(Window window) {
            imageViews = new List<ImageView>(window.SwapchainImages.Count);
            framebuffers = new List<Framebuffer>(window.SwapchainImages.Count);

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
                fbInfo.renderPass = renderGraph.RenderPass;
                fbInfo.attachments = new List<ImageView> { imageViews[i] };
                fbInfo.height = window.SwapchainExtent.height;
                fbInfo.width = window.SwapchainExtent.width;
                fbInfo.layers = 1;

                framebuffers.Add(new Framebuffer(device, fbInfo));
            }
        }

        void CreateCommandPool(Engine engine) {
            var info = new CommandPoolCreateInfo();
            info.queueFamilyIndex = engine.Graphics.GraphicsQueue.FamilyIndex;

            commandPool = new CommandPool(engine.Graphics.Device, info);
        }

        void CreateCommandBuffers(Device device, Window window) {
            if (commandBuffers != null) {
                commandPool.Free(commandBuffers);
            }

            var info = new CommandBufferAllocateInfo();
            info.level = VkCommandBufferLevel.Primary;
            info.commandBufferCount = (uint)window.SwapchainImages.Count;

            commandBuffers = new List<CommandBuffer>(commandPool.Allocate(info));

            for (int i = 0; i < commandBuffers.Count; i++) {
                var buffer = commandBuffers[i];
                var beginInfo = new CommandBufferBeginInfo();
                beginInfo.flags = VkCommandBufferUsageFlags.SimultaneousUseBit;

                buffer.Begin(beginInfo);

                var renderPassInfo = new RenderPassBeginInfo();
                renderPassInfo.framebuffer = framebuffers[i];
                renderPassInfo.renderArea.extent = window.SwapchainExtent;

                VkClearValue clearColor = new VkClearValue();
                clearColor.color.float32_0 = 0;
                clearColor.color.float32_1 = 0;
                clearColor.color.float32_2 = 0;
                clearColor.color.float32_3 = 1f;

                renderPassInfo.clearValues = new List<VkClearValue> { clearColor };

                renderGraph.Render(renderPassInfo, buffer);
                
                buffer.End();
            }
        }

        public override void PreRender() {
            index = acquireImageNode.ImageIndex;
        }

        public override List<CommandBuffer> GetCommands() {
            submitCommands[0] = commandBuffers[(int)index];
            return submitCommands;
        }

        public new void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected override void Dispose(bool disposing) {
            if (disposed) return;

            commandPool.Dispose();
            foreach (var fb in framebuffers) fb.Dispose();
            foreach (var iv in imageViews) iv.Dispose();
            renderGraph.Dispose();

            disposed = true;
        }

        ~TriangleNode() {
            Dispose(false);
        }
    }
}
