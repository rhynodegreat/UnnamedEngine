using System;
using System.Collections.Generic;

using CSGL.Vulkan;

using UnnamedEngine.Core;
using UnnamedEngine.Rendering;
using UnnamedEngine.UI;

namespace Test {
    public class FullscreenUI : CommandNode {
        bool disposed;

        Engine engine;
        Window window;
        Renderer renderer;

        CommandPool pool;
        CommandBuffer commandBuffer;
        CommandBufferBeginInfo beginInfo;
        RenderPassBeginInfo renderPassBeginInfo;
        List<ImageView> imageViews;
        List<Framebuffer> framebuffers;

        public Screen Screen { get; private set; }
        public RenderPass RenderPass { get; private set; }

        public FullscreenUI(Engine engine, SubmitNode submitNode, Camera camera, Renderer renderer) : base(engine.Graphics.Device, VkPipelineStageFlags.FragmentShaderBit, VkPipelineStageFlags.ColorAttachmentOutputBit) {
            if (engine == null) throw new ArgumentNullException(nameof(engine));
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            if (engine.Window == null) throw new ArgumentNullException(nameof(window));
            if (renderer == null) throw new ArgumentNullException(nameof(renderer));

            this.engine = engine;
            window = engine.Window;
            this.renderer = renderer;

            imageViews = new List<ImageView>();
            framebuffers = new List<Framebuffer>();

            Screen = new Screen(engine, submitNode, camera, window.Width, window.Height);
            window.OnSizeChanged += Recreate;

            CreateRenderPass();
            CreateFramebuffers();
            CreateCommandPool();

            beginInfo = new CommandBufferBeginInfo();
            beginInfo.flags = VkCommandBufferUsageFlags.OneTimeSubmitBit;

            renderPassBeginInfo = new RenderPassBeginInfo();
            renderPassBeginInfo.clearValues = new List<VkClearValue> {
                new VkClearValue(), //not used
                new VkClearValue {
                    depthStencil = new VkClearDepthStencilValue {
                        depth = 1,
                        stencil = 0
                    }
                }
            };
            renderPassBeginInfo.renderArea.extent.width = (uint)window.Width;
            renderPassBeginInfo.renderArea.extent.height = (uint)window.Height;
            renderPassBeginInfo.renderPass = RenderPass;
        }

        void Recreate(int width, int height) {
            Screen.Recreate(width, height);
            CreateFramebuffers();
        }

        void CreateRenderPass() {
            RenderPassCreateInfo info = new RenderPassCreateInfo();
            info.attachments = new List<AttachmentDescription> {
                new AttachmentDescription {
                    format = window.SwapchainImageFormat,
                    samples = VkSampleCountFlags._1Bit,
                    loadOp = VkAttachmentLoadOp.Load,
                    storeOp = VkAttachmentStoreOp.Store,
                    initialLayout = VkImageLayout.ColorAttachmentOptimal,
                    finalLayout = VkImageLayout.PresentSrcKhr
                },
                new AttachmentDescription {
                    format = Screen.StencilFormat,
                    samples = VkSampleCountFlags._1Bit,
                    loadOp = VkAttachmentLoadOp.Clear,
                    storeOp = VkAttachmentStoreOp.DontCare,
                    stencilLoadOp = VkAttachmentLoadOp.Clear,
                    stencilStoreOp = VkAttachmentStoreOp.DontCare,
                    initialLayout = VkImageLayout.DepthStencilAttachmentOptimal,
                    finalLayout = VkImageLayout.DepthStencilAttachmentOptimal
                }
            };
            info.subpasses = new List<SubpassDescription> {
                new SubpassDescription {
                    pipelineBindPoint = VkPipelineBindPoint.Graphics,
                    colorAttachments = new List<AttachmentReference> {
                        new AttachmentReference {
                            attachment = 0,
                            layout = VkImageLayout.ColorAttachmentOptimal
                        }
                    },
                    depthStencilAttachment = new AttachmentReference {
                        attachment = 1,
                        layout = VkImageLayout.DepthStencilAttachmentOptimal
                    }
                }
            };

            RenderPass = new RenderPass(engine.Graphics.Device, info);
        }

        void CreateFramebuffers() {
            foreach (var iv in imageViews) iv.Dispose();
            foreach (var fb in framebuffers) fb.Dispose();
            imageViews.Clear();
            framebuffers.Clear();

            for (int i = 0; i < window.SwapchainImages.Count; i++) {
                ImageViewCreateInfo viewInfo = new ImageViewCreateInfo();
                viewInfo.format = window.SwapchainImageFormat;
                viewInfo.image = window.SwapchainImages[i];
                viewInfo.viewType = VkImageViewType._2d;
                viewInfo.subresourceRange.aspectMask = VkImageAspectFlags.ColorBit;
                viewInfo.subresourceRange.baseArrayLayer = 0;
                viewInfo.subresourceRange.layerCount = 1;
                viewInfo.subresourceRange.baseMipLevel = 0;
                viewInfo.subresourceRange.levelCount = 1;

                ImageView iv = new ImageView(engine.Graphics.Device, viewInfo);
                imageViews.Add(iv);

                FramebufferCreateInfo info = new FramebufferCreateInfo();
                info.renderPass = RenderPass;
                info.attachments = new List<ImageView> {
                    iv,
                    Screen.StencilView
                };
                info.layers = 1;
                info.width = (uint)window.Width;
                info.height = (uint)window.Height;

                Framebuffer fb = new Framebuffer(engine.Graphics.Device, info);
                framebuffers.Add(fb);
            }
        }

        void CreateCommandPool() {
            CommandPoolCreateInfo info = new CommandPoolCreateInfo();
            info.flags = VkCommandPoolCreateFlags.ResetCommandBufferBit;
            info.queueFamilyIndex = engine.Graphics.GraphicsQueue.FamilyIndex;

            pool = new CommandPool(engine.Graphics.Device, info);

            commandBuffer = pool.Allocate(VkCommandBufferLevel.Primary);
        }

        public override CommandBuffer GetCommands() {
            commandBuffer.Reset(VkCommandBufferResetFlags.None);
            commandBuffer.Begin(beginInfo);

            renderPassBeginInfo.framebuffer = framebuffers[(int)renderer.ImageIndex];
            commandBuffer.BeginRenderPass(renderPassBeginInfo, VkSubpassContents.Inline);

            Screen.Render(commandBuffer);

            commandBuffer.EndRenderPass();
            commandBuffer.End();

            return commandBuffer;
        }

        public new void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected override void Dispose(bool disposing) {
            if (disposed) return;

            base.Dispose(disposing);

            window.OnSizeChanged -= Recreate;
            Screen.Dispose();

            foreach (var iv in imageViews) iv.Dispose();
            foreach (var fb in framebuffers) fb.Dispose();

            RenderPass.Dispose();
            pool.Dispose();

            disposed = true;
        }

        ~FullscreenUI() {
            Dispose(false);
        }
    }
}
