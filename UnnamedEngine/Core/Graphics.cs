using System;
using System.Collections.Generic;

using CSGL;
using CSGL.GLFW;
using CSGL.Vulkan1;

using UnnamedEngine.Utilities;
using UnnamedEngine.Resources;

namespace UnnamedEngine.Core {
    public class Graphics : IDisposable {
        bool disposed;
        
        string[] deviceExtensions = {
            "VK_KHR_swapchain"
        };

        public Instance Instance { get; private set; }
        public PhysicalDevice PhysicalDevice { get; private set; }
        public Device Device { get; private set; }

        public Queue GraphicsQueue { get; private set; }
        public Queue PresentQueue { get; private set; }

        public Graphics(Instance instance, PhysicalDevice physicalDevice) {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            if (physicalDevice == null) throw new ArgumentNullException(nameof(physicalDevice));

            Instance = instance;
            PhysicalDevice = physicalDevice;

            CreateDevice();
        }

        void CreateDevice() {
            uint graphicsIndex;
            uint presentIndex;

            int g = -1;
            int p = -1;

            for (int i = 0; i < PhysicalDevice.QueueFamilies.Count; i++) {
                var family = PhysicalDevice.QueueFamilies[i];
                if (g == -1 && (family.Flags & VkQueueFlags.GraphicsBit) != 0) {
                    g = i;
                }

                if (p == -1 && GLFW.GetPhysicalDevicePresentationSupport(Instance.Native.native, PhysicalDevice.Native.native, (uint)i)) {
                    p = i;
                }
            }

            if (g == -1) throw new GraphicsException("Graphics operations not supported");
            if (p == -1) throw new GraphicsException("Presentation not supported");

            graphicsIndex = (uint)g;
            presentIndex = (uint)p;

            var features = PhysicalDevice.Features;

            HashSet<uint> uniqueIndices = new HashSet<uint> { graphicsIndex, presentIndex };
            List<float> priorities = new List<float> { 1f };
            List<DeviceQueueCreateInfo> queueInfos = new List<DeviceQueueCreateInfo>(uniqueIndices.Count);
            
            foreach (var ind in uniqueIndices) {
                var queueInfo = new DeviceQueueCreateInfo(ind, 1, priorities);
                queueInfos.Add(queueInfo);
            }

            var info = new DeviceCreateInfo(new List<string>(deviceExtensions), queueInfos, features);
            Device = new Device(PhysicalDevice, info);

            GraphicsQueue = Device.GetQueue(graphicsIndex, 0);
            PresentQueue = Device.GetQueue(presentIndex, 0);
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposing) {
            if (disposed) return;
            
            Device.Dispose();
            Instance.Dispose();

            disposed = true;
        }

        ~Graphics() {
            Dispose(false);
        }
    }

    public class GraphicsException : Exception {
        public GraphicsException(string message) : base(message) { }
    }
}
