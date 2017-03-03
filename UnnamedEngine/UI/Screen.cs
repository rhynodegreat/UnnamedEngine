using System;
using System.Collections.Generic;

using CSGL.Vulkan;

using UnnamedEngine.Core;
using UnnamedEngine.Utilities;

namespace UnnamedEngine.UI {
    public class Screen : IDisposable {
        bool disposed;

        Engine engine;

        VkaAllocation stencilAlloc;
        Image stencil;
        ImageView stencilView;

        public bool Clear { get; set; }
        public int Width { get; private set; }
        public int Height { get; private set; }

        public Screen(Engine engine, int width, int height, bool clear) {
            this.engine = engine;
            Width = width;
            Height = height;

            Clear = clear;

            CreateStencil();
        }

        public void Recreate(int width, int height) {
            Width = width;
            Height = height;
            CreateStencil();
        }

        void CreateStencil() {
            stencil?.Dispose();
            stencilView?.Dispose();

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

            stencil = new Image(engine.Graphics.Device, info);
            stencilAlloc = engine.Graphics.Allocator.Alloc(stencil.Requirements, VkMemoryPropertyFlags.DeviceLocalBit);
            stencil.Bind(stencilAlloc.memory, stencilAlloc.offset);

            ImageViewCreateInfo viewInfo = new ImageViewCreateInfo();
            viewInfo.image = stencil;
            viewInfo.viewType = VkImageViewType._2d;
            viewInfo.format = VkFormat.D32SfloatS8Uint;
            viewInfo.subresourceRange.aspectMask = VkImageAspectFlags.StencilBit;
            viewInfo.subresourceRange.baseArrayLayer = 0;
            viewInfo.subresourceRange.layerCount = 1;
            viewInfo.subresourceRange.baseMipLevel = 0;
            viewInfo.subresourceRange.levelCount = 1;

            stencilView = new ImageView(engine.Graphics.Device, viewInfo);
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposing) {
            if (disposed) return;

            stencilView.Dispose();
            stencil.Dispose();

            disposed = true;
        }

        ~Screen() {
            Dispose(true);
        }
    }
}
