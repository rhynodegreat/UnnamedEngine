using System;
using System.Collections.Generic;

using CSGL.Vulkan;

namespace UnnamedEngine.Core {
    public abstract class QueueNode : IDisposable {
        bool disposed;
        protected Device device;
        List<QueueNode> input;
        List<QueueNode> output;
        List<CommandBuffer> internalCommands;
        Queue<CommandBuffer> oneTimeCommands;

        public Queue Queue { get; private set; }
        public IList<QueueNode> Input { get; private set; }
        public IList<QueueNode> Output { get; private set; }
        public VkPipelineStageFlags SignalStage { get; protected set; }

        internal struct WaitPair {
            public Semaphore semaphore;
            public VkPipelineStageFlags flags;

            public WaitPair(Semaphore semaphore, VkPipelineStageFlags flags) {
                this.semaphore = semaphore;
                this.flags = flags;
            }
        }

        internal List<WaitPair> ExtraInput { get; private set; }
        internal List<Semaphore> ExtraOutput { get; private set; }

        protected QueueNode(Device device, Queue queue, VkPipelineStageFlags stageFlags) {
            this.device = device;
            Queue = queue;
            SignalStage = stageFlags;
            input = new List<QueueNode>();
            output = new List<QueueNode>();
            Input = input.AsReadOnly();
            Output = output.AsReadOnly();
            ExtraInput = new List<WaitPair>();
            ExtraOutput = new List<Semaphore>();
            internalCommands = new List<CommandBuffer>();
            oneTimeCommands = new Queue<CommandBuffer>();
        }

        public abstract List<CommandBuffer> GetCommands();
        
        public virtual void PreSubmit() { }
        public virtual void PostSubmit() { }

        internal List<CommandBuffer> InternalGetCommands() {
            internalCommands.Clear();
            lock (oneTimeCommands) {
                while (oneTimeCommands.Count > 0) internalCommands.Add(oneTimeCommands.Dequeue());
            }

            List<CommandBuffer> derivedCommands = GetCommands();

            for (int i = 0; i < derivedCommands.Count; i++) {
                internalCommands.Add(derivedCommands[i]);
            }

            return internalCommands;
        }

        public void SubmitOnce(CommandBuffer commandBuffer) {
            lock (oneTimeCommands) {
                oneTimeCommands.Enqueue(commandBuffer);
            }
        }

        public void AddInput(QueueNode other) {
            if (input.Contains(other)) return;
            input.Add(other);
            other.output.Add(this);
        }

        public void RemoveInput(QueueNode other) {
            if (input.Contains(other)) {
                input.Remove(other);
            }
        }

        public void AddInput(Semaphore semaphore, VkPipelineStageFlags waitFlags) {
            for (int i = 0; i < ExtraInput.Count; i++) {
                if (ExtraInput[i].semaphore == semaphore) return;
            }

            ExtraInput.Add(new WaitPair(semaphore, waitFlags));
        }

        public void RemoveInput(Semaphore semaphore) {
            for (int i = 0; i < ExtraInput.Count; i++) {
                if (ExtraInput[i].semaphore == semaphore) {
                    ExtraInput.RemoveAt(i);
                    break;
                }
            }
        }

        public void AddOutput(Semaphore semaphore) {
            for (int i = 0; i < ExtraOutput.Count; i++) {
                if (ExtraOutput[i] == semaphore) return;
            }

            ExtraOutput.Add(semaphore);
        }

        public void RemoveOutput(Semaphore semaphore) {
            ExtraOutput.Remove(semaphore);
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            //nothing
        }
    }

    public class RenderNodeException : Exception {
        public RenderNodeException(string message) : base(message) { }
        public RenderNodeException(string format, params object[] args) : base(string.Format(format, args)) { }
    }
}
