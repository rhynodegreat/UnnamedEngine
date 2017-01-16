using System;
using System.Collections.Generic;

using CSGL.Vulkan;

namespace UnnamedEngine.Rendering {
    public abstract class RenderNode : IDisposable {
        bool disposed;
        List<RenderNode> input;
        List<RenderNode> output;
        public IList<RenderNode> Input { get; private set; }
        public IList<RenderNode> Output { get; private set; }
        public Semaphore SignalSemaphore { get; private set; }
        public VkPipelineStageFlags SignalStage { get; protected set; }

        protected RenderNode(Device device, VkPipelineStageFlags stageFlags) {
            SignalSemaphore = new Semaphore(device);
            SignalStage = stageFlags;
            input = new List<RenderNode>();
            output = new List<RenderNode>();
            Input = input.AsReadOnly();
            Output = output.AsReadOnly();
        }

        public abstract CommandBuffer[] GetCommands(out int count);

        public virtual void OnBake(RenderGraph graph) { }
        public virtual void PreRender() { }
        public virtual void PostRender() { }

        public void AddInput(RenderNode node) {
            if (input.Contains(node)) return;
            input.Add(node);
            node.output.Add(this);
        }

        public void RemoveInput(RenderNode node) {
            if (input.Remove(node)) {
                node.output.Remove(this);
            }
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (disposed) return;

            if (disposing) {
                SignalSemaphore.Dispose();
            }

            disposed = true;
        }

        ~RenderNode() {
            Dispose(false);
        }
    }

    public class RenderNodeException : Exception {
        public RenderNodeException(string message) : base(message) { }
        public RenderNodeException(string format, params object[] args) : base(string.Format(format, args)) { }
    }
}
