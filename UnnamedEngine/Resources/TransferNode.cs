using System;
using System.Collections.Generic;

using CSGL.Vulkan;
using Buffer = CSGL.Vulkan.Buffer;

using UnnamedEngine.Core;

namespace UnnamedEngine.Resources {
    public abstract class TransferNode : CommandNode {
        protected Device device;

        protected TransferNode(Device device, VkPipelineStageFlags flags) : base(device, flags) {

        }

        public abstract void Transfer<T>(T[] data, Buffer buffer, VkBufferUsageFlags usage) where T : struct;
    }
}
