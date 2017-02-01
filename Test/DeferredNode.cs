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

        public DeferredNode(Engine engine, GBuffer gbuffer) : base(engine.Graphics.Device, VkPipelineStageFlags.ColorAttachmentOutputBit) {
            if (engine == null) throw new ArgumentNullException(nameof(engine));
            if (gbuffer == null) throw new ArgumentNullException(nameof(gbuffer));

            this.engine = engine;
            this.gbuffer = gbuffer;

            gbuffer.OnSizeChanged += CreateFramebuffer;

            CreateRenderpass();
            CreateFramebuffer(gbuffer.Width, gbuffer.Height);
        }

        void CreateRenderpass() {
            VkAttachmentDescription albedo = new VkAttachmentDescription();
            albedo.format = gbuffer.AlbedoFormat;
            albedo.samples = VkSampleCountFlags._1Bit;
            albedo.loadOp = VkAttachmentLoadOp.Clear;
            albedo.storeOp = VkAttachmentStoreOp.DontCare;
            albedo.initialLayout = VkImageLayout.Undefined;
            albedo.finalLayout = VkImageLayout.ColorAttachmentOptimal;

            VkAttachmentDescription norm = new VkAttachmentDescription();
            norm.format = gbuffer.NormFormat;
            norm.samples = VkSampleCountFlags._1Bit;
            norm.loadOp = VkAttachmentLoadOp.Clear;
            norm.storeOp = VkAttachmentStoreOp.DontCare;
            norm.initialLayout = VkImageLayout.Undefined;
            norm.finalLayout = VkImageLayout.ColorAttachmentOptimal;

            VkAttachmentDescription depth = new VkAttachmentDescription();
            depth.format = gbuffer.DepthFormat;
            depth.samples = VkSampleCountFlags._1Bit;
            depth.loadOp = VkAttachmentLoadOp.Clear;
            depth.storeOp = VkAttachmentStoreOp.Store;
            depth.stencilLoadOp = VkAttachmentLoadOp.Clear;
            depth.stencilStoreOp = VkAttachmentStoreOp.Store;
            depth.initialLayout = VkImageLayout.Undefined;
            depth.finalLayout = VkImageLayout.DepthStencilAttachmentOptimal;

            VkAttachmentDescription light = new VkAttachmentDescription();
            light.format = gbuffer.LightFormat;
            light.samples = VkSampleCountFlags._1Bit;
            light.loadOp = VkAttachmentLoadOp.Clear;
            light.storeOp = VkAttachmentStoreOp.Store;
            light.initialLayout = VkImageLayout.Undefined;
            light.finalLayout = VkImageLayout.ColorAttachmentOptimal;

            SubpassDescription opaque = new SubpassDescription();
            opaque.PipelineBindPoint = VkPipelineBindPoint.Graphics;
            opaque.ColorAttachments = new List<VkAttachmentReference> {
                new VkAttachmentReference { attachment = 0, layout = VkImageLayout.ColorAttachmentOptimal },
                new VkAttachmentReference { attachment = 1, layout = VkImageLayout.ColorAttachmentOptimal },
                new VkAttachmentReference { attachment = 3, layout = VkImageLayout.ColorAttachmentOptimal }
            };
            opaque.DepthStencilAttachment = new VkAttachmentReference { attachment = 2, layout = VkImageLayout.DepthStencilAttachmentOptimal };

            SubpassDescription lighting = new SubpassDescription();
            lighting.PipelineBindPoint = VkPipelineBindPoint.Graphics;
            lighting.InputAttachments = new List<VkAttachmentReference> {
                new VkAttachmentReference { attachment = 0, layout = VkImageLayout.ShaderReadOnlyOptimal },
                new VkAttachmentReference { attachment = 1, layout = VkImageLayout.ShaderReadOnlyOptimal },
                new VkAttachmentReference { attachment = 3, layout = VkImageLayout.General }
            };
            lighting.ColorAttachments = new List<VkAttachmentReference> {
                new VkAttachmentReference { attachment = 3, layout = VkImageLayout.General }
            };
            lighting.PreserveAttachments = new List<uint> { 2 };

            VkSubpassDependency toOpaque = new VkSubpassDependency();
            toOpaque.srcSubpass = uint.MaxValue;
            toOpaque.dstSubpass = 0;
            toOpaque.srcStageMask = VkPipelineStageFlags.TopOfPipeBit;
            toOpaque.dstStageMask = VkPipelineStageFlags.ColorAttachmentOutputBit;
            toOpaque.srcAccessMask = VkAccessFlags.None;
            toOpaque.dstAccessMask = VkAccessFlags.ColorAttachmentWriteBit
                | VkAccessFlags.DepthStencilAttachmentWriteBit
                | VkAccessFlags.InputAttachmentReadBit;

            VkSubpassDependency opaqueToLighting = new VkSubpassDependency();
            opaqueToLighting.srcSubpass = 0;
            opaqueToLighting.dstSubpass = 1;
            opaqueToLighting.srcStageMask = VkPipelineStageFlags.ColorAttachmentOutputBit;
            opaqueToLighting.dstStageMask = VkPipelineStageFlags.ColorAttachmentOutputBit;
            opaqueToLighting.srcAccessMask = VkAccessFlags.ColorAttachmentReadBit
                | VkAccessFlags.InputAttachmentReadBit;
            opaqueToLighting.dstAccessMask = VkAccessFlags.ColorAttachmentWriteBit;

            RenderPassCreateInfo info = new RenderPassCreateInfo();
            info.attachments = new List<VkAttachmentDescription> { albedo, norm, depth, light };
            info.subpasses = new List<SubpassDescription> { opaque, lighting };
            info.dependencies = new List<VkSubpassDependency> { toOpaque, opaqueToLighting, };

            renderPass?.Dispose();
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

        public override List<CommandBuffer> GetCommands() {
            return null;
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

            gbuffer.OnSizeChanged -= CreateFramebuffer;

            disposed = true;
        }
    }
}
