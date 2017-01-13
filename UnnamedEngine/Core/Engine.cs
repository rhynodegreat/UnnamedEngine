using System;
using System.Collections.Generic;

namespace UnnamedEngine.Core {
    public class Engine : IDisposable {
        bool disposed;

        public Renderer Renderer { get; private set; }

        public Engine(Renderer renderer) {
            if (renderer == null) throw new ArgumentNullException(nameof(renderer));

            Renderer = renderer;
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposing) {
            if (disposed) return;

            if (disposing) {
                Renderer.Dispose();
            }

            Renderer = null;

            disposed = true;
        }

        ~Engine() {
            Dispose(false);
        }
    }
}
