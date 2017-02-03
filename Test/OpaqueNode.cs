using System;
using System.Collections.Generic;

using CSGL.Vulkan;

using UnnamedEngine.Rendering;

namespace Test {
    public class OpaqueNode : RenderNode {
        bool disposed;

        RenderPass renderPass;
        uint subpassIndex;
        CommandPool pool;

        List<ISubpass> renderers;

        List<CommandBuffer> submitBuffers;

        public OpaqueNode(CommandPool pool) {
            this.pool = pool;

            submitBuffers = new List<CommandBuffer>();
            renderers = new List<ISubpass>();

            Dirty = true;
        }

        public void AddRenderer(ISubpass renderer) {
            renderers.Add(renderer);
            renderer.Bake(renderPass, subpassIndex);
        }

        protected override void Bake(RenderPass renderPass, uint subpassIndex) {
            this.renderPass = renderPass;
            this.subpassIndex = subpassIndex;
        }

        public override List<CommandBuffer> GetCommands() {
            submitBuffers.Clear();

            for (int i = 0; i < renderers.Count; i++) {
                submitBuffers.Add(renderers[i].GetCommandBuffer());
            }

            return submitBuffers;
        }

        public new void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected override void Dispose(bool disposing) {
            if (disposed) return;

            if (disposing) {
                foreach (var renderer in renderers) {
                    renderer.Dispose();
                }
            }

            base.Dispose(disposing);

            disposed = true;
        }

        ~OpaqueNode() {

        }
    }
}
