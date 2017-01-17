using System;
using System.Collections.Generic;

using CSGL.Vulkan;

namespace UnnamedEngine.Rendering {
    public abstract class RenderNode : IDisposable {
        bool disposed;
        Device device;
        List<RenderNode> input;
        List<RenderNode> output;

        public IList<RenderNode> Input { get; private set; }
        public IList<RenderNode> Output { get; private set; }
        public VkPipelineStageFlags SignalStage { get; protected set; }
        public SubmitInfo SubmitInfo { get; private set; }
        public Dictionary<RenderNode, Semaphore> semaphoreMap;

        protected RenderNode(Device device, VkPipelineStageFlags stageFlags) {
            this.device = device;
            SignalStage = stageFlags;
            input = new List<RenderNode>();
            output = new List<RenderNode>();
            Input = input.AsReadOnly();
            Output = output.AsReadOnly();
            semaphoreMap = new Dictionary<RenderNode, Semaphore>();

            SubmitInfo = new SubmitInfo();
            SubmitInfo.commandBuffers = new List<CommandBuffer>();
            SubmitInfo.signalSemaphores = new List<Semaphore>();
            SubmitInfo.waitDstStageMask = new List<VkPipelineStageFlags>();
            SubmitInfo.waitSemaphores = new List<Semaphore>();
        }

        public abstract List<CommandBuffer> GetCommands();

        public virtual void OnBake(RenderGraph graph) { }
        public virtual void PreRender() { }
        public virtual void PostRender() { }

        public void AddInput(RenderNode other) {
            if (input.Contains(other)) return;
            input.Add(other);
            other.output.Add(this);

            var sem = new Semaphore(device);
            other.SubmitInfo.signalSemaphores.Add(sem);
            SubmitInfo.waitSemaphores.Add(sem);
            SubmitInfo.waitDstStageMask.Add(other.SignalStage);
            semaphoreMap.Add(other, sem);
        }

        public void RemoveInput(RenderNode other) {
            if (input.Contains(other)) {
                input.Remove(other);
                int index = other.SubmitInfo.signalSemaphores.IndexOf(semaphoreMap[other]);
                other.SubmitInfo.signalSemaphores.RemoveAt(index);
                SubmitInfo.waitSemaphores.RemoveAt(index);
                semaphoreMap.Remove(other);
            }
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (disposed) return;
            
            foreach (var sem in SubmitInfo.signalSemaphores) {
                sem.Dispose();
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
