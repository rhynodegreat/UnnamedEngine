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

        RenderGraph renderGraph;
        OpaqueNode opaque;
        LightingNode lighting;
        CommandPool pool;
        CommandBuffer commandBuffer;
        List<CommandBuffer> submitBuffers;

        public Framebuffer Framebuffer { get; private set; }

        public DeferredNode(Engine engine, GBuffer gbuffer) : base(engine.Graphics.Device, VkPipelineStageFlags.ColorAttachmentOutputBit) {
            if (engine == null) throw new ArgumentNullException(nameof(engine));
            if (gbuffer == null) throw new ArgumentNullException(nameof(gbuffer));

            this.engine = engine;
            this.gbuffer = gbuffer;

            gbuffer.OnSizeChanged += CreateFramebuffer;

            CommandPoolCreateInfo poolInfo = new CommandPoolCreateInfo();
            poolInfo.queueFamilyIndex = engine.Graphics.GraphicsQueue.FamilyIndex;

            pool = new CommandPool(engine.Graphics.Device, poolInfo);

            CreateRenderpass();
            CreateFramebuffer(gbuffer.Width, gbuffer.Height);
            CreateCommandBuffer();
        }

        void CreateRenderpass() {
            renderGraph = new RenderGraph(engine);

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

            renderGraph.AddAttachment(albedo);
            renderGraph.AddAttachment(norm);
            renderGraph.AddAttachment(depth);
            renderGraph.AddAttachment(light);

            opaque = new OpaqueNode(pool);
            opaque.AddColor(albedo, VkImageLayout.ColorAttachmentOptimal);
            opaque.AddColor(norm, VkImageLayout.ColorAttachmentOptimal);
            opaque.AddColor(light, VkImageLayout.ColorAttachmentOptimal);
            opaque.DepthStencil = depth;
            opaque.DepthStencilLayout = VkImageLayout.DepthStencilAttachmentOptimal;

            renderGraph.AddNode(opaque);

            lighting = new LightingNode(engine, gbuffer, pool);
            lighting.AddInput(albedo, VkImageLayout.ShaderReadOnlyOptimal);
            lighting.AddInput(norm, VkImageLayout.ShaderReadOnlyOptimal);
            lighting.AddColor(light, VkImageLayout.ColorAttachmentOptimal);
            lighting.DepthStencil = depth;
            lighting.DepthStencilLayout = VkImageLayout.DepthStencilReadOnlyOptimal;

            renderGraph.AddNode(lighting);

            SubpassDependency toOpaque = new SubpassDependency();
            toOpaque.srcStageMask = VkPipelineStageFlags.TopOfPipeBit;
            toOpaque.dstStageMask = VkPipelineStageFlags.ColorAttachmentOutputBit;
            toOpaque.srcAccessMask = VkAccessFlags.None;
            toOpaque.dstAccessMask = VkAccessFlags.ColorAttachmentWriteBit
                | VkAccessFlags.DepthStencilAttachmentWriteBit
                | VkAccessFlags.InputAttachmentReadBit;

            SubpassDependency opaqueToLighting = new SubpassDependency();
            opaqueToLighting.srcStageMask = VkPipelineStageFlags.ColorAttachmentOutputBit;
            opaqueToLighting.dstStageMask = VkPipelineStageFlags.ColorAttachmentOutputBit;
            opaqueToLighting.srcAccessMask = VkAccessFlags.ColorAttachmentReadBit
                | VkAccessFlags.InputAttachmentReadBit;
            opaqueToLighting.dstAccessMask = VkAccessFlags.ColorAttachmentWriteBit;

            renderGraph.AddDependency(null, opaque, toOpaque);
            renderGraph.AddDependency(opaque, lighting, opaqueToLighting);

            renderGraph.Bake();
        }

        void CreateFramebuffer(int width, int height) {
            FramebufferCreateInfo info = new FramebufferCreateInfo();
            info.attachments = new List<ImageView> { gbuffer.AlbedoView, gbuffer.NormView, gbuffer.DepthView, gbuffer.LightView };
            info.width = (uint)width;
            info.height = (uint)height;
            info.layers = 1;
            info.renderPass = renderGraph.RenderPass;

            Framebuffer?.Dispose();
            Framebuffer = new Framebuffer(engine.Graphics.Device, info);

            lighting.Init(Framebuffer);
            opaque.Init(Framebuffer);
        }

        void CreateCommandBuffer() {
            commandBuffer = pool.Allocate(VkCommandBufferLevel.Primary);

            submitBuffers = new List<CommandBuffer> { commandBuffer };

            CommandBufferBeginInfo beginInfo = new CommandBufferBeginInfo();
            RenderPassBeginInfo renderPassInfo = new RenderPassBeginInfo();
            renderPassInfo.renderPass = renderGraph.RenderPass;
            renderPassInfo.framebuffer = Framebuffer;
            renderPassInfo.clearValues = new List<VkClearValue> {
                new VkClearValue {
                    color = new VkClearColorValue { //albedo
                        uint32_0 = 0,
                        uint32_1 = 0,
                        uint32_2 = 0,
                        uint32_3 = 255
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
                        depth = 1,
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

            renderGraph.Render(renderPassInfo, commandBuffer);

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

            Framebuffer.Dispose();
            renderGraph.Dispose();
            pool.Dispose();

            gbuffer.OnSizeChanged -= CreateFramebuffer;

            disposed = true;
        }
    }
}
