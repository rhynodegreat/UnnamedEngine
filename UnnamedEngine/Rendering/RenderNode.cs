using System;
using System.Collections.Generic;

using CSGL.Vulkan;

namespace UnnamedEngine.Rendering {
    public abstract class RenderNode {
        public IEnumerable<RenderNode> Input { get; protected set; }
        public Semaphore SignalSemaphore { get; protected set; }

        public abstract void AddInput(RenderNode node);
        public abstract void RemoveInput(RenderNode node);

        public abstract SubmitInfo GetSubmitInfo();
    }

    public class RenderNodeException : Exception {
        public RenderNodeException(string message) : base(message) { }
        public RenderNodeException(string format, params object[] args) : base(string.Format(format, args)) { }
    }
}
