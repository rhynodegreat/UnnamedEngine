using System;
using System.Collections.Generic;

using CSGL;
using CSGL.Vulkan;

namespace UnnamedEngine {
    public class Renderer : IDisposable {
        bool disposed;

        public Instance Instance { get; private set; }
        public Device Device { get; private set; }

        public Renderer(Instance instance, Device device) {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            if (device == null) throw new ArgumentNullException(nameof(device));

            Instance = instance;
            Device = device;
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposing) {
            if (disposed) return;

            if (disposing) {
                Device.Dispose();
                Instance.Dispose();
            }

            Instance = null;
            Device = null;

            disposed = true;
        }

        ~Renderer() {
            Dispose(false);
        }
    }
}
