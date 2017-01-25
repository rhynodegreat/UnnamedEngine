using System;
using System.Collections.Generic;

using CSGL.Vulkan;
using Buffer = CSGL.Vulkan.Buffer;

using UnnamedEngine.Core;

namespace UnnamedEngine.Resources {
    public abstract class TransferNode : CommandNode {
        protected TransferNode(Device device, VkPipelineStageFlags flags) : base(device, flags) {
            SignalStage = flags;
        }

        public abstract void Transfer<T>(T[] data, Buffer buffer) where T : struct;
    }

    public class TransferException : Exception {
        public TransferException(string message) : base(message) { }
        public TransferException(string format, params object[] args) : base(string.Format(format, args)) { }
    }
}
