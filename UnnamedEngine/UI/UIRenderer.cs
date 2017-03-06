using System;
using System.Collections.Generic;

using CSGL.Vulkan;

using UnnamedEngine.Rendering;
using UnnamedEngine.ECS;

namespace UnnamedEngine.UI {
    public interface UIRenderer : IDisposable {
        void PreRender(Entity e, Transform transform, UIElement element);
        void Render(CommandBuffer commandbuffer, Entity e, Transform transform, UIElement element);
    }
}
