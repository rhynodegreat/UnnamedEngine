using System;
using System.Collections.Generic;

using CSGL.Vulkan;

using UnnamedEngine.Core;
using UnnamedEngine.Rendering;

namespace Test {
    public class Deferred : CommandNode, IDisposable {
        bool disposed;
        Engine engine;

        CommandPool pool;
        CommandBuffer commandBuffer;

        public GBuffer GBuffer { get; private set; }
        public RenderGraph RenderGraph { get; private set; }
        public Framebuffer Framebuffer { get; private set; }
        public OpaqueNode Opaque { get; private set; }
        public LightingNode Lighting { get; private set; }

        public event Action OnFramebufferChanged = delegate { };

        public Deferred(Engine engine, GBuffer gbuffer) : base(engine.Graphics.Device) {
            if (engine == null) throw new ArgumentNullException(nameof(engine));
            if (gbuffer == null) throw new ArgumentNullException(nameof(gbuffer));

            this.engine = engine;
            GBuffer = gbuffer;

            EventStage = VkPipelineStageFlags.VertexInputBit | VkPipelineStageFlags.VertexShaderBit | VkPipelineStageFlags.FragmentShaderBit | VkPipelineStageFlags.ColorAttachmentOutputBit;
            SrcStage = VkPipelineStageFlags.TopOfPipeBit;
            DstStage = VkPipelineStageFlags.ColorAttachmentOutputBit;

            gbuffer.OnSizeChanged += RecreateFramebuffer;

            CommandPoolCreateInfo poolInfo = new CommandPoolCreateInfo();
            poolInfo.queueFamilyIndex = engine.Graphics.GraphicsQueue.FamilyIndex;
            poolInfo.flags = VkCommandPoolCreateFlags.ResetCommandBufferBit;

            pool = new CommandPool(engine.Graphics.Device, poolInfo);

            CreateRenderpass();
            CreateCommandBuffer();
            CreateFramebuffer(gbuffer.Width, gbuffer.Height);
        }

        void RecreateFramebuffer(int width, int height) {
            CreateFramebuffer(width, height);
            OnFramebufferChanged();
        }

        void CreateRenderpass() {
            RenderGraph = new RenderGraph(engine);

            AttachmentDescription albedo = new AttachmentDescription();
            albedo.format = GBuffer.AlbedoFormat;
            albedo.samples = VkSampleCountFlags._1Bit;
            albedo.loadOp = VkAttachmentLoadOp.Clear;
            albedo.storeOp = VkAttachmentStoreOp.DontCare;
            albedo.initialLayout = VkImageLayout.Undefined;
            albedo.finalLayout = VkImageLayout.ShaderReadOnlyOptimal;

            AttachmentDescription norm = new AttachmentDescription();
            norm.format = GBuffer.NormFormat;
            norm.samples = VkSampleCountFlags._1Bit;
            norm.loadOp = VkAttachmentLoadOp.Clear;
            norm.storeOp = VkAttachmentStoreOp.DontCare;
            norm.initialLayout = VkImageLayout.Undefined;
            norm.finalLayout = VkImageLayout.ShaderReadOnlyOptimal;

            AttachmentDescription depth = new AttachmentDescription();
            depth.format = GBuffer.DepthFormat;
            depth.samples = VkSampleCountFlags._1Bit;
            depth.loadOp = VkAttachmentLoadOp.Clear;
            depth.storeOp = VkAttachmentStoreOp.Store;
            depth.stencilLoadOp = VkAttachmentLoadOp.Clear;
            depth.stencilStoreOp = VkAttachmentStoreOp.Store;
            depth.initialLayout = VkImageLayout.Undefined;
            depth.finalLayout = VkImageLayout.DepthStencilAttachmentOptimal;

            AttachmentDescription light = new AttachmentDescription();
            light.format = GBuffer.LightFormat;
            light.samples = VkSampleCountFlags._1Bit;
            light.loadOp = VkAttachmentLoadOp.Clear;
            light.storeOp = VkAttachmentStoreOp.Store;
            light.initialLayout = VkImageLayout.Undefined;
            light.finalLayout = VkImageLayout.ShaderReadOnlyOptimal;

            RenderGraph.AddAttachment(albedo);
            RenderGraph.AddAttachment(norm);
            RenderGraph.AddAttachment(depth);
            RenderGraph.AddAttachment(light);

            Opaque = new OpaqueNode(pool);
            Opaque.AddColor(albedo, VkImageLayout.ColorAttachmentOptimal);
            Opaque.AddColor(norm, VkImageLayout.ColorAttachmentOptimal);
            Opaque.AddColor(light, VkImageLayout.ColorAttachmentOptimal);
            Opaque.DepthStencil = depth;
            Opaque.DepthStencilLayout = VkImageLayout.DepthStencilAttachmentOptimal;

            RenderGraph.AddNode(Opaque);

            Lighting = new LightingNode(engine, GBuffer, pool);
            Lighting.AddInput(albedo, VkImageLayout.ShaderReadOnlyOptimal);
            Lighting.AddInput(norm, VkImageLayout.ShaderReadOnlyOptimal);
            Lighting.AddInput(depth, VkImageLayout.DepthStencilReadOnlyOptimal);
            Lighting.AddColor(light, VkImageLayout.ColorAttachmentOptimal);

            RenderGraph.AddNode(Lighting);

            SubpassDependency toOpaque = new SubpassDependency();
            toOpaque.srcStageMask = VkPipelineStageFlags.TopOfPipeBit;
            toOpaque.dstStageMask = VkPipelineStageFlags.ColorAttachmentOutputBit;
            toOpaque.srcAccessMask = VkAccessFlags.None;
            toOpaque.dstAccessMask = VkAccessFlags.ColorAttachmentWriteBit
                | VkAccessFlags.DepthStencilAttachmentWriteBit;

            SubpassDependency opaqueToLighting = new SubpassDependency();
            opaqueToLighting.srcStageMask = VkPipelineStageFlags.ColorAttachmentOutputBit;
            opaqueToLighting.dstStageMask = VkPipelineStageFlags.ColorAttachmentOutputBit;
            opaqueToLighting.srcAccessMask = VkAccessFlags.ColorAttachmentReadBit
                | VkAccessFlags.InputAttachmentReadBit;
            opaqueToLighting.dstAccessMask = VkAccessFlags.ColorAttachmentWriteBit;

            RenderGraph.AddDependency(null, Opaque, toOpaque);
            RenderGraph.AddDependency(Opaque, Lighting, opaqueToLighting);

            RenderGraph.Bake();
        }

        void CreateFramebuffer(int width, int height) {
            FramebufferCreateInfo info = new FramebufferCreateInfo();
            info.attachments = new List<ImageView> { GBuffer.AlbedoView, GBuffer.NormView, GBuffer.DepthView, GBuffer.LightView };
            info.width = (uint)width;
            info.height = (uint)height;
            info.layers = 1;
            info.renderPass = RenderGraph.RenderPass;

            Framebuffer?.Dispose();
            Framebuffer = new Framebuffer(engine.Graphics.Device, info);
        }

        void CreateCommandBuffer() {
            commandBuffer = pool.Allocate(VkCommandBufferLevel.Primary);
        }

        void RecordCommands() {
            commandBuffer?.Reset(VkCommandBufferResetFlags.None);

            CommandBufferBeginInfo beginInfo = new CommandBufferBeginInfo();
            beginInfo.flags = VkCommandBufferUsageFlags.OneTimeSubmitBit;

            RenderPassBeginInfo renderPassInfo = new RenderPassBeginInfo();
            renderPassInfo.renderPass = RenderGraph.RenderPass;
            renderPassInfo.framebuffer = Framebuffer;
            renderPassInfo.clearValues = new List<VkClearValue> {
                new VkClearValue {
                    color = new VkClearColorValue { //albedo
                        float32_0 = 0,
                        float32_1 = 0,
                        float32_2 = 0,
                        float32_3 = 0
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
            renderPassInfo.renderArea.extent.width = (uint)GBuffer.Width;
            renderPassInfo.renderArea.extent.height = (uint)GBuffer.Height;

            commandBuffer.Begin(beginInfo);

            WaitEvents(commandBuffer);

            RenderGraph.Render(renderPassInfo, commandBuffer);

            SetEvents(commandBuffer);

            commandBuffer.End();
        }

        public override CommandBuffer GetCommands() {
            RecordCommands();
            return commandBuffer;
        }

        public new void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected override void Dispose(bool disposing) {
            if (disposed) return;

            base.Dispose(disposing);

            Framebuffer.Dispose();
            RenderGraph.Dispose();
            pool.Dispose();

            GBuffer.OnSizeChanged -= RecreateFramebuffer;

            disposed = true;
        }
    }
}
