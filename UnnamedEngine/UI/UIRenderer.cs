using System;
using System.Collections.Generic;

using CSGL.Vulkan;

namespace UnnamedEngine.UI {
    public abstract class UIRenderer : IDisposable {
        public abstract void Render(CommandBuffer commandbuffer);

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) { }

        ~UIRenderer() {
            Dispose(false);
        }
    }
}
