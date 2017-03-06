using System;
using System.Collections.Generic;

using CSGL.Vulkan;

using UnnamedEngine.ECS;
using UnnamedEngine.Rendering;

namespace UnnamedEngine.UI {
    public class LabelRenderer : UIRenderer {
        bool disposed;

        public void PreRender(Entity e, Transform transform, UIElement element) {

        }

        public void Render(CommandBuffer commandbuffer, Entity e, Transform transform, UIElement element) {

        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposing) {
            if (disposed) return;

            disposed = true;
        }

        ~LabelRenderer() {

        }
    }
}
