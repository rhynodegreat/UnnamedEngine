using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;

using CSGL;
using CSGL.Graphics;
using CSGL.Vulkan1;

using UnnamedEngine.Core;
using UnnamedEngine.ECS;
using UnnamedEngine.Rendering;

namespace UnnamedEngine.UI {
    public class PanelRenderer : UIRenderer {
        bool disposed;

        Engine engine;
        Screen screen;
        RenderPass renderPass;

        PipelineLayout pipelineLayout;
        Pipeline pipeline;
        
        struct PanelInfo {
            public Matrix4x4 model;
            public Color4 color;

            public PanelInfo(Matrix4x4 model, Color4 color) {
                this.model = model;
                this.color = color;
            }
        }

        public PanelRenderer(Engine engine, Screen screen, RenderPass renderPass) {
            if (engine == null) throw new ArgumentNullException(nameof(engine));
            if (screen == null) throw new ArgumentNullException(nameof(screen));
            if (renderPass == null) throw new ArgumentNullException(nameof(renderPass));

            this.engine = engine;
            this.screen = screen;
            this.renderPass = renderPass;

            CreatePipeline();

            screen.OnSizeChanged += Recreate;
        }

        void Recreate(int width, int height) {
            CreatePipeline();
        }

        ShaderModule CreateShaderModule(Device device, byte[] code) {
            var info = new ShaderModuleCreateInfo(code);
            return new ShaderModule(device, info);
        }

        void CreatePipeline() {
            var vert = CreateShaderModule(engine.Graphics.Device, File.ReadAllBytes("shaders/panel_vert.spv"));
            var frag = CreateShaderModule(engine.Graphics.Device, File.ReadAllBytes("shaders/panel_frag.spv"));

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

            var inputAssembly = new PipelineInputAssemblyStateCreateInfo();
            inputAssembly.topology = VkPrimitiveTopology.TriangleList;

            var viewport = new VkViewport();
            viewport.width = screen.Width;
            viewport.height = screen.Height;
            viewport.minDepth = 0f;
            viewport.maxDepth = 1f;

            var scissor = new VkRect2D();
            scissor.extent.width = (uint)screen.Width;
            scissor.extent.height = (uint)screen.Height;

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

            var color = new PipelineColorBlendAttachmentState();
            color.colorWriteMask = VkColorComponentFlags.RBit
                                                | VkColorComponentFlags.GBit
                                                | VkColorComponentFlags.BBit
                                                | VkColorComponentFlags.ABit;
            color.blendEnable = true;
            color.srcColorBlendFactor = VkBlendFactor.SrcAlpha;
            color.dstColorBlendFactor = VkBlendFactor.OneMinusSrcAlpha;

            var colorBlending = new PipelineColorBlendStateCreateInfo();
            colorBlending.logicOp = VkLogicOp.Copy;
            colorBlending.attachments = new List<PipelineColorBlendAttachmentState> { color };

            var pipelineLayoutInfo = new PipelineLayoutCreateInfo();
            pipelineLayoutInfo.setLayouts = new List<DescriptorSetLayout> { screen.Camera.Manager.Layout };
            pipelineLayoutInfo.pushConstantRanges = new List<VkPushConstantRange> {
                new VkPushConstantRange {
                    offset = 0,
                    size = (uint)Interop.SizeOf<PanelInfo>(),
                    stageFlags = VkShaderStageFlags.VertexBit | VkShaderStageFlags.FragmentBit
                }
            };

            PipelineDepthStencilStateCreateInfo depth = new PipelineDepthStencilStateCreateInfo();
            depth.depthTestEnable = false;
            depth.depthWriteEnable = false;

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
            info.basePipelineHandle = oldPipeline;
            info.basePipelineIndex = -1;

            pipeline = new GraphicsPipeline(engine.Graphics.Device, info, null);

            oldPipeline?.Dispose();

            vert.Dispose();
            frag.Dispose();
        }

        public void PreRenderElement(UIElement element) {

        }

        public void PreRender() {

        }

        public void Render(CommandBuffer commandBuffer, UIElement element) {
            Transform transform = element.Transform;
            commandBuffer.BindPipeline(VkPipelineBindPoint.Graphics, pipeline);
            commandBuffer.BindDescriptorSets(VkPipelineBindPoint.Graphics, pipelineLayout, 0, screen.Camera.Manager.Descriptor, screen.Camera.Offset);

            Panel p = (Panel)element;
            Matrix4x4 model = Matrix4x4.CreateScale(p.Size.X, p.Size.Y, 1) * transform.WorldTransform;

            commandBuffer.PushConstants(pipelineLayout, VkShaderStageFlags.VertexBit | VkShaderStageFlags.FragmentBit, 0, new PanelInfo(model, p.Color));
            commandBuffer.Draw(6, 1, 0, 0);
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposing) {
            if (disposed) return;

            pipeline.Dispose();
            pipelineLayout.Dispose();
            screen.OnSizeChanged -= Recreate;

            disposed = true;
        }

        ~PanelRenderer() {
            Dispose(false);
        }
    }
}
