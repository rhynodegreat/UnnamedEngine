using System;
using System.Collections.Generic;

using CSGL.Graphics;
using CSGL.Vulkan;

using UnnamedEngine.Core;

namespace UnnamedEngine.UI.Text {
    public class FontCache : IDisposable {
        bool disposed;

        Engine engine;

        public FontCache(Engine engine) {
            if (engine == null) throw new ArgumentNullException(nameof(engine));

            this.engine = engine;
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposing) {
            if (disposed) return;

            disposed = true;
        }

        ~FontCache() {
            Dispose(false);
        }
    }
}
