using System;
using System.Collections.Generic;

namespace UnnamedEngine {
    public class Engine : IDisposable {
        bool disposed;

        public Engine() {

        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposing) {
            if (disposed) return;

            disposed = true;
        }

        ~Engine() {
            Dispose(false);
        }
    }
}
