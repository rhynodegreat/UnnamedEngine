using System;
using System.Collections.Generic;

using CSGL.Vulkan;

using UnnamedEngine.Rendering;

namespace Test {
    public class OpaqueNode : RenderNode {
        RenderPass renderPass;
        uint subpassIndex;

        List<CommandBuffer> submitBuffers;

        public OpaqueNode(CommandPool pool) {
            submitBuffers = new List<CommandBuffer>();
        }

        protected override void Bake(RenderPass renderPass, uint subpassIndex) {
            this.renderPass = renderPass;
            this.subpassIndex = subpassIndex;
        }

        public override List<CommandBuffer> GetCommands() {
            return submitBuffers;
        }
    }
}
