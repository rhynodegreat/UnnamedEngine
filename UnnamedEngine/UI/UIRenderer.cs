using System;
using System.Collections.Generic;

using CSGL.Vulkan1;

using UnnamedEngine.Rendering;
using UnnamedEngine.ECS;

namespace UnnamedEngine.UI {
    public interface UIRenderer : IDisposable {
        void PreRenderElement(UIElement element);
        void PreRender();
        void Render(CommandBuffer commandBuffer, UIElement element);
    }
}
