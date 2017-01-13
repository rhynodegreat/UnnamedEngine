using System;
using System.Collections.Generic;

using CSGL.GLFW;

namespace UnnamedEngine.Core {
    public class Window : IDisposable {
        WindowPtr window;
        bool disposed;

        public bool ShouldClose {
            get {
                return GLFW.WindowShouldClose(window);
            }
        }

        public Window(Engine engine, int width, int height, string title) {
            GLFW.WindowHint(WindowHint.ClientAPI, (int)ClientAPI.NoAPI);
            window = GLFW.CreateWindow(width, height, title, MonitorPtr.Null, WindowPtr.Null);
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposing) {
            if (disposed) return;

            GLFW.DestroyWindow(window);

            disposed = true;
        }

        ~Window() {
            Dispose(false);
        }
    }
}
