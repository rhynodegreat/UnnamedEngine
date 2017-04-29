using System;
using System.Collections.Generic;
using System.Numerics;

using CSGL.GLFW;
using CSGL.Input;

using UnnamedEngine.Rendering;

namespace UnnamedEngine.Core {
    public class Engine : IDisposable {
        bool disposed;

        Window window;

        public Graphics Graphics { get; private set; }
        public QueueGraph QueueGraph { get; private set; }
        public FrameLoop FrameLoop { get; private set; }
        public Clock Clock { get; private set; }
        public CameraManager Cameras { get; private set; }
        public Memory Memory { get; private set; }

        public Window Window {
            get {
                return window;
            }
            set {
                if (value == null) throw new ArgumentNullException(nameof(Window));
                window = value;
            }
        }


        public Engine(Graphics graphics) {
            if (graphics == null) throw new ArgumentNullException(nameof(graphics));
            
            Graphics = graphics;
            Memory = new Memory(this);

            QueueGraph = new QueueGraph(this);
            FrameLoop = new FrameLoop();
            Clock = new Clock();
            Cameras = new CameraManager(this);

            QueueGraph.Add(Memory.TransferNode);
        }

        public void Run() {
            if (Window == null) throw new EngineException("Window not set");

            while (true) {
                GLFW.PollEvents();

                if (Window.ShouldClose) break;

                Clock.FrameUpdate();
                FrameLoop.Update(Clock.FrameDelta);
                Window.Update();
                Cameras.Update();

                QueueGraph.Submit();
                QueueGraph.Wait();
                Memory.ResetStaging();
            }

            Graphics.Device.WaitIdle();
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposing) {
            if (disposed) return;

            QueueGraph.Dispose();
            Cameras.Dispose();
            Memory.Dispose();

            if (disposing) {
                Window.Dispose();
                Graphics.Dispose();
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
