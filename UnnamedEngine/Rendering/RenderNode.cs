using System;
using System.Collections.Generic;

using CSGL.Vulkan;

namespace UnnamedEngine.Rendering {
    public abstract class RenderNode {
        public IEnumerable<RenderNode> Input { get; protected set; }
        public IEnumerable<RenderNode> Output { get; protected set; }
        public Semaphore SignalSemaphore { get; protected set; }
        public Semaphore WaitSemaphore { get; protected set; }

        protected abstract void AddInput(RenderNode node);
        protected abstract void RemoveInput(RenderNode node);

        protected abstract void AddOutput(RenderNode node);
        protected abstract void RemoveOutput(RenderNode node);

        public abstract CommandBuffer GetCommandBuffer();

        public static void Compose(RenderNode a, RenderNode b) {
            a.AddOutput(b);
            b.AddInput(a);
        }

        public static void Uncompose(RenderNode a, RenderNode b) {
            a.RemoveOutput(b);
            b.RemoveInput(a);
        }
    }
}
