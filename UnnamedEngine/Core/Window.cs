using System;
using System.Collections.Generic;

using CSGL.GLFW;
using CSGL.Vulkan;
using GWindow = CSGL.GLFW.Window;

using Image = CSGL.Vulkan.Image;

namespace UnnamedEngine.Core {
    public class Window : IDisposable {
        bool disposed;

        GWindow window;
        int width;
        int height;
        
        public Surface Surface { get; private set; }
        public Swapchain Swapchain { get; private set; }
        public VkFormat SwapchainImageFormat { get; private set; }
        public VkExtent2D SwapchainExtent { get; private set; }

        public IList<Image> SwapchainImages {
            get {
                return Swapchain?.Images;
            }
        }

        public bool ShouldClose {
            get {
                return window.ShouldClose;
            }
        }

        public int Width {
            get {
                return width;
            }
        }

        public int Height {
            get {
                return height;
            }
        }

        public Window(Engine engine, int width, int height, string title) {
            if (width < 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height < 0) throw new ArgumentOutOfRangeException(nameof(height));

            this.width = width;
            this.height = height;

            GLFW.WindowHint(WindowHint.ClientAPI, (int)ClientAPI.NoAPI);
            window = new GWindow(width, height, title, null, null);

            Surface = new Surface(engine.Renderer.PhysicalDevice, window);
            if (!engine.Renderer.PresentQueue.Family.SurfaceSupported(Surface)) {   //this check is apparently required by the validation layer
                throw new WindowException("Could not create surface (Not supported by present queue)");
            }

            CreateSwapchain(engine.Renderer);
        }

        void CreateSwapchain(Renderer renderer) {
            var cap = Surface.Capabilities;
            var format = ChooseSwapSurfaceFormat(Surface.Formats);
            var mode = ChooseSwapPresentMode(Surface.PresentModes);
            var extent = ChooseSwapExtent(ref cap);
            
            uint imageCount = cap.minImageCount + 1;
            if (cap.maxImageCount > 0 && imageCount > cap.maxImageCount) {
                imageCount = cap.maxImageCount;
            }

            var oldSwapchain = Swapchain;
            var info = new SwapchainCreateInfo(Surface, oldSwapchain);
            info.minImageCount = imageCount;
            info.imageFormat = format.format;
            info.imageColorSpace = format.colorSpace;
            info.imageExtent = extent;
            info.imageArrayLayers = 1;
            info.imageUsage = VkImageUsageFlags.ColorAttachmentBit;

            var queueFamilyIndices = new List<uint> { renderer.GraphicsQueue.FamilyIndex,  renderer.PresentQueue.FamilyIndex };

            if (renderer.GraphicsQueue.FamilyIndex != renderer.PresentQueue.FamilyIndex) {
                info.imageSharingMode = VkSharingMode.Concurrent;
                info.queueFamilyIndices = queueFamilyIndices;
            } else {
                info.imageSharingMode = VkSharingMode.Exclusive;
            }

            info.preTransform = cap.currentTransform;
            info.compositeAlpha = VkCompositeAlphaFlagsKHR.OpaqueBitKhr;
            info.presentMode = mode;
            info.clipped = true;

            Swapchain = new Swapchain(renderer.Device, info);
            oldSwapchain?.Dispose();

            SwapchainImageFormat = format.format;
            SwapchainExtent = extent;
        }

        VkSurfaceFormatKHR ChooseSwapSurfaceFormat(IList<VkSurfaceFormatKHR> formats) {
            if (formats.Count == 1 && formats[0].format == VkFormat.Undefined) {
                var result = new VkSurfaceFormatKHR();
                result.format = VkFormat.B8g8r8a8Unorm;
                result.colorSpace = VkColorSpaceKHR.SrgbNonlinearKhr;
                return result;
            }

            foreach (var f in formats) {
                if (f.format == VkFormat.B8g8r8a8Unorm && f.colorSpace == VkColorSpaceKHR.SrgbNonlinearKhr) {
                    return f;
                }
            }

            return formats[0];
        }

        VkPresentModeKHR ChooseSwapPresentMode(IList<VkPresentModeKHR> modes) {
            foreach (var m in modes) {
                if (m == VkPresentModeKHR.MailboxKhr) {
                    return m;
                }
            }

            return VkPresentModeKHR.FifoKhr;
        }

        VkExtent2D ChooseSwapExtent(ref VkSurfaceCapabilitiesKHR cap) {
            if (cap.currentExtent.width != uint.MaxValue) {
                return cap.currentExtent;
            } else {
                var extent = new VkExtent2D();
                extent.width = (uint)width;
                extent.height = (uint)height;

                extent.width = Math.Max(cap.minImageExtent.width, Math.Min(cap.maxImageExtent.width, extent.width));
                extent.height = Math.Max(cap.minImageExtent.height, Math.Min(cap.maxImageExtent.height, extent.height));

                return extent;
            }
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposing) {
            if (disposed) return;
            
            Swapchain.Dispose();
            Surface.Dispose();
            window.Dispose();

            disposed = true;
        }

        ~Window() {
            Dispose(false);
        }
    }

    public class WindowException : Exception {
        public WindowException(string message) : base(message) { }
        public WindowException(string format, params object[] args) : base(string.Format(format, args)) { }
    }
}
