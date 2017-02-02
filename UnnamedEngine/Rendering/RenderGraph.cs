using System;
using System.Collections.Generic;

using CSGL.Vulkan;

using UnnamedEngine.Core;

namespace UnnamedEngine.Rendering {
    public class RenderGraph : IDisposable {
        bool disposed;
        Engine engine;

        List<AttachmentDescription> attachments;
        Dictionary<RenderNode, SubpassDescription> nodeMap;
        List<RenderNode> nodes;
        List<SubpassDescription> subpasses;
        List<SubpassDependency> dependencies;

        public IList<AttachmentDescription> Attachments { get; private set; }
        public RenderPass RenderPass { get; private set; }

        public RenderGraph(Engine engine) {
            if (engine == null) throw new ArgumentNullException(nameof(engine));

            this.engine = engine;

            attachments = new List<AttachmentDescription>();
            subpasses = new List<SubpassDescription>();
            dependencies = new List<SubpassDependency>();
            Attachments = attachments.AsReadOnly();

            nodeMap = new Dictionary<RenderNode, SubpassDescription>();
            nodes = new List<RenderNode>();
        }

        public void Bake() {
            subpasses.Clear();
            dependencies.Clear();

            foreach (var node in nodeMap.Keys) {
                nodes.Add(node);
                subpasses.Add(nodeMap[node]);
            }

            foreach (var node in nodeMap.Keys) {
                Bake(node);
            }

            RenderPassCreateInfo info = new RenderPassCreateInfo();
            info.attachments = attachments;
            info.subpasses = subpasses;
            info.dependencies = dependencies;

            RenderPass?.Dispose();
            RenderPass = new RenderPass(engine.Graphics.Device, info);
        }

        void Bake(RenderNode node) {
            var desc = nodeMap[node];

            desc.inputAttachments = new List<AttachmentReference>();
            foreach (var input in node.input) {
                if (!attachments.Contains(input.attachment)) throw new RenderGraphException("Attachment that is not part of this RenderGraph referenced");
                desc.inputAttachments.Add(new AttachmentReference { attachment = (uint)attachments.IndexOf(input.attachment), layout = input.layout });
            }

            desc.colorAttachments = new List<AttachmentReference>();
            foreach (var color in node.color) {
                if (!attachments.Contains(color.attachment)) throw new RenderGraphException("Attachment that is not part of this RenderGraph referenced");
                desc.colorAttachments.Add(new AttachmentReference { attachment = (uint)attachments.IndexOf(color.attachment), layout = color.layout });
            }

            desc.resolveAttachments = new List<AttachmentReference>();
            foreach (var resolve in node.resolve) {
                if (!attachments.Contains(resolve.attachment)) throw new RenderGraphException("Attachment that is not part of this RenderGraph referenced");
                desc.resolveAttachments.Add(new AttachmentReference { attachment = (uint)attachments.IndexOf(resolve.attachment), layout = resolve.layout });
            }

            desc.preserveAttachments = new List<uint>();
            foreach (var preserve in node.preserve) {
                if (!attachments.Contains(preserve)) throw new RenderGraphException("Attachment that is not part of this RenderGraph referenced");
                desc.preserveAttachments.Add((uint)attachments.IndexOf(preserve));
            }

            if (!attachments.Contains(node.DepthStencil)) throw new RenderGraphException("Attachment that is not part of this RenderGraph referenced");
            desc.depthStencilAttachment = new AttachmentReference { attachment = (uint)attachments.IndexOf(node.DepthStencil), layout = node.DepthStencilLayout };

            foreach (var dependency in node.dependencies) {
                var subpassDependency = dependency.dependency;

                if (dependency.source != null) {
                    subpassDependency.srcSubpass = (uint)subpasses.IndexOf(nodeMap[dependency.source]);
                } else {
                    subpassDependency.srcSubpass = uint.MaxValue;
                }

                if (dependency.dest != null) {
                    subpassDependency.dstSubpass = (uint)subpasses.IndexOf(nodeMap[node]);
                } else {
                    subpassDependency.dstSubpass = uint.MaxValue;
                }

                dependencies.Add(subpassDependency);
            }
        }

        public void AddAttachment(AttachmentDescription attachment) {
            if (attachment == null) throw new ArgumentNullException(nameof(attachment));
            if (attachments.Contains(attachment)) return;

            attachments.Add(attachment);
        }

        public void RemoveAttachment(AttachmentDescription attachment) {
            attachments.Remove(attachment);
        }

        public void AddNode(RenderNode node) {
            if (node == null) throw new ArgumentNullException(nameof(node));
            
            nodeMap.Add(node, new SubpassDescription());
        }

        public void RemoveNode(RenderNode node) {
            nodeMap.Remove(node);
        }

        public void Render(RenderPassBeginInfo beginInfo, CommandBuffer commandBuffer) {
            beginInfo.renderPass = RenderPass;

            commandBuffer.BeginRenderPass(beginInfo, VkSubpassContents.SecondaryCommandBuffers);

            for (int i = 0; i < nodes.Count; i++) {
                commandBuffer.Execute(nodes[i].GetCommands());
                if (i < nodes.Count - 1) commandBuffer.NextSubpass(VkSubpassContents.SecondaryCommandBuffers);
            }

            commandBuffer.EndRenderPass();
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposing) {
            if (disposed) return;

            RenderPass?.Dispose();

            disposed = true;
        }

        ~RenderGraph() {
            Dispose(false);
        }
    }

    public class RenderGraphException : Exception {
        public RenderGraphException(string message) : base(message) { }
    }
}
