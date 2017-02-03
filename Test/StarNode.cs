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
    public class StarNode : IRenderer, IDisposable {
        bool disposed;
        Engine engine;
        TransferNode transferNode;
        DeferredNode deferredNode;
        Camera camera;
        
        PipelineLayout pipelineLayout;
        Pipeline pipeline;
        CommandPool commandPool;
        Buffer vertexBuffer;
        VkaAllocation vertexAllocation;
        CommandBuffer commandBuffer;
        uint index;

        List<Star> stars;

        public StarNode(Engine engine, TransferNode transferNode, DeferredNode deferredNode) {
            if (engine == null) throw new ArgumentNullException(nameof(engine));
            if (engine.Window == null) throw new ArgumentNullException(nameof(engine.Window));
            if (engine.Camera == null) throw new ArgumentNullException(nameof(engine.Camera));
            if (transferNode == null) throw new ArgumentNullException(nameof(transferNode));
            if (deferredNode == null) throw new ArgumentNullException(nameof(deferredNode));

            this.engine = engine;
            this.transferNode = transferNode;
            this.deferredNode = deferredNode;
            camera = engine.Camera;

            Random rand = new Random(0);
            stars = new List<Star>();
            for (int i = 0; i < 1000; i++) {
                Vector4 pos = new Vector4((float)(rand.NextDouble() * 2) - 1, (float)(rand.NextDouble() * 2) - 1, (float)(rand.NextDouble() * 2) - 1, (float)(rand.NextDouble() * 10) + 1);
                Vector3 col = new Vector3(1, 1, 1);
                stars.Add(new Star(pos, col));
            }
            
            CreatePipeline(engine.Graphics.Device, engine.Window);
            CreateVertexBuffer(engine.Graphics);
            CreateCommandPool(engine);
            CreateCommandBuffers(engine.Graphics.Device, engine.Window);

            deferredNode.Opaque.AddRenderer(this);
        }

        void CreateVertexBuffer(Graphics renderer) {
            BufferCreateInfo info = new BufferCreateInfo();
            info.sharingMode = VkSharingMode.Exclusive;
            info.size = (uint)Interop.SizeOf(stars);
            info.usage = VkBufferUsageFlags.VertexBufferBit | VkBufferUsageFlags.TransferDstBit;

            vertexBuffer = new Buffer(engine.Graphics.Device, info);

            vertexAllocation = renderer.Allocator.Alloc(vertexBuffer.Requirements, VkMemoryPropertyFlags.DeviceLocalBit);
            vertexBuffer.Bind(vertexAllocation.memory, vertexAllocation.offset);

            transferNode.Transfer(stars, vertexBuffer);
        }

        ShaderModule CreateShaderModule(Device device, byte[] code) {
            var info = new ShaderModuleCreateInfo(code);
            return new ShaderModule(device, info);
        }

        void CreatePipeline(Device device, Window window) {
            var vert = CreateShaderModule(device, File.ReadAllBytes("stars_vert.spv"));
            var frag = CreateShaderModule(device, File.ReadAllBytes("stars_frag.spv"));

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
            vertexInputInfo.vertexBindingDescriptions = new List<VkVertexInputBindingDescription> { Star.GetBindingDescription() };
            vertexInputInfo.vertexAttributeDescriptions = Star.GetAttributeDescriptions();

            var inputAssembly = new PipelineInputAssemblyStateCreateInfo();
            inputAssembly.topology = VkPrimitiveTopology.PointList;

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
            rasterizer.polygonMode = VkPolygonMode.Point;
            rasterizer.lineWidth = 10f;
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
            light.srcColorBlendFactor = VkBlendFactor.One;
            light.dstColorBlendFactor = VkBlendFactor.Zero;
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
            depth.depthWriteEnable = false;
            depth.depthCompareOp = VkCompareOp.LessOrEqual;

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
            info.depthStencilState = depth;
            info.layout = pipelineLayout;
            info.renderPass = deferredNode.RenderGraph.RenderPass;
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
            var info = new CommandBufferAllocateInfo();
            info.level = VkCommandBufferLevel.Primary;
            info.commandBufferCount = (uint)window.SwapchainImages.Count;

            commandBuffer = commandPool.Allocate(VkCommandBufferLevel.Secondary);

            CommandBufferInheritanceInfo inheritance = new CommandBufferInheritanceInfo();
            inheritance.renderPass = deferredNode.RenderGraph.RenderPass;
            inheritance.framebuffer = deferredNode.Framebuffer;
            inheritance.subpass = 0;

            CommandBufferBeginInfo beginInfo = new CommandBufferBeginInfo();
            beginInfo.flags = VkCommandBufferUsageFlags.SimultaneousUseBit | VkCommandBufferUsageFlags.RenderPassContinueBit;
            beginInfo.inheritanceInfo = inheritance;

            commandBuffer.Begin(beginInfo);

            commandBuffer.BindPipeline(VkPipelineBindPoint.Graphics, pipeline);
            commandBuffer.BindDescriptorSets(VkPipelineBindPoint.Graphics, pipelineLayout, 0, new DescriptorSet[] { camera.Desciptor });
            commandBuffer.BindVertexBuffers(0, new Buffer[] { vertexBuffer }, new ulong[] { 0 });
            commandBuffer.Draw(stars.Count, 1, 0, 0);

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

            disposed = true;
        }

        ~StarNode() {
            Dispose(false);
        }
    }

    public struct Star {
        public Vector4 position;
        public Vector3 color;

        public Star(Vector4 position, Vector3 color) {
            this.position = position;
            this.color = color;
        }

        public static VkVertexInputBindingDescription GetBindingDescription() {
            var result = new VkVertexInputBindingDescription();
            result.binding = 0;
            result.stride = (uint)Interop.SizeOf<Star>();
            result.inputRate = VkVertexInputRate.Vertex;

            return result;
        }

        public static List<VkVertexInputAttributeDescription> GetAttributeDescriptions() {
            Star v = new Star();
            var a = new VkVertexInputAttributeDescription();
            a.binding = 0;
            a.location = 0;
            a.format = VkFormat.R32g32b32a32Sfloat;
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
