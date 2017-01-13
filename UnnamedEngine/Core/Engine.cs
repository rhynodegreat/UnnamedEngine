using System;
using System.Collections.Generic;

using CSGL.GLFW;

namespace UnnamedEngine.Core {
    public class Engine : IDisposable {
        bool disposed;

        Window window;

        public Renderer Renderer { get; private set; }

        public Window Window {
            get {
                return window;
            }
            set {
                if (value == null) throw new ArgumentNullException(nameof(Window));
                window = value;
            }
        }

        public Engine(Renderer renderer) {
            if (renderer == null) throw new ArgumentNullException(nameof(renderer));

            Renderer = renderer;
        }

        public void Run() {
            if (Renderer == null) throw new EngineException("Renderer not set");
            if (Window == null) throw new EngineException("Window not set");

            while (true) {
                GLFW.PollEvents();

                if (Window.ShouldClose) break;
            }
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposing) {
            if (disposed) return;

            if (disposing) {
                Window.Dispose();
                Renderer.Dispose();
            }

            Renderer = null;

            disposed = true;
        }

        ~Engine() {
            Dispose(false);
        }
    }

    public class EngineException : Exception {
        public EngineException(string message) : base(message) { }
        public EngineException(string format, params object[] args) : base(string.Format(format, args)) { }
    }
}
