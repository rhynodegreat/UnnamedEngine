using System;
using System.IO;
using System.Collections.Generic;
using System.Numerics;

using CSGL;
using CSGL.Vulkan;
using Buffer = CSGL.Vulkan.Buffer;

using UnnamedEngine.Core;
using UnnamedEngine.Rendering;
using UnnamedEngine.Resources;
using UnnamedEngine.Utilities;

namespace Test {
    public class TriangleRenderNode : RenderNode {
        bool disposed;

        Engine engine;
        Camera camera;
        CommandPool pool;
        TransferNode transferNode;

        RenderPass renderPass;
        uint subpassIndex;

        CommandBuffer commandBuffer;
        PipelineLayout pipelineLayout;
        Pipeline pipeline;
        Buffer vertexBuffer;
        VkaAllocation vertexAllocation;

        List<CommandBuffer> renderBuffers;

        Vertex[] vertices = {
            new Vertex(new Vector3(0, 1, 0), new Vector3(1, 0, 0)),
            new Vertex(new Vector3(1, -1, 0), new Vector3(0, 1, 0)),
            new Vertex(new Vector3(-1, -1, 0), new Vector3(0, 0, 1)),
        };

        public TriangleRenderNode(Engine engine, CommandPool pool, TransferNode transferNode) {
            if (engine == null) throw new ArgumentNullException(nameof(engine));
            if (pool == null) throw new ArgumentNullException(nameof(pool));

            this.engine = engine;
            camera = engine.Camera;
            this.pool = pool;
            this.transferNode = transferNode;

            CreateVertexBuffer();
        }

        protected override void Bake(RenderPass renderPass, uint subpassIndex) {
            this.renderPass = renderPass;
            this.subpassIndex = subpassIndex;

            CreatePipeline();
            CreateCommandBuffer();
        }

        void CreateVertexBuffer() {
            BufferCreateInfo info = new BufferCreateInfo();
            info.sharingMode = VkSharingMode.Exclusive;
            info.size = (uint)Interop.SizeOf(vertices);
            info.usage = VkBufferUsageFlags.VertexBufferBit | VkBufferUsageFlags.TransferDstBit;

            vertexBuffer = new Buffer(engine.Graphics.Device, info);

            vertexAllocation = engine.Graphics.Allocator.Alloc(vertexBuffer.Requirements, VkMemoryPropertyFlags.DeviceLocalBit);
            vertexBuffer.Bind(vertexAllocation.memory, vertexAllocation.offset);

            transferNode.Transfer(vertices, vertexBuffer);
        }

        ShaderModule CreateShaderModule(Device device, byte[] code) {
            var info = new ShaderModuleCreateInfo(code);
            return new ShaderModule(device, info);
        }

        void CreatePipeline() {
            var vert = CreateShaderModule(engine.Graphics.Device, File.ReadAllBytes("vert.spv"));
            var frag = CreateShaderModule(engine.Graphics.Device, File.ReadAllBytes("frag.spv"));

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
            pipelineLayoutInfo.setLayouts = new List<DescriptorSetLayout> { camera.Layout };

            pipelineLayout?.Dispose();

            pipelineLayout = new PipelineLayout(engine.Graphics.Device, pipelineLayoutInfo);

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

            pipeline = new Pipeline(engine.Graphics.Device, info, null);

            vert.Dispose();
            frag.Dispose();
        }

        void CreateCommandBuffer() {
            commandBuffer = pool.Allocate(VkCommandBufferLevel.Secondary);

            CommandBufferInheritanceInfo inheritance = new CommandBufferInheritanceInfo();
            inheritance.subpass = subpassIndex;
            inheritance.renderPass = renderPass;

            CommandBufferBeginInfo beginInfo = new CommandBufferBeginInfo();
            beginInfo.flags = VkCommandBufferUsageFlags.RenderPassContinueBit | VkCommandBufferUsageFlags.SimultaneousUseBit;
            beginInfo.inheritanceInfo = inheritance;

            commandBuffer.Begin(beginInfo);
            commandBuffer.BindPipeline(VkPipelineBindPoint.Graphics, pipeline);
            commandBuffer.BindDescriptorSets(VkPipelineBindPoint.Graphics, pipelineLayout, 0, new DescriptorSet[] { camera.Desciptor });
            commandBuffer.BindVertexBuffers(0, new Buffer[] { vertexBuffer }, new ulong[] { 0 });
            commandBuffer.Draw(3, 1, 0, 0);
            commandBuffer.End();

            renderBuffers = new List<CommandBuffer> { commandBuffer };
        }

        public override List<CommandBuffer> GetCommands() {
            return renderBuffers;
        }

        public new void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected override void Dispose(bool disposing) {
            if (disposed) return;

            vertexBuffer.Dispose();
            pipeline.Dispose();
            pipelineLayout.Dispose();
            engine.Graphics.Allocator.Free(vertexAllocation);

            base.Dispose(disposing);

            disposed = true;
        }

        ~TriangleRenderNode() {
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
