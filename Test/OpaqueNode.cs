using System;
using System.Collections.Generic;

using CSGL.Vulkan;

using UnnamedEngine.Rendering;

namespace Test {
    public class OpaqueNode : RenderNode {
        RenderPass renderPass;
        uint subpassIndex;
        CommandPool pool;

        List<IRenderer> renderers;

        List<CommandBuffer> submitBuffers;

        public OpaqueNode(CommandPool pool) {
            this.pool = pool;

            submitBuffers = new List<CommandBuffer>();
            renderers = new List<IRenderer>();

            Dirty = true;
        }

        public void AddRenderer(IRenderer renderer) {
            renderers.Add(renderer);
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
    }
}
