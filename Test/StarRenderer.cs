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
    public class StarRenderer : ISubpass, IDisposable {
        bool disposed;
        Engine engine;
        TransferNode transferNode;
        Deferred deferred;
        Camera camera;
        RenderPass renderPass;
        uint subpassIndex;
        
        PipelineLayout pipelineLayout;
        Pipeline pipeline;
        CommandPool commandPool;
        Buffer vertexBuffer;
        VkaAllocation vertexAllocation;
        CommandBuffer commandBuffer;

        List<Star> stars;

        public StarRenderer(Engine engine, Deferred deferred, Camera camera) {
            if (engine == null) throw new ArgumentNullException(nameof(engine));
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            if (deferred == null) throw new ArgumentNullException(nameof(deferred));

            this.engine = engine;
            transferNode = engine.Graphics.TransferNode;
            this.deferred = deferred;
            this.camera = camera;

            Random rand = new Random(0);
            stars = new List<Star>();
            int subdivisions = 100;

            for (int x = 0; x < subdivisions; x++) {
                for (int y = 0; y < subdivisions; y++) {
                    for (int z = 0; z < subdivisions; z++) {
                        if (rand.NextDouble() < 0.01d) {
                            stars.Add(new Star(new Vector4(
                                (10 * x / (float)subdivisions) - 5,
                                (10 * y / (float)subdivisions) - 5,
                                (10 * z / (float)subdivisions) - 5, (float)(rand.NextDouble() * 10) + 1),
                                new Vector3(1, 1, 1)));
                        }
                    }
                }
            }

            Console.WriteLine("{0:n0} stars    {1:n0} bytes", stars.Count, Interop.SizeOf(stars));
            
            CreateVertexBuffer(engine.Graphics);
            CreateCommandPool(engine);

            deferred.Opaque.AddRenderer(this);

            deferred.OnFramebufferChanged += Recreate;
            CreateCommandBuffers();
        }

        void Recreate() {
            CreatePipeline();
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

        void CreatePipeline() {
            var vert = CreateShaderModule(engine.Graphics.Device, File.ReadAllBytes("shaders/stars_vert.spv"));
            var frag = CreateShaderModule(engine.Graphics.Device, File.ReadAllBytes("shaders/stars_frag.spv"));

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
            viewport.width = deferred.GBuffer.Width;
            viewport.height = deferred.GBuffer.Height;
            viewport.minDepth = 0f;
            viewport.maxDepth = 1f;

            var scissor = new VkRect2D();
            scissor.extent.width = (uint)deferred.GBuffer.Width;
            scissor.extent.height = (uint)deferred.GBuffer.Height;

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
            albedo.colorWriteMask = VkColorComponentFlags.None;

            var norm = new PipelineColorBlendAttachmentState();
            norm.colorWriteMask = VkColorComponentFlags.None;

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
            pipelineLayoutInfo.pushConstantRanges = new List<VkPushConstantRange> {
                new VkPushConstantRange {
                    offset = 0,
                    size = 4,
                    stageFlags = VkShaderStageFlags.VertexBit
                }
            };

            PipelineDepthStencilStateCreateInfo depth = new PipelineDepthStencilStateCreateInfo();
            depth.depthTestEnable = true;
            depth.depthWriteEnable = false;
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
            info.flags = VkCommandPoolCreateFlags.ResetCommandBufferBit;
            info.queueFamilyIndex = engine.Graphics.GraphicsQueue.FamilyIndex;

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
            commandBuffer.BindDescriptorSets(VkPipelineBindPoint.Graphics, pipelineLayout, 0, new DescriptorSet[] { camera.Descriptor });
            commandBuffer.BindVertexBuffers(0, new Buffer[] { vertexBuffer }, new ulong[] { 0 });
            commandBuffer.PushConstants(pipelineLayout, VkShaderStageFlags.VertexBit, 0, camera.Index);
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

            deferred.OnFramebufferChanged -= Recreate;

            disposed = true;
        }

        ~StarRenderer() {
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
