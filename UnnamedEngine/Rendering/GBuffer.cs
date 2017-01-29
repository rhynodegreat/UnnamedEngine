using System;
using System.Collections.Generic;

using CSGL;
using CSGL.Vulkan;
using Image = CSGL.Vulkan.Image;

using UnnamedEngine.Core;
using UnnamedEngine.Utilities;

namespace UnnamedEngine.Rendering {
    public class GBuffer : IDisposable {
        bool disposed;
        Engine engine;
        Window window;

        VkaAllocation albedoAlloc;
        VkaAllocation normAlloc;
        VkaAllocation depthAlloc;
        VkaAllocation lightAlloc;

        public Image Albedo { get; private set; }
        public Image Norm { get; private set; }
        public Image Depth { get; private set; }
        public Image Light { get; private set; }

        public ImageView AlbedoView { get; private set; }
        public ImageView NormView { get; private set; }
        public ImageView DepthView { get; private set; }
        public ImageView LightView { get; private set; }

        public VkFormat DepthFormat { get; private set; }

        public GBuffer(Engine engine, Window window) {
            if (engine == null) throw new ArgumentNullException(nameof(engine));
            if (window == null) throw new ArgumentNullException(nameof(window));

            this.engine = engine;
            this.window = window;

            window.OnSizeChanged += CreateGBuffer;

            CreateGBuffer(window.Width, window.Height);
        }

        void CreateGBuffer(int width, int height) {
            Free();
            DepthFormat = FindDepthFormat();

            CreateAlbedo(width, height);
            CreateNorm(width, height);
            CreateDepth(width, height);
            CreateLight(width, height);
        }

        void CreateAlbedo(int width, int height) {
            ImageCreateInfo albedoInfo = new ImageCreateInfo();
            albedoInfo.usage = VkImageUsageFlags.ColorAttachmentBit | VkImageUsageFlags.InputAttachmentBit;
            albedoInfo.tiling = VkImageTiling.Optimal;
            albedoInfo.sharingMode = VkSharingMode.Exclusive;
            albedoInfo.samples = VkSampleCountFlags._1Bit;
            albedoInfo.imageType = VkImageType._2d;
            albedoInfo.initialLayout = VkImageLayout.Undefined;
            albedoInfo.mipLevels = 1;
            albedoInfo.arrayLayers = 1;
            albedoInfo.extent.width = (uint)width;
            albedoInfo.extent.height = (uint)height;
            albedoInfo.extent.depth = 1;
            albedoInfo.format = VkFormat.R8g8b8a8Uint;

            Albedo = new Image(engine.Renderer.Device, albedoInfo);
            albedoAlloc = engine.Renderer.Allocator.Alloc(Albedo.MemoryRequirements, VkMemoryPropertyFlags.DeviceLocalBit);
            Albedo.Bind(albedoAlloc.memory, albedoAlloc.offset);

            ImageViewCreateInfo albedoViewInfo = new ImageViewCreateInfo(Albedo);
            albedoViewInfo.components.r = VkComponentSwizzle.Identity;
            albedoViewInfo.components.g = VkComponentSwizzle.Identity;
            albedoViewInfo.components.b = VkComponentSwizzle.Identity;
            albedoViewInfo.components.a = VkComponentSwizzle.Identity;
            albedoViewInfo.format = VkFormat.R8g8b8a8Uint;
            albedoViewInfo.viewType = VkImageViewType._2d;
            albedoViewInfo.subresourceRange.aspectMask = VkImageAspectFlags.ColorBit;
            albedoViewInfo.subresourceRange.levelCount = 1;
            albedoViewInfo.subresourceRange.baseMipLevel = 0;
            albedoViewInfo.subresourceRange.layerCount = 1;
            albedoViewInfo.subresourceRange.baseArrayLayer = 0;
            AlbedoView = new ImageView(engine.Renderer.Device, albedoViewInfo);
        }

        void CreateNorm(int width, int height) {
            ImageCreateInfo normInfo = new ImageCreateInfo();
            normInfo.usage = VkImageUsageFlags.ColorAttachmentBit | VkImageUsageFlags.InputAttachmentBit;
            normInfo.tiling = VkImageTiling.Optimal;
            normInfo.sharingMode = VkSharingMode.Exclusive;
            normInfo.samples = VkSampleCountFlags._1Bit;
            normInfo.imageType = VkImageType._2d;
            normInfo.initialLayout = VkImageLayout.Undefined;
            normInfo.mipLevels = 1;
            normInfo.arrayLayers = 1;
            normInfo.extent.width = (uint)width;
            normInfo.extent.height = (uint)height;
            normInfo.extent.depth = 1;
            normInfo.format = VkFormat.R16g16b16a16Sfloat;

            Norm = new Image(engine.Renderer.Device, normInfo);
            normAlloc = engine.Renderer.Allocator.Alloc(Norm.MemoryRequirements, VkMemoryPropertyFlags.DeviceLocalBit);
            Norm.Bind(normAlloc.memory, normAlloc.offset);

            ImageViewCreateInfo normViewInfo = new ImageViewCreateInfo(Norm);
            normViewInfo.components.r = VkComponentSwizzle.Identity;
            normViewInfo.components.g = VkComponentSwizzle.Identity;
            normViewInfo.components.b = VkComponentSwizzle.Identity;
            normViewInfo.components.a = VkComponentSwizzle.Identity;
            normViewInfo.format = VkFormat.R16g16b16a16Sfloat;
            normViewInfo.viewType = VkImageViewType._2d;
            normViewInfo.subresourceRange.aspectMask = VkImageAspectFlags.ColorBit;
            normViewInfo.subresourceRange.levelCount = 1;
            normViewInfo.subresourceRange.baseMipLevel = 0;
            normViewInfo.subresourceRange.layerCount = 1;
            normViewInfo.subresourceRange.baseArrayLayer = 0;
            NormView = new ImageView(engine.Renderer.Device, normViewInfo);
        }

        VkFormat FindDepthFormat() {
            VkFormat[] candidates = new VkFormat[] { VkFormat.D32Sfloat, VkFormat.D32SfloatS8Uint, VkFormat.D24UnormS8Uint };

            for (int i = 0; i < candidates.Length; i++) {
                var props = engine.Renderer.PhysicalDevice.GetFormatProperties(candidates[i]);

                if ((props.optimalTilingFeatures & VkFormatFeatureFlags.DepthStencilAttachmentBit) == VkFormatFeatureFlags.DepthStencilAttachmentBit) {
                    return candidates[i];
                }
            }

            throw new GBufferException("Could not find good depth buffer format");
        }

        void CreateDepth(int width, int height) {
            ImageCreateInfo depthInfo = new ImageCreateInfo();
            depthInfo.usage = VkImageUsageFlags.DepthStencilAttachmentBit | VkImageUsageFlags.InputAttachmentBit;
            depthInfo.tiling = VkImageTiling.Optimal;
            depthInfo.sharingMode = VkSharingMode.Exclusive;
            depthInfo.samples = VkSampleCountFlags._1Bit;
            depthInfo.imageType = VkImageType._2d;
            depthInfo.initialLayout = VkImageLayout.Undefined;
            depthInfo.mipLevels = 1;
            depthInfo.arrayLayers = 1;
            depthInfo.extent.width = (uint)width;
            depthInfo.extent.height = (uint)height;
            depthInfo.extent.depth = 1;
            depthInfo.format = DepthFormat;

            Depth = new Image(engine.Renderer.Device, depthInfo);
            depthAlloc = engine.Renderer.Allocator.Alloc(Depth.MemoryRequirements, VkMemoryPropertyFlags.DeviceLocalBit);
            Depth.Bind(depthAlloc.memory, depthAlloc.offset);

            ImageViewCreateInfo depthViewInfo = new ImageViewCreateInfo(Depth);
            depthViewInfo.components.r = VkComponentSwizzle.Identity;
            depthViewInfo.components.g = VkComponentSwizzle.Identity;
            depthViewInfo.components.b = VkComponentSwizzle.Identity;
            depthViewInfo.components.a = VkComponentSwizzle.Identity;
            depthViewInfo.format = DepthFormat;
            depthViewInfo.viewType = VkImageViewType._2d;
            depthViewInfo.subresourceRange.aspectMask = VkImageAspectFlags.DepthBit;

            if (DepthFormat == VkFormat.D32SfloatS8Uint || DepthFormat == VkFormat.D24UnormS8Uint) {
                depthViewInfo.subresourceRange.aspectMask |= VkImageAspectFlags.StencilBit;
            }

            depthViewInfo.subresourceRange.levelCount = 1;
            depthViewInfo.subresourceRange.baseMipLevel = 0;
            depthViewInfo.subresourceRange.layerCount = 1;
            depthViewInfo.subresourceRange.baseArrayLayer = 0;
            DepthView = new ImageView(engine.Renderer.Device, depthViewInfo);
        }

        void CreateLight(int width, int height) {
            ImageCreateInfo lightInfo = new ImageCreateInfo();
            lightInfo.usage = VkImageUsageFlags.ColorAttachmentBit | VkImageUsageFlags.InputAttachmentBit;
            lightInfo.tiling = VkImageTiling.Optimal;
            lightInfo.sharingMode = VkSharingMode.Exclusive;
            lightInfo.samples = VkSampleCountFlags._1Bit;
            lightInfo.imageType = VkImageType._2d;
            lightInfo.initialLayout = VkImageLayout.Undefined;
            lightInfo.mipLevels = 1;
            lightInfo.arrayLayers = 1;
            lightInfo.extent.width = (uint)width;
            lightInfo.extent.height = (uint)height;
            lightInfo.extent.depth = 1;
            lightInfo.format = VkFormat.R16g16b16a16Sfloat;

            Light = new Image(engine.Renderer.Device, lightInfo);
            lightAlloc = engine.Renderer.Allocator.Alloc(Light.MemoryRequirements, VkMemoryPropertyFlags.DeviceLocalBit);
            Light.Bind(lightAlloc.memory, lightAlloc.offset);

            ImageViewCreateInfo lightViewInfo = new ImageViewCreateInfo(Light);
            lightViewInfo.components.r = VkComponentSwizzle.Identity;
            lightViewInfo.components.g = VkComponentSwizzle.Identity;
            lightViewInfo.components.b = VkComponentSwizzle.Identity;
            lightViewInfo.components.a = VkComponentSwizzle.Identity;
            lightViewInfo.format = VkFormat.R16g16b16a16Sfloat;
            lightViewInfo.viewType = VkImageViewType._2d;
            lightViewInfo.subresourceRange.aspectMask = VkImageAspectFlags.ColorBit;
            lightViewInfo.subresourceRange.levelCount = 1;
            lightViewInfo.subresourceRange.baseMipLevel = 0;
            lightViewInfo.subresourceRange.layerCount = 1;
            lightViewInfo.subresourceRange.baseArrayLayer = 0;
            LightView = new ImageView(engine.Renderer.Device, lightViewInfo);
        }

        void Free() {
            AlbedoView?.Dispose();
            NormView?.Dispose();
            DepthView?.Dispose();
            LightView?.Dispose();
            Albedo?.Dispose();
            Norm?.Dispose();
            Depth?.Dispose();
            Light?.Dispose();
            engine.Renderer.Allocator.Free(albedoAlloc);
            engine.Renderer.Allocator.Free(normAlloc);
            engine.Renderer.Allocator.Free(depthAlloc);
            engine.Renderer.Allocator.Free(lightAlloc);
            albedoAlloc = default(VkaAllocation);
            normAlloc = default(VkaAllocation);
            depthAlloc = default(VkaAllocation);
            lightAlloc = default(VkaAllocation);
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposing) {
            if (disposed) return;

            window.OnSizeChanged -= CreateGBuffer;

            Free();

            disposed = true;
        }

        ~GBuffer() {
            Dispose(false);
        }
    }

    public class GBufferException : Exception {
        public GBufferException(string message) : base(message) { }
    }
}
