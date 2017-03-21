using System;
using System.IO;
using System.Collections.Generic;
using System.Numerics;

using CSGL;
using CSGL.Vulkan;

using UnnamedEngine.Core;
using UnnamedEngine.ECS;
using UnnamedEngine.Rendering;
using UnnamedEngine.UI.Text;

namespace UnnamedEngine.UI {
    public class LabelRenderer : UIRenderer {
        bool disposed;

        Engine engine;
        Screen screen;
        RenderPass renderPass;
        GlyphCache cache;

        Dictionary<Label, int> labelHashes;

        PipelineLayout pipelineLayout;
        Pipeline pipeline;

        public LabelRenderer(Engine engine, Screen screen, RenderPass renderPass, GlyphCache cache) {
            if (engine == null) throw new ArgumentNullException(nameof(engine));
            if (screen == null) throw new ArgumentNullException(nameof(screen));
            if (renderPass == null) throw new ArgumentNullException(nameof(renderPass));
            if (cache == null) throw new ArgumentNullException(nameof(cache));

            this.engine = engine;
            this.screen = screen;
            this.renderPass = renderPass;
            this.cache = cache;

            labelHashes = new Dictionary<Label, int>();

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
            var vert = CreateShaderModule(engine.Graphics.Device, File.ReadAllBytes("shaders/label_vert.spv"));
            var frag = CreateShaderModule(engine.Graphics.Device, File.ReadAllBytes("shaders/label_frag.spv"));

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
            vertexInputInfo.vertexAttributeDescriptions = LabelVertex.GetAttributeDescriptions();
            vertexInputInfo.vertexBindingDescriptions = new List<VkVertexInputBindingDescription> { LabelVertex.GetBindingDescription() };

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
            pipelineLayoutInfo.setLayouts = new List<DescriptorSetLayout> { screen.Camera.Manager.Layout, cache.DescriptorLayout };
            pipelineLayoutInfo.pushConstantRanges = new List<VkPushConstantRange> {
                new VkPushConstantRange {
                    offset = 0,
                    size = (uint)Interop.SizeOf<FontMetrics>(),
                    stageFlags = VkShaderStageFlags.FragmentBit
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
            info.basePipeline = oldPipeline;
            info.basePipelineIndex = -1;

            pipeline = new Pipeline(engine.Graphics.Device, info, null);

            oldPipeline?.Dispose();

            vert.Dispose();
            frag.Dispose();
        }

        public void PreRenderElement(Entity e, Transform transform, UIElement element) {
            Label l = (Label)element;
            int hash = l.Text.GetHashCode() ^ l.Font.GetHashCode(); //this hash has to be computed here because the GetHashCode of Label can't be changed, since we're using as the dictionary key

            if (!labelHashes.ContainsKey(l)) {
                cache.AddString(l.Font, l.Text);
                labelHashes.Add(l, hash);
            } else if (labelHashes[l] != hash) {
                cache.AddString(l.Font, l.Text);
                labelHashes[l] = hash;
            }
        }

        public void PreRender() {
            cache.Update();
        }

        public void Render(CommandBuffer commandbuffer, Entity e, Transform transform, UIElement element) {

        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposing) {
            if (disposed) return;

            pipeline.Dispose();
            pipelineLayout.Dispose();

            disposed = true;
        }

        ~LabelRenderer() {
            Dispose(false);
        }

        struct FontMetrics {
            Vector4 color;
            Vector4 borderColor;
            float bias;
            float scale;
            float borderThickness;

            public FontMetrics(Vector4 color, Vector4 borderColor, float bias, float scale, float borderThickness) {
                this.color = color;
                this.borderColor = borderColor;
                this.bias = bias;
                this.scale = scale;
                this.borderThickness = borderThickness;
            }
        }

        public struct LabelVertex {
            public Vector3 position;
            public Vector3 uv;

            public LabelVertex(Vector3 position, Vector3 uv) {
                this.position = position;
                this.uv = uv;
            }

            public static VkVertexInputBindingDescription GetBindingDescription() {
                var result = new VkVertexInputBindingDescription();
                result.binding = 0;
                result.stride = (uint)Interop.SizeOf<LabelVertex>();
                result.inputRate = VkVertexInputRate.Vertex;

                return result;
            }

            public static List<VkVertexInputAttributeDescription> GetAttributeDescriptions() {
                LabelVertex v = new LabelVertex();
                var a = new VkVertexInputAttributeDescription();
                a.binding = 0;
                a.location = 0;
                a.format = VkFormat.R32g32b32Sfloat;
                a.offset = (uint)Interop.Offset(ref v, ref v.position);

                var b = new VkVertexInputAttributeDescription();
                b.binding = 0;
                b.location = 1;
                b.format = VkFormat.R32g32b32Sfloat;
                b.offset = (uint)Interop.Offset(ref v, ref v.uv);

                return new List<VkVertexInputAttributeDescription> { a, b };
            }
        }
    }
}
