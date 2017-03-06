using System;
using System.Collections.Generic;

using CSGL.Vulkan;

using UnnamedEngine.ECS;

namespace UnnamedEngine.UI {
    public interface UIRenderer : IDisposable{
        void PreRender(Entity e);
        void Render(CommandBuffer commandbuffer, Entity e);
    }
}
