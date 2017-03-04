using System;
using System.Collections.Generic;

using CSGL.Vulkan;

using UnnamedEngine.Core;

namespace UnnamedEngine.UI {
    public class PanelRenderer : IRenderer {
        bool disposed;

        public void Render(CommandBuffer commandBuffer) {

        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposing) {
            if (disposed) return;

            disposed = true;
        }

        ~PanelRenderer() {
            Dispose(false);
        }
    }
}
