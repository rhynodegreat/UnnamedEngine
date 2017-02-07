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
    public class TriangleRenderer : ISubpass, IDisposable {
        bool disposed;
        Engine engine;
        TransferNode transferNode;
        DeferredNode deferredNode;
        Camera camera;
        RenderPass renderPass;
        uint subpassIndex;

        PipelineLayout pipelineLayout;
        Pipeline pipeline;
        CommandPool commandPool;
        Buffer vertexBuffer;
        VkaAllocation vertexAllocation;
        CommandBuffer commandBuffer;
        
        Vertex[] vertices = {
            new Vertex(new Vector3(0, 1, 0), new Vector3(1, 0, 0), new Vector3(0, 0, 1)),
            new Vertex(new Vector3(1, -1, 0), new Vector3(0, 1, 0), new Vector3(0, 0, 1)),
            new Vertex(new Vector3(-1, -1, 0), new Vector3(0, 0, 1), new Vector3(0, 0, 1)),
            new Vertex(new Vector3(0, 1, -.1f), new Vector3(1, 0, 0), new Vector3(0, 0, -1)),
            new Vertex(new Vector3(1, -1, -.1f), new Vector3(0, 1, 0), new Vector3(0, 0, -1)),
            new Vertex(new Vector3(-1, -1, -.1f), new Vector3(0, 0, 1), new Vector3(0, 0, -1)),
        };

        public TriangleRenderer(Engine engine, DeferredNode deferredNode) {
            if (engine == null) throw new ArgumentNullException(nameof(engine));
            if (engine.Camera == null) throw new ArgumentNullException(nameof(engine.Camera));
            if (deferredNode == null) throw new ArgumentNullException(nameof(deferredNode));

            this.engine = engine;
            transferNode = engine.Graphics.TransferNode;
            this.deferredNode = deferredNode;
            camera = engine.Camera;

            CreateVertexBuffer(engine.Graphics);
            CreateCommandPool(engine);

            deferredNode.Opaque.AddRenderer(this);

            deferredNode.OnFramebufferChanged += () => {
                CreatePipeline();
                CreateCommandBuffers();
            };

            CreateCommandBuffers();
        }

        public void Bake(RenderPass renderPass, uint subpassIndex) {
            this.renderPass = renderPass;
            this.subpassIndex = subpassIndex;
            CreatePipeline();
        }

        void CreateVertexBuffer(Graphics renderer) {
            BufferCreateInfo info = new BufferCreateInfo();
            info.sharingMode = VkSharingMode.Exclusive;
            info.size = (uint)Interop.SizeOf(vertices);
            info.usage = VkBufferUsageFlags.VertexBufferBit | VkBufferUsageFlags.TransferDstBit;

            vertexBuffer = new Buffer(engine.Graphics.Device, info);

            vertexAllocation = renderer.Allocator.Alloc(vertexBuffer.Requirements, VkMemoryPropertyFlags.DeviceLocalBit);
            vertexBuffer.Bind(vertexAllocation.memory, vertexAllocation.offset);

            transferNode.Transfer(vertices, vertexBuffer);
        }

        ShaderModule CreateShaderModule(Device device, byte[] code) {
            var info = new ShaderModuleCreateInfo(code);
            return new ShaderModule(device, info);
        }

        void CreatePipeline() {
            var vert = CreateShaderModule(engine.Graphics.Device, File.ReadAllBytes("tri_vert.spv"));
            var frag = CreateShaderModule(engine.Graphics.Device, File.ReadAllBytes("tri_frag.spv"));

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
            viewport.width = deferredNode.GBuffer.Width;
            viewport.height = deferredNode.GBuffer.Height;
            viewport.minDepth = 0f;
            viewport.maxDepth = 1f;

            var scissor = new VkRect2D();
            scissor.extent.width = (uint)deferredNode.GBuffer.Width;
            scissor.extent.height = (uint)deferredNode.GBuffer.Height;

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

            var albedo = new PipelineColorBlendAttachmentState();
            albedo.colorWriteMask = VkColorComponentFlags.RBit
                                                | VkColorComponentFlags.GBit
                                                | VkColorComponentFlags.BBit
                                                | VkColorComponentFlags.ABit;
            albedo.srcColorBlendFactor = VkBlendFactor.One;
            albedo.dstColorBlendFactor = VkBlendFactor.Zero;
            albedo.colorBlendOp = VkBlendOp.Add;
            albedo.srcAlphaBlendFactor = VkBlendFactor.One;
            albedo.dstAlphaBlendFactor = VkBlendFactor.Zero;
            albedo.alphaBlendOp = VkBlendOp.Add;

            var norm = new PipelineColorBlendAttachmentState();
            norm.colorWriteMask = VkColorComponentFlags.RBit
                                                | VkColorComponentFlags.GBit
                                                | VkColorComponentFlags.BBit
                                                | VkColorComponentFlags.ABit;
            norm.srcColorBlendFactor = VkBlendFactor.One;
            norm.dstColorBlendFactor = VkBlendFactor.Zero;
            norm.colorBlendOp = VkBlendOp.Add;
            norm.srcAlphaBlendFactor = VkBlendFactor.One;
            norm.dstAlphaBlendFactor = VkBlendFactor.Zero;
            norm.alphaBlendOp = VkBlendOp.Add;

            var light = new PipelineColorBlendAttachmentState();
            light.colorWriteMask = VkColorComponentFlags.RBit
                                                | VkColorComponentFlags.GBit
                                                | VkColorComponentFlags.BBit
                                                | VkColorComponentFlags.ABit;
            light.blendEnable = true;
            light.srcColorBlendFactor = VkBlendFactor.One;
            light.dstColorBlendFactor = VkBlendFactor.One;
            light.colorBlendOp = VkBlendOp.Add;
            light.srcAlphaBlendFactor = VkBlendFactor.One;
            light.dstAlphaBlendFactor = VkBlendFactor.Zero;
            light.alphaBlendOp = VkBlendOp.Add;

            var colorBlending = new PipelineColorBlendStateCreateInfo();
            colorBlending.logicOp = VkLogicOp.Copy;
            colorBlending.attachments = new List<PipelineColorBlendAttachmentState> { albedo, norm, light };

            var pipelineLayoutInfo = new PipelineLayoutCreateInfo();
            pipelineLayoutInfo.setLayouts = new List<DescriptorSetLayout> { camera.Layout };

            PipelineDepthStencilStateCreateInfo depth = new PipelineDepthStencilStateCreateInfo();
            depth.depthTestEnable = true;
            depth.depthWriteEnable = true;
            depth.depthCompareOp = VkCompareOp.Greater;

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
            info.depthStencilState = depth;
            info.layout = pipelineLayout;
            info.renderPass = renderPass;
            info.subpass = 0;
            info.basePipeline = oldPipeline;
            info.basePipelineIndex = -1;

            pipeline = new Pipeline(engine.Graphics.Device, info, null);

            oldPipeline?.Dispose();

            vert.Dispose();
            frag.Dispose();
        }

        void CreateCommandPool(Engine engine) {
            var info = new CommandPoolCreateInfo();
            info.queueFamilyIndex = engine.Graphics.GraphicsQueue.FamilyIndex;
            info.flags = VkCommandPoolCreateFlags.ResetCommandBufferBit;

            commandPool = new CommandPool(engine.Graphics.Device, info);

            commandBuffer = commandPool.Allocate(VkCommandBufferLevel.Secondary);
        }

        void CreateCommandBuffers() {
            commandBuffer.Reset(VkCommandBufferResetFlags.None);

            CommandBufferInheritanceInfo inheritance = new CommandBufferInheritanceInfo();
            inheritance.renderPass = renderPass;
            //inheritance.framebuffer = deferredNode.Framebuffer;
            inheritance.subpass = subpassIndex;

            CommandBufferBeginInfo beginInfo = new CommandBufferBeginInfo();
            beginInfo.flags = VkCommandBufferUsageFlags.SimultaneousUseBit | VkCommandBufferUsageFlags.RenderPassContinueBit;
            beginInfo.inheritanceInfo = inheritance;

            commandBuffer.Begin(beginInfo);

            commandBuffer.BindPipeline(VkPipelineBindPoint.Graphics, pipeline);
            commandBuffer.BindDescriptorSets(VkPipelineBindPoint.Graphics, pipelineLayout, 0, new DescriptorSet[] { camera.Desciptor });
            commandBuffer.BindVertexBuffers(0, new Buffer[] { vertexBuffer }, new ulong[] { 0 });
            commandBuffer.Draw(vertices.Length, 1, 0, 0);

            commandBuffer.End();
        }

        public CommandBuffer GetCommandBuffer() {
            return commandBuffer;
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposing) {
            if (disposed) return;

            vertexBuffer.Dispose();
            commandPool.Dispose();
            pipeline.Dispose();
            pipelineLayout.Dispose();
            engine.Graphics.Allocator.Free(vertexAllocation);

            deferredNode.OnFramebufferChanged -= CreateCommandBuffers;

            disposed = true;
        }

        ~TriangleRenderer() {
            Dispose(false);
        }
    }

    public struct Vertex {
        public Vector3 position;
        public Vector3 color;
        public Vector3 normal;

        public Vertex(Vector3 position, Vector3 color, Vector3 normal) {
            this.position = position;
            this.color = color;
            this.normal = normal;
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

            var c = new VkVertexInputAttributeDescription();
            c.binding = 0;
            c.location = 2;
            c.format = VkFormat.R32g32b32Sfloat;
            c.offset = (uint)Interop.Offset(ref v, ref v.normal);

            return new List<VkVertexInputAttributeDescription> { a, b, c };
        }
    }
}
