using System;
using System.Collections.Generic;

using CSGL.Vulkan;
using UnnamedEngine.Core;

namespace UnnamedEngine.Rendering {
    public class AcquireImageNode : RenderNode {
        Renderer renderer;
        Swapchain swapchain;

        uint imageIndex;
        public uint ImageIndex {
            get {
                return imageIndex;
            }
        }

        public AcquireImageNode(Engine engine) : base(engine.Renderer.Device, VkPipelineStageFlags.TopOfPipeBit) {
            if (engine == null) throw new ArgumentNullException(nameof(engine));
            renderer = engine.Renderer;
            swapchain = engine.Window.Swapchain;
        }

        public override void PreRender() {
            swapchain.AcquireNextImage(ulong.MaxValue, SignalSemaphore, out imageIndex);
        }

        public override CommandBuffer[] GetCommands(out int count) {
            count = -1;
            return null;
        }
    }
}
