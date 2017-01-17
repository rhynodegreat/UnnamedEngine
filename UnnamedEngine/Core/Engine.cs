using System;
using System.Collections.Generic;

using CSGL.GLFW;

using UnnamedEngine.Rendering;

namespace UnnamedEngine.Core {
    public class Engine : IDisposable {
        bool disposed;

        Window window;
        RenderGraph renderGraph;

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

        public RenderGraph RenderGraph {
            get {
                return renderGraph;
            }
            set {
                if (value == null) throw new ArgumentNullException(nameof(RenderGraph));
                renderGraph = value;
            }
        }

        public Engine(Renderer renderer) {
            if (renderer == null) throw new ArgumentNullException(nameof(renderer));

            Renderer = renderer;
        }

        public void Run() {
            if (Window == null) throw new EngineException("Window not set");
            if (RenderGraph == null) throw new EngineException("Render Graph not set");

            while (true) {
                GLFW.PollEvents();

                if (Window.ShouldClose) break;

                RenderGraph.Render();
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
                RenderGraph.Dispose();
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
