using System;
using System.Collections.Generic;

using CSGL.GLFW;

using UnnamedEngine.Rendering;

namespace UnnamedEngine.Core {
    public class Engine : IDisposable {
        bool disposed;

        Window window;
        Camera camera;

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

        public Camera Camera {
            get {
                return camera;
            }
            set {
                if (value == null) throw new ArgumentNullException(nameof(Camera));
                camera = value;
            }
        }

        public CommandGraph CommandGraph { get; private set; }

        public Engine(Renderer renderer) {
            if (renderer == null) throw new ArgumentNullException(nameof(renderer));

            Renderer = renderer;
            CommandGraph = new CommandGraph(this);
        }

        public void Run() {
            if (Window == null) throw new EngineException("Window not set");
            if (Camera == null) throw new EngineException("Camera not set");

            while (true) {
                Renderer.Allocator.ResetTemp();
                GLFW.PollEvents();

                if (Window.ShouldClose) break;

                Camera.Update();

                CommandGraph.Render();
            }

            Renderer.Device.WaitIdle();
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposing) {
            if (disposed) return;

            if (disposing) {
                CommandGraph.Dispose();
                Window.Dispose();
                Renderer.Dispose();
            }

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
