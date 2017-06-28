using System;
using System.Collections.Generic;

using CSGL.Vulkan1;

namespace Test {
    public interface IRenderer : IDisposable {
        CommandBuffer GetCommandBuffer();
    }
}
