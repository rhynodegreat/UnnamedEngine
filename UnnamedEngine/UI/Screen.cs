using System;
using System.Collections.Generic;

using CSGL.Vulkan;

using UnnamedEngine.Core;
using UnnamedEngine.Rendering;
using UnnamedEngine.Utilities;

namespace UnnamedEngine.UI {
    public class Screen : IDisposable {
        bool disposed;

        Engine engine;

        CommandPool pool;
        VkaAllocation stencilAlloc;
        public Image Stencil { get; private set; }
        public ImageView StencilView { get; private set; }

        public PerspectiveCamera Camera { get; set; }
        public bool Clear { get; set; }
        public int Width { get; private set; }
        public int Height { get; private set; }

        public Screen(Engine engine, PerspectiveCamera camera, int width, int height, bool clear) {
            this.engine = engine;

            Camera = camera;
            Clear = clear;
            Width = width;
            Height = height;

            CreateCommandPool();
            CreateStencil();
        }

        public void Recreate(int width, int height) {
            Width = width;
            Height = height;
            CreateStencil();
        }

        void CreateCommandPool() {
            CommandPoolCreateInfo info = new CommandPoolCreateInfo();
            info.flags = VkCommandPoolCreateFlags.ResetCommandBufferBit;
            info.queueFamilyIndex = engine.Graphics.GraphicsQueue.FamilyIndex;

            pool = new CommandPool(engine.Graphics.Device, info);
        }

        void CreateStencil() {
            engine.Graphics.Allocator.Free(stencilAlloc);
            Stencil?.Dispose();
            StencilView?.Dispose();

            ImageCreateInfo info = new ImageCreateInfo();
            info.imageType = VkImageType._2d;
            info.format = VkFormat.D32SfloatS8Uint;
            info.extent.width = (uint)Width;
            info.extent.height = (uint)Height;
            info.extent.depth = 1;
            info.mipLevels = 1;
            info.arrayLayers = 1;
            info.samples = VkSampleCountFlags._1Bit;
            info.tiling = VkImageTiling.Optimal;
            info.usage = VkImageUsageFlags.DepthStencilAttachmentBit;
            info.sharingMode = VkSharingMode.Exclusive;
            info.initialLayout = VkImageLayout.Undefined;

            Stencil = new Image(engine.Graphics.Device, info);
            stencilAlloc = engine.Graphics.Allocator.Alloc(Stencil.Requirements, VkMemoryPropertyFlags.DeviceLocalBit);
            Stencil.Bind(stencilAlloc.memory, stencilAlloc.offset);

            ImageViewCreateInfo viewInfo = new ImageViewCreateInfo();
            viewInfo.image = Stencil;
            viewInfo.viewType = VkImageViewType._2d;
            viewInfo.format = VkFormat.D32SfloatS8Uint;
            viewInfo.subresourceRange.aspectMask = VkImageAspectFlags.StencilBit;
            viewInfo.subresourceRange.baseArrayLayer = 0;
            viewInfo.subresourceRange.layerCount = 1;
            viewInfo.subresourceRange.baseMipLevel = 0;
            viewInfo.subresourceRange.levelCount = 1;

            StencilView = new ImageView(engine.Graphics.Device, viewInfo);
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposing) {
            if (disposed) return;

            pool.Dispose();
            StencilView.Dispose();
            Stencil.Dispose();
            engine.Graphics.Allocator.Free(stencilAlloc);

            disposed = true;
        }

        ~Screen() {
            Dispose(true);
        }
    }
}
