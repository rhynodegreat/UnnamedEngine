using System;
using System.Collections.Generic;

using CSGL.Vulkan;

namespace UnnamedEngine.Rendering {
    public abstract class RenderNode {
        internal List<AttachmentPair> input;
        internal List<AttachmentPair> color;
        internal List<AttachmentPair> resolve;
        internal List<AttachmentDescription> preserve;
        internal List<Dependency> dependencies;

        public AttachmentDescription DepthStencil { get; set; }
        public VkImageLayout DepthStencilLayout { get; set; }

        public RenderNode() {
            input = new List<AttachmentPair>();
            color = new List<AttachmentPair>();
            resolve = new List<AttachmentPair>();
            preserve = new List<AttachmentDescription>();

            dependencies = new List<Dependency>();
        }

        public void AddInput(AttachmentDescription attachment, VkImageLayout layout) {
            if (attachment == null) throw new ArgumentNullException(nameof(attachment));

            for (int i = 0; i < input.Count; i++) {
                var pair = input[i];
                if (pair.attachment == attachment) {
                    pair.layout = layout;
                    input[i] = pair;
                    return;
                }
            }

            input.Add(new AttachmentPair { attachment = attachment, layout = layout });
        }

        public void RemoveInput(AttachmentDescription attachment) {
            for (int i = 0; i < input.Count; i++) {
                if (input[i].attachment == attachment) {
                    input.RemoveAt(i);
                    return;
                }
            }
        }

        public void AddColor(AttachmentDescription attachment, VkImageLayout layout) {
            if (attachment == null) throw new ArgumentNullException(nameof(attachment));

            for (int i = 0; i < color.Count; i++) {
                var pair = color[i];
                if (pair.attachment == attachment) {
                    pair.layout = layout;
                    color[i] = pair;
                    return;
                }
            }

            color.Add(new AttachmentPair { attachment = attachment, layout = layout });
        }

        public void RemoveColor(AttachmentDescription attachment) {
            for (int i = 0; i < color.Count; i++) {
                if (color[i].attachment == attachment) {
                    color.RemoveAt(i);
                    return;
                }
            }
        }

        public void AddResolve(AttachmentDescription attachment, VkImageLayout layout) {
            if (attachment == null) throw new ArgumentNullException(nameof(attachment));

            for (int i = 0; i < resolve.Count; i++) {
                var pair = resolve[i];
                if (pair.attachment == attachment) {
                    pair.layout = layout;
                    resolve[i] = pair;
                    return;
                }
            }

            resolve.Add(new AttachmentPair { attachment = attachment, layout = layout });
        }

        public void RemoveResolve(AttachmentDescription attachment) {
            for (int i = 0; i < resolve.Count; i++) {
                if (resolve[i].attachment == attachment) {
                    resolve.RemoveAt(i);
                    return;
                }
            }
        }

        public void AddPreserve(AttachmentDescription attachment) {
            if (attachment == null) throw new ArgumentNullException(nameof(attachment));
            if (preserve.Contains(attachment)) return;

            preserve.Add(attachment);
        }

        public void RemovePreserce(AttachmentDescription attachment) {
            preserve.Remove(attachment);
        }

        public void AddSource(RenderNode source, SubpassDependency dependency) {
            if (dependency == null) throw new ArgumentNullException(nameof(dependency));

            for (int i = 0; i < dependencies.Count; i++) {
                var dep = dependencies[i];
                if (dep.dependency == dependency) {
                    dep.source = source;
                    dependencies[i] = dep;
                    return;
                }
            }

            dependencies.Add(new Dependency { source = source, dest = this, dependency = dependency });
        }

        public void AddDest(RenderNode dest, SubpassDependency dependency) {
            if (dependency == null) throw new ArgumentNullException(nameof(dependency));

            for (int i = 0; i < dependencies.Count; i++) {
                var dep = dependencies[i];
                if (dep.dependency == dependency) {
                    dep.dest = dest;
                    dependencies[i] = dep;
                    return;
                }
            }

            dependencies.Add(new Dependency { source = this, dest = dest, dependency = dependency });
        }

        public void RemoveDependency(SubpassDependency dependency) {
            for (int i = 0; i < dependencies.Count; i++) {
                if (dependencies[i].dependency == dependency) {
                    dependencies.RemoveAt(i);
                    return;
                }
            }
        }

        public abstract List<CommandBuffer> GetCommands();

        internal struct AttachmentPair {
            public AttachmentDescription attachment;
            public VkImageLayout layout;
        }

        internal struct Dependency {
            public RenderNode source;
            public RenderNode dest;
            public SubpassDependency dependency;
        }
    }
}
