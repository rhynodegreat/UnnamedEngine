using System;
using System.Collections.Generic;

using CSGL.Vulkan;

namespace UnnamedEngine.Core {
    public abstract class CommandNode : IDisposable {
        bool disposed;
        List<CommandNode> input;
        List<CommandNode> output;
        List<Event> waitEvents;

        public IList<CommandNode> Input { get; private set; }
        public IList<CommandNode> Output { get; private set; }

        public List<MemoryBarrier> MemoryBarriers { get; set; }
        public List<BufferMemoryBarrier> BufferMemoryBarriers { get; set; }
        public List<ImageMemoryBarrier> ImageMemoryBarriers { get; set; }

        public VkPipelineStageFlags SrcStage { get; set; }
        public VkPipelineStageFlags DstStage { get; set; }

        public Event Event { get; private set; }
        public VkPipelineStageFlags EventStage { get; set; }

        protected CommandNode(Device device) {
            Event = new Event(device);
            input = new List<CommandNode>();
            output = new List<CommandNode>();
            Input = input.AsReadOnly();
            Output = output.AsReadOnly();
            waitEvents = new List<Event>();
        }

        protected abstract void OnBake();
        public abstract CommandBuffer GetCommands();

        public void WaitEvents(CommandBuffer commandBuffer) {
            if (waitEvents.Count > 0) commandBuffer.WaitEvents(waitEvents, SrcStage, DstStage, MemoryBarriers, BufferMemoryBarriers, ImageMemoryBarriers);
        }

        public void SetEvents(CommandBuffer commandBuffer) {
            commandBuffer.ResetEvent(Event, EventStage);
            commandBuffer.SetEvent(Event, EventStage);
        }

        public void AddInput(CommandNode node) {
            if (node == null) throw new ArgumentNullException(nameof(node));
            if (input.Contains(node)) return;
            input.Add(node);
            node.output.Add(this);
        }

        public void RemoveInput(CommandNode node) {
            if (input.Remove(node)) {
                node.output.Remove(this);
            }
        }

        internal void Bake() {
            waitEvents.Clear();
            foreach (var node in input) {
                waitEvents.Add(node.Event);
            }
            OnBake();
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (disposed) return;

            Event.Dispose();

            disposed = true;
        }

        ~CommandNode() {
            Dispose(false);
        }
    }
}
