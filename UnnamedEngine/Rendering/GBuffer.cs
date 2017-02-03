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

        DescriptorPool pool;

        VkaAllocation albedoAlloc;
        VkaAllocation normAlloc;
        VkaAllocation depthAlloc;
        VkaAllocation lightAlloc;

        Sampler lightSampler;

        public DescriptorSetLayout InputLayout { get; private set; }
        public DescriptorSet InputDescriptor { get; private set; }
        public DescriptorSetLayout LightLayout { get; private set; }
        public DescriptorSet LightDescriptor { get; private set; }

        public VkFormat AlbedoFormat { get; private set; } = VkFormat.R8g8b8a8Unorm;
        public VkFormat NormFormat { get; private set; } = VkFormat.R16g16b16a16Sfloat;
        public VkFormat DepthFormat { get; private set; }
        public VkFormat LightFormat { get; private set; } = VkFormat.R16g16b16a16Sfloat;

        public Image Albedo { get; private set; }
        public Image Norm { get; private set; }
        public Image Depth { get; private set; }
        public Image Light { get; private set; }

        public ImageView AlbedoView { get; private set; }
        public ImageView NormView { get; private set; }
        public ImageView DepthView { get; private set; }
        public ImageView LightView { get; private set; }

        public int Width { get; private set; }
        public int Height { get; private set; }

        public event Action<int, int> OnSizeChanged = delegate { };

        public GBuffer(Engine engine, Window window) {
            if (engine == null) throw new ArgumentNullException(nameof(engine));
            if (window == null) throw new ArgumentNullException(nameof(window));

            this.engine = engine;
            this.window = window;

            window.OnSizeChanged += CreateGBuffer;

            DepthFormat = FindDepthFormat();

            CreateSampler();
            CreateDescriptors();
            CreateGBuffer(window.Width, window.Height);
        }

        void CreateGBuffer(int width, int height) {
            Free();

            Width = width;
            Height = height;

            CreateAlbedo();
            CreateNorm();
            CreateDepth();
            CreateLight();

            UpdateInputSet();

            OnSizeChanged(width, height);
        }

        void CreateSampler() {
            SamplerCreateInfo info = new SamplerCreateInfo();
            info.magFilter = VkFilter.Linear;
            info.minFilter = VkFilter.Linear;
            info.mipmapMode = VkSamplerMipmapMode.Nearest;
            info.addressModeU = VkSamplerAddressMode.MirroredRepeat;
            info.addressModeV = VkSamplerAddressMode.MirroredRepeat;
            info.addressModeW = VkSamplerAddressMode.MirroredRepeat;
            info.unnormalizedCoordinates = true;

            lightSampler = new Sampler(engine.Graphics.Device, info);
        }

        void CreateDescriptors() {
            DescriptorSetLayoutCreateInfo inputLayoutInfo = new DescriptorSetLayoutCreateInfo();
            inputLayoutInfo.bindings = new List<VkDescriptorSetLayoutBinding> {
                new VkDescriptorSetLayoutBinding {  //albedo
                    binding = 0,
                    descriptorCount = 1,
                    descriptorType = VkDescriptorType.InputAttachment,
                    stageFlags = VkShaderStageFlags.FragmentBit
                },
                new VkDescriptorSetLayoutBinding {  //norm
                    binding = 1,
                    descriptorCount = 1,
                    descriptorType = VkDescriptorType.InputAttachment,
                    stageFlags = VkShaderStageFlags.FragmentBit
                }
            };

            InputLayout = new DescriptorSetLayout(engine.Graphics.Device, inputLayoutInfo);

            DescriptorSetLayoutCreateInfo lightLayoutInfo = new DescriptorSetLayoutCreateInfo();
            lightLayoutInfo.bindings = new List<VkDescriptorSetLayoutBinding> {
                new VkDescriptorSetLayoutBinding {
                    binding = 0,
                    descriptorCount = 1,
                    descriptorType = VkDescriptorType.CombinedImageSampler,
                    stageFlags = VkShaderStageFlags.FragmentBit
                }
            };

            LightLayout = new DescriptorSetLayout(engine.Graphics.Device, lightLayoutInfo);

            DescriptorPoolCreateInfo poolInfo = new DescriptorPoolCreateInfo();
            poolInfo.maxSets = 2;
            poolInfo.poolSizes = new List<VkDescriptorPoolSize> {
                new VkDescriptorPoolSize {
                    descriptorCount = 2,
                    type = VkDescriptorType.InputAttachment
                },
                new VkDescriptorPoolSize {
                    descriptorCount = 1,
                    type = VkDescriptorType.CombinedImageSampler
                }
            };

            pool = new DescriptorPool(engine.Graphics.Device, poolInfo);

            DescriptorSetAllocateInfo inputAllocInfo = new DescriptorSetAllocateInfo();
            inputAllocInfo.descriptorSetCount = 1;
            inputAllocInfo.setLayouts = new List<DescriptorSetLayout> { InputLayout };

            InputDescriptor = pool.Allocate(inputAllocInfo)[0];

            DescriptorSetAllocateInfo lightAllocInfo = new DescriptorSetAllocateInfo();
            lightAllocInfo.descriptorSetCount = 1;
            lightAllocInfo.setLayouts = new List<DescriptorSetLayout> { LightLayout };

            LightDescriptor = pool.Allocate(lightAllocInfo)[0];
        }

        void UpdateInputSet() {
            InputDescriptor.Update(new List<WriteDescriptorSet> {
                new WriteDescriptorSet {    //albedo
                    dstSet = InputDescriptor,
                    descriptorType = VkDescriptorType.InputAttachment,
                    dstArrayElement = 0,
                    dstBinding = 0,
                    imageInfo = new List<DescriptorImageInfo> {
                        new DescriptorImageInfo {
                            imageLayout = VkImageLayout.ColorAttachmentOptimal,
                            imageView = AlbedoView
                        }
                    }
                },
                new WriteDescriptorSet {    //norm
                    dstSet = InputDescriptor,
                    descriptorType = VkDescriptorType.InputAttachment,
                    dstArrayElement = 0,
                    dstBinding = 1,
                    imageInfo = new List<DescriptorImageInfo> {
                        new DescriptorImageInfo {
                            imageLayout = VkImageLayout.ColorAttachmentOptimal,
                            imageView = NormView
                        }
                    }
                },
                new WriteDescriptorSet {
                    dstSet = LightDescriptor,
                    descriptorType = VkDescriptorType.CombinedImageSampler,
                    dstArrayElement = 0,
                    dstBinding = 0,
                    imageInfo = new List<DescriptorImageInfo> {
                        new DescriptorImageInfo {
                            imageLayout = VkImageLayout.ShaderReadOnlyOptimal,
                            imageView = LightView,
                            sampler = lightSampler
                        }
                    }
                }
            });
        }

        void CreateAlbedo() {
            ImageCreateInfo albedoInfo = new ImageCreateInfo();
            albedoInfo.usage = VkImageUsageFlags.ColorAttachmentBit | VkImageUsageFlags.InputAttachmentBit;
            albedoInfo.tiling = VkImageTiling.Optimal;
            albedoInfo.sharingMode = VkSharingMode.Exclusive;
            albedoInfo.samples = VkSampleCountFlags._1Bit;
            albedoInfo.imageType = VkImageType._2d;
            albedoInfo.initialLayout = VkImageLayout.Undefined;
            albedoInfo.mipLevels = 1;
            albedoInfo.arrayLayers = 1;
            albedoInfo.extent.width = (uint)Width;
            albedoInfo.extent.height = (uint)Height;
            albedoInfo.extent.depth = 1;
            albedoInfo.format = AlbedoFormat;

            Albedo = new Image(engine.Graphics.Device, albedoInfo);
            albedoAlloc = engine.Graphics.Allocator.Alloc(Albedo.MemoryRequirements, VkMemoryPropertyFlags.DeviceLocalBit);
            Albedo.Bind(albedoAlloc.memory, albedoAlloc.offset);

            ImageViewCreateInfo albedoViewInfo = new ImageViewCreateInfo(Albedo);
            albedoViewInfo.components.r = VkComponentSwizzle.Identity;
            albedoViewInfo.components.g = VkComponentSwizzle.Identity;
            albedoViewInfo.components.b = VkComponentSwizzle.Identity;
            albedoViewInfo.components.a = VkComponentSwizzle.Identity;
            albedoViewInfo.format = AlbedoFormat;
            albedoViewInfo.viewType = VkImageViewType._2d;
            albedoViewInfo.subresourceRange.aspectMask = VkImageAspectFlags.ColorBit;
            albedoViewInfo.subresourceRange.levelCount = 1;
            albedoViewInfo.subresourceRange.baseMipLevel = 0;
            albedoViewInfo.subresourceRange.layerCount = 1;
            albedoViewInfo.subresourceRange.baseArrayLayer = 0;
            AlbedoView = new ImageView(engine.Graphics.Device, albedoViewInfo);
        }

        void CreateNorm() {
            ImageCreateInfo normInfo = new ImageCreateInfo();
            normInfo.usage = VkImageUsageFlags.ColorAttachmentBit | VkImageUsageFlags.InputAttachmentBit;
            normInfo.tiling = VkImageTiling.Optimal;
            normInfo.sharingMode = VkSharingMode.Exclusive;
            normInfo.samples = VkSampleCountFlags._1Bit;
            normInfo.imageType = VkImageType._2d;
            normInfo.initialLayout = VkImageLayout.Undefined;
            normInfo.mipLevels = 1;
            normInfo.arrayLayers = 1;
            normInfo.extent.width = (uint)Width;
            normInfo.extent.height = (uint)Height;
            normInfo.extent.depth = 1;
            normInfo.format = NormFormat;

            Norm = new Image(engine.Graphics.Device, normInfo);
            normAlloc = engine.Graphics.Allocator.Alloc(Norm.MemoryRequirements, VkMemoryPropertyFlags.DeviceLocalBit);
            Norm.Bind(normAlloc.memory, normAlloc.offset);

            ImageViewCreateInfo normViewInfo = new ImageViewCreateInfo(Norm);
            normViewInfo.components.r = VkComponentSwizzle.Identity;
            normViewInfo.components.g = VkComponentSwizzle.Identity;
            normViewInfo.components.b = VkComponentSwizzle.Identity;
            normViewInfo.components.a = VkComponentSwizzle.Identity;
            normViewInfo.format = NormFormat;
            normViewInfo.viewType = VkImageViewType._2d;
            normViewInfo.subresourceRange.aspectMask = VkImageAspectFlags.ColorBit;
            normViewInfo.subresourceRange.levelCount = 1;
            normViewInfo.subresourceRange.baseMipLevel = 0;
            normViewInfo.subresourceRange.layerCount = 1;
            normViewInfo.subresourceRange.baseArrayLayer = 0;
            NormView = new ImageView(engine.Graphics.Device, normViewInfo);
        }

        VkFormat FindDepthFormat() {
            VkFormat[] candidates = new VkFormat[] { VkFormat.D32Sfloat, VkFormat.D32SfloatS8Uint, VkFormat.D24UnormS8Uint };

            for (int i = 0; i < candidates.Length; i++) {
                var props = engine.Graphics.PhysicalDevice.GetFormatProperties(candidates[i]);

                if ((props.optimalTilingFeatures & VkFormatFeatureFlags.DepthStencilAttachmentBit) == VkFormatFeatureFlags.DepthStencilAttachmentBit) {
                    return candidates[i];
                }
            }

            throw new GBufferException("Could not find good depth buffer format");
        }

        void CreateDepth() {
            ImageCreateInfo depthInfo = new ImageCreateInfo();
            depthInfo.usage = VkImageUsageFlags.DepthStencilAttachmentBit | VkImageUsageFlags.InputAttachmentBit;
            depthInfo.tiling = VkImageTiling.Optimal;
            depthInfo.sharingMode = VkSharingMode.Exclusive;
            depthInfo.samples = VkSampleCountFlags._1Bit;
            depthInfo.imageType = VkImageType._2d;
            depthInfo.initialLayout = VkImageLayout.Undefined;
            depthInfo.mipLevels = 1;
            depthInfo.arrayLayers = 1;
            depthInfo.extent.width = (uint)Width;
            depthInfo.extent.height = (uint)Height;
            depthInfo.extent.depth = 1;
            depthInfo.format = DepthFormat;

            Depth = new Image(engine.Graphics.Device, depthInfo);
            depthAlloc = engine.Graphics.Allocator.Alloc(Depth.MemoryRequirements, VkMemoryPropertyFlags.DeviceLocalBit);
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
            DepthView = new ImageView(engine.Graphics.Device, depthViewInfo);
        }

        void CreateLight() {
            ImageCreateInfo lightInfo = new ImageCreateInfo();
            lightInfo.usage = VkImageUsageFlags.ColorAttachmentBit | VkImageUsageFlags.SampledBit | VkImageUsageFlags.InputAttachmentBit;
            lightInfo.tiling = VkImageTiling.Optimal;
            lightInfo.sharingMode = VkSharingMode.Exclusive;
            lightInfo.samples = VkSampleCountFlags._1Bit;
            lightInfo.imageType = VkImageType._2d;
            lightInfo.initialLayout = VkImageLayout.Undefined;
            lightInfo.mipLevels = 1;
            lightInfo.arrayLayers = 1;
            lightInfo.extent.width = (uint)Width;
            lightInfo.extent.height = (uint)Height;
            lightInfo.extent.depth = 1;
            lightInfo.format = LightFormat;

            Light = new Image(engine.Graphics.Device, lightInfo);
            lightAlloc = engine.Graphics.Allocator.Alloc(Light.MemoryRequirements, VkMemoryPropertyFlags.DeviceLocalBit);
            Light.Bind(lightAlloc.memory, lightAlloc.offset);

            ImageViewCreateInfo lightViewInfo = new ImageViewCreateInfo(Light);
            lightViewInfo.components.r = VkComponentSwizzle.Identity;
            lightViewInfo.components.g = VkComponentSwizzle.Identity;
            lightViewInfo.components.b = VkComponentSwizzle.Identity;
            lightViewInfo.components.a = VkComponentSwizzle.Identity;
            lightViewInfo.format = LightFormat;
            lightViewInfo.viewType = VkImageViewType._2d;
            lightViewInfo.subresourceRange.aspectMask = VkImageAspectFlags.ColorBit;
            lightViewInfo.subresourceRange.levelCount = 1;
            lightViewInfo.subresourceRange.baseMipLevel = 0;
            lightViewInfo.subresourceRange.layerCount = 1;
            lightViewInfo.subresourceRange.baseArrayLayer = 0;
            LightView = new ImageView(engine.Graphics.Device, lightViewInfo);
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
            engine.Graphics.Allocator.Free(albedoAlloc);
            engine.Graphics.Allocator.Free(normAlloc);
            engine.Graphics.Allocator.Free(depthAlloc);
            engine.Graphics.Allocator.Free(lightAlloc);
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
            pool.Dispose();
            InputLayout.Dispose();
            LightLayout.Dispose();
            lightSampler.Dispose();

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
