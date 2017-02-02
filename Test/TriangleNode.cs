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

        RenderPass renderPass;
        List<ImageView> imageViews;
        List<Framebuffer> framebuffers;
        PipelineLayout pipelineLayout;
        Pipeline pipeline;
        CommandPool commandPool;
        Buffer vertexBuffer;
        VkaAllocation vertexAllocation;

        List<CommandBuffer> commandBuffers;
        List<CommandBuffer> submitCommands;
        uint index;

        Vertex[] vertices = {
            new Vertex(new Vector3(0, 1, 0), new Vector3(1, 0, 0)),
            new Vertex(new Vector3(1, -1, 0), new Vector3(0, 1, 0)),
            new Vertex(new Vector3(-1, -1, 0), new Vector3(0, 0, 1)),
        };

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

            CreateRenderPass(engine.Graphics.Device, engine.Window);
            CreateFramebuffers(engine.Window);
            CreatePipeline(engine.Graphics.Device, engine.Window);
            CreateVertexBuffer(engine.Graphics);
            CreateCommandPool(engine);
            CreateCommandBuffers(engine.Graphics.Device, engine.Window);

            AddInput(transferNode);
            AddInput(acquireImageNode);
        }

        void CreateRenderPass(Device device, Window window) {
            var colorAttachment = new AttachmentDescription();
            colorAttachment.format = window.SwapchainImageFormat;
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
                fbInfo.renderPass = renderPass;
                fbInfo.attachments = new List<ImageView> { imageViews[i] };
                fbInfo.height = window.SwapchainExtent.height;
                fbInfo.width = window.SwapchainExtent.width;
                fbInfo.layers = 1;

                framebuffers.Add(new Framebuffer(device, fbInfo));
            }
        }

        void CreateVertexBuffer(Graphics renderer) {
            BufferCreateInfo info = new BufferCreateInfo();
            info.sharingMode = VkSharingMode.Exclusive;
            info.size = (uint)Interop.SizeOf(vertices);
            info.usage = VkBufferUsageFlags.VertexBufferBit | VkBufferUsageFlags.TransferDstBit;

            vertexBuffer = new Buffer(device, info);

            vertexAllocation = renderer.Allocator.Alloc(vertexBuffer.Requirements, VkMemoryPropertyFlags.DeviceLocalBit);
            vertexBuffer.Bind(vertexAllocation.memory, vertexAllocation.offset);

            transferNode.Transfer(vertices, vertexBuffer);
        }

        ShaderModule CreateShaderModule(Device device, byte[] code) {
            var info = new ShaderModuleCreateInfo(code);
            return new ShaderModule(device, info);
        }

        void CreatePipeline(Device device, Window window) {
            var vert = CreateShaderModule(device, File.ReadAllBytes("vert.spv"));
            var frag = CreateShaderModule(device, File.ReadAllBytes("frag.spv"));

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
            vertexInputInfo.vertexBindingDescriptions = new List<VkVertexInputBindingDescription> { Vertex.GetBindingDescription() };
            vertexInputInfo.vertexAttributeDescriptions = Vertex.GetAttributeDescriptions();

            var inputAssembly = new PipelineInputAssemblyStateCreateInfo();
            inputAssembly.topology = VkPrimitiveTopology.TriangleList;

            var viewport = new VkViewport();
            viewport.width = window.SwapchainExtent.width;
            viewport.height = window.SwapchainExtent.height;
            viewport.minDepth = 0f;
            viewport.maxDepth = 1f;

            var scissor = new VkRect2D();
            scissor.extent = window.SwapchainExtent;

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
            pipelineLayoutInfo.setLayouts = new List<DescriptorSetLayout> { camera.Layout };

            pipelineLayout?.Dispose();

            pipelineLayout = new PipelineLayout(device, pipelineLayoutInfo);

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
            info.basePipeline = null;
            info.basePipelineIndex = -1;

            pipeline?.Dispose();

            pipeline = new Pipeline(device, info, null);

            vert.Dispose();
            frag.Dispose();
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
                renderPassInfo.renderPass = renderPass;
                renderPassInfo.framebuffer = framebuffers[i];
                renderPassInfo.renderArea.extent = window.SwapchainExtent;

                VkClearValue clearColor = new VkClearValue();
                clearColor.color.float32_0 = 0;
                clearColor.color.float32_1 = 0;
                clearColor.color.float32_2 = 0;
                clearColor.color.float32_3 = 1f;

                renderPassInfo.clearValues = new List<VkClearValue> { clearColor };

                buffer.BeginRenderPass(renderPassInfo, VkSubpassContents.Inline);
                buffer.BindPipeline(VkPipelineBindPoint.Graphics, pipeline);
                buffer.BindDescriptorSets(VkPipelineBindPoint.Graphics, pipelineLayout, 0, new DescriptorSet[] { camera.Desciptor });
                buffer.BindVertexBuffers(0, new Buffer[] { vertexBuffer }, new ulong[] { 0 });
                buffer.Draw(3, 1, 0, 0);
                buffer.EndRenderPass();
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

            vertexBuffer.Dispose();
            commandPool.Dispose();
            pipeline.Dispose();
            pipelineLayout.Dispose();
            foreach (var fb in framebuffers) fb.Dispose();
            foreach (var iv in imageViews) iv.Dispose();
            renderPass.Dispose();
            engine.Graphics.Allocator.Free(vertexAllocation);

            disposed = true;
        }

        ~TriangleNode() {
            Dispose(false);
        }
    }

    public struct Vertex {
        public Vector3 position;
        public Vector3 color;

        public Vertex(Vector3 position, Vector3 color) {
            this.position = position;
            this.color = color;
        }

        public static VkVertexInputBindingDescription GetBindingDescription() {
            var result = new VkVertexInputBindingDescription();
            result.binding = 0;
            result.stride = (uint)Interop.SizeOf<Vertex>();
            result.inputRate = VkVertexInputRate.Vertex;

            return result;
        }

        public static List<VkVertexInputAttributeDescription> GetAttributeDescriptions() {
            Vertex v = new Vertex();
            var a = new VkVertexInputAttributeDescription();
            a.binding = 0;
            a.location = 0;
            a.format = VkFormat.R32g32b32Sfloat;
            a.offset = (uint)Interop.Offset(ref v, ref v.position);

            var b = new VkVertexInputAttributeDescription();
            b.binding = 0;
            b.location = 1;
            b.format = VkFormat.R32g32b32Sfloat;
            b.offset = (uint)Interop.Offset(ref v, ref v.color);

            return new List<VkVertexInputAttributeDescription> { a, b };
        }
    }
}
