using System;
using System.Collections.Generic;

using CSGL.Vulkan;

using UnnamedEngine.Rendering;

namespace Test {
    public class OpaqueNode : RenderNode {
        RenderPass renderPass;
        uint subpassIndex;
        CommandPool pool;

        List<CommandBuffer> submitBuffers;

        public OpaqueNode(CommandPool pool) {
            this.pool = pool;
        }

        public void Init(Framebuffer framebuffer) {
            CommandBuffer commandBuffer = pool.Allocate(VkCommandBufferLevel.Secondary);

            CommandBufferInheritanceInfo inheritance = new CommandBufferInheritanceInfo();
            inheritance.renderPass = renderPass;
            inheritance.subpass = subpassIndex;
            inheritance.framebuffer = framebuffer;

            CommandBufferBeginInfo beginInfo = new CommandBufferBeginInfo();
            beginInfo.flags = VkCommandBufferUsageFlags.RenderPassContinueBit | VkCommandBufferUsageFlags.SimultaneousUseBit;
            beginInfo.inheritanceInfo = inheritance;

            commandBuffer.Begin(beginInfo);

            commandBuffer.End();

            submitBuffers = new List<CommandBuffer> { commandBuffer };
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
