using System;
using System.Collections.Generic;

using CSGL.Vulkan;

using UnnamedEngine.Core;
using UnnamedEngine.Rendering;

namespace Test {
    public class DeferredNode : CommandNode, IDisposable {
        bool disposed;
        Engine engine;
        GBuffer gbuffer;

        RenderPass renderPass;
        Framebuffer framebuffer;
        Pipeline lightingPipeline;
        CommandPool pool;
        CommandBuffer commandBuffer;
        List<CommandBuffer> submitBuffers;

        public DeferredNode(Engine engine, GBuffer gbuffer) : base(engine.Graphics.Device, VkPipelineStageFlags.ColorAttachmentOutputBit) {
            if (engine == null) throw new ArgumentNullException(nameof(engine));
            if (gbuffer == null) throw new ArgumentNullException(nameof(gbuffer));

            this.engine = engine;
            this.gbuffer = gbuffer;

            gbuffer.OnSizeChanged += CreateFramebuffer;

            CreateRenderpass();
            CreateFramebuffer(gbuffer.Width, gbuffer.Height);
            CreateCommandBuffer();
            CreateLightingPipeline();
        }

        void CreateRenderpass() {
            AttachmentDescription albedo = new AttachmentDescription();
            albedo.format = gbuffer.AlbedoFormat;
            albedo.samples = VkSampleCountFlags._1Bit;
            albedo.loadOp = VkAttachmentLoadOp.Clear;
            albedo.storeOp = VkAttachmentStoreOp.DontCare;
            albedo.initialLayout = VkImageLayout.Undefined;
            albedo.finalLayout = VkImageLayout.ShaderReadOnlyOptimal;

            AttachmentDescription norm = new AttachmentDescription();
            norm.format = gbuffer.NormFormat;
            norm.samples = VkSampleCountFlags._1Bit;
            norm.loadOp = VkAttachmentLoadOp.Clear;
            norm.storeOp = VkAttachmentStoreOp.DontCare;
            norm.initialLayout = VkImageLayout.Undefined;
            norm.finalLayout = VkImageLayout.ShaderReadOnlyOptimal;

            AttachmentDescription depth = new AttachmentDescription();
            depth.format = gbuffer.DepthFormat;
            depth.samples = VkSampleCountFlags._1Bit;
            depth.loadOp = VkAttachmentLoadOp.Clear;
            depth.storeOp = VkAttachmentStoreOp.Store;
            depth.stencilLoadOp = VkAttachmentLoadOp.Clear;
            depth.stencilStoreOp = VkAttachmentStoreOp.Store;
            depth.initialLayout = VkImageLayout.Undefined;
            depth.finalLayout = VkImageLayout.DepthStencilAttachmentOptimal;

            AttachmentDescription light = new AttachmentDescription();
            light.format = gbuffer.LightFormat;
            light.samples = VkSampleCountFlags._1Bit;
            light.loadOp = VkAttachmentLoadOp.Clear;
            light.storeOp = VkAttachmentStoreOp.Store;
            light.initialLayout = VkImageLayout.Undefined;
            light.finalLayout = VkImageLayout.ShaderReadOnlyOptimal;

            SubpassDescription opaque = new SubpassDescription();
            opaque.pipelineBindPoint = VkPipelineBindPoint.Graphics;
            opaque.colorAttachments = new List<AttachmentReference> {
                new AttachmentReference { attachment = 0, layout = VkImageLayout.ColorAttachmentOptimal },
                new AttachmentReference { attachment = 1, layout = VkImageLayout.ColorAttachmentOptimal },
                new AttachmentReference { attachment = 3, layout = VkImageLayout.ColorAttachmentOptimal }
            };
            opaque.depthStencilAttachment = new AttachmentReference { attachment = 2, layout = VkImageLayout.DepthStencilAttachmentOptimal };

            SubpassDescription lighting = new SubpassDescription();
            lighting.pipelineBindPoint = VkPipelineBindPoint.Graphics;
            lighting.inputAttachments = new List<AttachmentReference> {
                new AttachmentReference { attachment = 0, layout = VkImageLayout.ShaderReadOnlyOptimal },
                new AttachmentReference { attachment = 1, layout = VkImageLayout.ShaderReadOnlyOptimal },
                new AttachmentReference { attachment = 3, layout = VkImageLayout.General }
            };
            lighting.colorAttachments = new List<AttachmentReference> {
                new AttachmentReference { attachment = 3, layout = VkImageLayout.General }
            };
            lighting.depthStencilAttachment = new AttachmentReference { attachment = 2, layout = VkImageLayout.DepthStencilReadOnlyOptimal };

            SubpassDependency toOpaque = new SubpassDependency();
            toOpaque.srcSubpass = uint.MaxValue;
            toOpaque.dstSubpass = 0;
            toOpaque.srcStageMask = VkPipelineStageFlags.TopOfPipeBit;
            toOpaque.dstStageMask = VkPipelineStageFlags.ColorAttachmentOutputBit;
            toOpaque.srcAccessMask = VkAccessFlags.None;
            toOpaque.dstAccessMask = VkAccessFlags.ColorAttachmentWriteBit
                | VkAccessFlags.DepthStencilAttachmentWriteBit
                | VkAccessFlags.InputAttachmentReadBit;

            SubpassDependency opaqueToLighting = new SubpassDependency();
            opaqueToLighting.srcSubpass = 0;
            opaqueToLighting.dstSubpass = 1;
            opaqueToLighting.srcStageMask = VkPipelineStageFlags.ColorAttachmentOutputBit;
            opaqueToLighting.dstStageMask = VkPipelineStageFlags.ColorAttachmentOutputBit;
            opaqueToLighting.srcAccessMask = VkAccessFlags.ColorAttachmentReadBit
                | VkAccessFlags.InputAttachmentReadBit;
            opaqueToLighting.dstAccessMask = VkAccessFlags.ColorAttachmentWriteBit;

            RenderPassCreateInfo info = new RenderPassCreateInfo();
            info.attachments = new List<AttachmentDescription> { albedo, norm, depth, light };
            info.subpasses = new List<SubpassDescription> { opaque, lighting };
            info.dependencies = new List<SubpassDependency> { toOpaque, opaqueToLighting, };
            
            renderPass = new RenderPass(engine.Graphics.Device, info);
        }

        void CreateFramebuffer(int width, int height) {
            FramebufferCreateInfo info = new FramebufferCreateInfo();
            info.attachments = new List<ImageView> { gbuffer.AlbedoView, gbuffer.NormView, gbuffer.DepthView, gbuffer.LightView };
            info.width = (uint)width;
            info.height = (uint)height;
            info.layers = 1;
            info.renderPass = renderPass;

            framebuffer?.Dispose();
            framebuffer = new Framebuffer(engine.Graphics.Device, info);
        }

        void CreateLightingPipeline() {

        }

        void CreateCommandBuffer() {
            CommandPoolCreateInfo poolInfo = new CommandPoolCreateInfo();
            poolInfo.queueFamilyIndex = engine.Graphics.GraphicsQueue.FamilyIndex;

            pool = new CommandPool(engine.Graphics.Device, poolInfo);

            commandBuffer = pool.Allocate(VkCommandBufferLevel.Primary);

            submitBuffers = new List<CommandBuffer> { commandBuffer };

            CommandBufferBeginInfo beginInfo = new CommandBufferBeginInfo();
            RenderPassBeginInfo renderPassInfo = new RenderPassBeginInfo();
            renderPassInfo.renderPass = renderPass;
            renderPassInfo.framebuffer = framebuffer;
            renderPassInfo.clearValues = new List<VkClearValue> {
                new VkClearValue {
                    color = new VkClearColorValue { //albedo
                        uint32_0 = 255,
                        uint32_1 = 0,
                        uint32_2 = 0,
                        uint32_3 = 0
                    }
                },
                new VkClearValue {
                    color = new VkClearColorValue { //norm
                        float32_0 = 0,
                        float32_1 = 0,
                        float32_2 = 0,
                        float32_3 = 0
                    }
                },
                new VkClearValue {
                    depthStencil = new VkClearDepthStencilValue {   //depth
                        depth = 0,
                        stencil = 0
                    }
                },
                new VkClearValue {
                    color = new VkClearColorValue { //light
                        float32_0 = 0,
                        float32_1 = 0,
                        float32_2 = 0,
                        float32_3 = 0
                    }
                },
            };
            renderPassInfo.renderArea.offset.x = 0;
            renderPassInfo.renderArea.offset.y = 0;
            renderPassInfo.renderArea.extent.width = (uint)gbuffer.Width;
            renderPassInfo.renderArea.extent.height = (uint)gbuffer.Height;

            commandBuffer.Begin(beginInfo);
            commandBuffer.BeginRenderPass(renderPassInfo, VkSubpassContents.Inline);

            commandBuffer.NextSubpass(VkSubpassContents.Inline);

            commandBuffer.EndRenderPass();
            commandBuffer.End();
            
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
            
            base.Dispose(disposing);

            framebuffer.Dispose();
            renderPass.Dispose();
            pool.Dispose();

            gbuffer.OnSizeChanged -= CreateFramebuffer;

            disposed = true;
        }
    }
}
