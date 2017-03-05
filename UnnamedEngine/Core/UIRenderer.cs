using System;
using System.Collections.Generic;

using CSGL.Vulkan;

namespace UnnamedEngine.Core {
    public interface IRenderer : IDisposable {
        void PreRender();
        void Render(CommandBuffer commandbuffer);
    }
}
