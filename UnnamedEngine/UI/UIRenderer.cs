using System;
using System.Collections.Generic;

using CSGL.Vulkan;

using UnnamedEngine.Rendering;
using UnnamedEngine.ECS;

namespace UnnamedEngine.UI {
    public interface UIRenderer : IDisposable {
        void PreRenderElement(Entity e, Transform transform, UIElement element);
        void PreRender();
        void Render(CommandBuffer commandBuffer, Entity e, Transform transform, UIElement element);
    }
}
