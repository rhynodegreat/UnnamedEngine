using System;
using System.Collections.Generic;

using CSGL;
using CSGL.Vulkan;

namespace UnnamedEngine.Core {
    public class Renderer : IDisposable {
        bool disposed;

        public Instance Instance { get; private set; }
        public PhysicalDevice PhysicalDevice { get; private set; }

        public Renderer(Instance instance, PhysicalDevice physicalDevice) {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            if (physicalDevice == null) throw new ArgumentNullException(nameof(physicalDevice));

            Instance = instance;
            PhysicalDevice = physicalDevice;
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposing) {
            if (disposed) return;

            if (disposing) {
                Instance.Dispose();
            }

            Instance = null;
            PhysicalDevice = null;

            disposed = true;
        }

        ~Renderer() {
            Dispose(false);
        }
    }
}
