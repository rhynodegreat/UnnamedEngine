using System;
using System.Collections.Generic;
using CSGL.Vulkan1;

namespace UnnamedEngine.Core {
    public class SubmitNode : QueueNode {
        Engine engine;

        List<CommandBuffer> commandBuffers;

        public SubmitNode(Engine engine, Queue queue) : base(engine.Graphics.Device, queue, VkPipelineStageFlags.BottomOfPipeBit) {
            if (engine == null) throw new ArgumentNullException(nameof(engine));

            this.engine = engine;

            commandBuffers = new List<CommandBuffer>();
        }

        public override List<CommandBuffer> GetCommands() {
            return commandBuffers;
        }

        public override void PostSubmit() {
            commandBuffers.Clear();
        }

        public void SubmitOnce(CommandBuffer commandBuffer) {
            if (commandBuffer == null) throw new ArgumentNullException(nameof(commandBuffer));

            commandBuffers.Add(commandBuffer);
        }
    }
}
