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
        internal SubmitInfo SubmitInfo { get; private set; }
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
        
        public virtual void PreRender() { }
        public virtual void PostRender() { }

        public void AddInput(RenderNode other) {
            if (input.Contains(other)) return;
            input.Add(other);
            other.output.Add(this);

            var sem = new Semaphore(device);
            other.SubmitInfo.signalSemaphores.Add(sem);
            AddInput(sem, other.SignalStage);
            semaphoreMap.Add(other, sem);
        }

        public void RemoveInput(RenderNode other) {
            if (input.Contains(other)) {
                input.Remove(other);
                int index = other.SubmitInfo.signalSemaphores.IndexOf(semaphoreMap[other]);
                other.SubmitInfo.signalSemaphores.RemoveAt(index);
                RemoveInput(semaphoreMap[other]);
                semaphoreMap.Remove(other);
            }
        }

        public void AddInput(Semaphore semaphore, VkPipelineStageFlags waitFlags) {
            if (SubmitInfo.waitSemaphores.Contains(semaphore)) return;
            SubmitInfo.waitSemaphores.Add(semaphore);
            SubmitInfo.waitDstStageMask.Add(waitFlags);
        }

        public void RemoveInput(Semaphore semaphore) {
            if (SubmitInfo.waitSemaphores.Contains(semaphore)) {
                int index = SubmitInfo.waitSemaphores.IndexOf(semaphore);
                SubmitInfo.waitSemaphores.RemoveAt(index);
                SubmitInfo.waitDstStageMask.RemoveAt(index);
            }
        }

        public void AddOutput(Semaphore semaphore) {
            if (SubmitInfo.signalSemaphores.Contains(semaphore)) return;
            SubmitInfo.signalSemaphores.Add(semaphore);
        }

        public void RemoveOutput(Semaphore semaphore) {
            SubmitInfo.signalSemaphores.Remove(semaphore);
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (disposed) return;
            
            foreach (var sem in semaphoreMap.Values) {
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
