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
    public class BasicRenderer : ISubpass, IDisposable {
        bool disposed;
        Engine engine;
        TransferNode transferNode;
        Deferred deferred;
        Camera camera;
        RenderPass renderPass;
        uint subpassIndex;

        Mesh mesh;

        PipelineLayout pipelineLayout;
        Pipeline pipeline;
        CommandPool commandPool;
        CommandBuffer commandBuffer;

        public BasicRenderer(Engine engine, Deferred deferred, Mesh mesh, Camera camera) {
            if (engine == null) throw new ArgumentNullException(nameof(engine));
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            if (deferred == null) throw new ArgumentNullException(nameof(deferred));
            if (mesh == null) throw new ArgumentNullException(nameof(mesh));

            this.engine = engine;
            transferNode = engine.Graphics.TransferNode;
            this.deferred = deferred;
            this.camera = camera;
            this.mesh = mesh;
            
            CreateCommandPool(engine);

            deferred.Opaque.AddRenderer(this);

            deferred.OnFramebufferChanged += () => {
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

        ShaderModule CreateShaderModule(Device device, byte[] code) {
            var info = new ShaderModuleCreateInfo(code);
            return new ShaderModule(device, info);
        }

        void CreatePipeline() {
            var vert = CreateShaderModule(engine.Graphics.Device, File.ReadAllBytes("shaders/basic_vert.spv"));
            var frag = CreateShaderModule(engine.Graphics.Device, File.ReadAllBytes("shaders/basic_frag.spv"));

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
            vertexInputInfo.vertexBindingDescriptions = mesh.VertexData.Bindings;
            vertexInputInfo.vertexAttributeDescriptions = mesh.VertexData.Attributes;

            var inputAssembly = new PipelineInputAssemblyStateCreateInfo();
            inputAssembly.topology = VkPrimitiveTopology.TriangleList;

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

            var norm = new PipelineColorBlendAttachmentState();
            norm.colorWriteMask = VkColorComponentFlags.RBit
                                                | VkColorComponentFlags.GBit
                                                | VkColorComponentFlags.BBit
                                                | VkColorComponentFlags.ABit;

            var light = new PipelineColorBlendAttachmentState();
            light.colorWriteMask = VkColorComponentFlags.RBit
                                                | VkColorComponentFlags.GBit
                                                | VkColorComponentFlags.BBit
                                                | VkColorComponentFlags.ABit;

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
            commandBuffer.BindDescriptorSets(VkPipelineBindPoint.Graphics, pipelineLayout, 0, new DescriptorSet[] { camera.Descriptor });
            commandBuffer.BindVertexBuffer(0, mesh.VertexBuffer, 0);
            
            if (mesh.IndexBuffer != null) {
                commandBuffer.BindIndexBuffer(mesh.IndexBuffer, 0, mesh.IndexData.IndexType);
                commandBuffer.DrawIndexed((uint)mesh.IndexData.IndexCount, 1, 0, 0, 0);
            } else {
                commandBuffer.Draw(mesh.VertexData.VertexCount, 1, 0, 0);
            }

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

            if (disposing) {
                mesh.Dispose();
            }
            
            commandPool.Dispose();
            pipeline.Dispose();
            pipelineLayout.Dispose();

            deferred.OnFramebufferChanged -= CreateCommandBuffers;

            disposed = true;
        }

        ~BasicRenderer() {
            Dispose(false);
        }
    }
}
