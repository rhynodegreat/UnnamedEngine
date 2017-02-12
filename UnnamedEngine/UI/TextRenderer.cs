using System;
using System.Collections.Generic;

using CSGL.Vulkan;

using UnnamedEngine.Core;

namespace UnnamedEngine.UI {
    public class TextRenderer : CommandNode {
        bool disposed;
        Engine engine;
        Device device;

        CommandPool commandPool;
        CommandBuffer commandBuffer;

        public TextRenderer(Engine engine) : base(engine.Graphics.Device) {
            if (engine == null) throw new ArgumentNullException(nameof(engine));
            device = engine.Graphics.Device;

            CreateCommandPool();
        }

        void CreateCommandPool() {
            CommandPoolCreateInfo poolInfo = new CommandPoolCreateInfo();
            poolInfo.queueFamilyIndex = engine.Graphics.GraphicsQueue.FamilyIndex;
            poolInfo.flags = VkCommandPoolCreateFlags.ResetCommandBufferBit;

            commandPool = new CommandPool(engine.Graphics.Device, poolInfo);

            commandBuffer = commandPool.Allocate(VkCommandBufferLevel.Primary);
        }

        public override CommandBuffer GetCommands() {
            return commandBuffer;
        }

        protected override void OnBake() {
            RecordCommands();
        }

        void RecordCommands() {
            CommandBufferBeginInfo beginInfo = new CommandBufferBeginInfo();
            commandBuffer.Begin(beginInfo);

            commandBuffer.End();
        }

        public new void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected override void Dispose(bool disposing) {
            if (disposed) return;
            base.Dispose(disposing);

            commandPool.Dispose();

            disposed = true;
        }
    }
}
