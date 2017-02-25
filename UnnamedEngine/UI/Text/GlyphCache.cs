using System;
using System.Collections.Generic;
using System.Numerics;

using CSGL.Graphics;
using CSGL.Vulkan;
using CSGL.Math;

using MSDFGen;

using UnnamedEngine.Core;
using UnnamedEngine.Utilities;

namespace UnnamedEngine.UI.Text {
    public struct GlyphMetrics {
        public Vector2 offset;
        public Vector2 size;
        public Vector3 uvPosition;
    }

    public class GlyphCache : IDisposable {
        struct GlyphPair : IEquatable<GlyphPair> {
            public Font font;
            public int codepoint;

            public GlyphPair(Font font, int codepoint) {
                this.font = font;
                this.codepoint = codepoint;
            }

            public override int GetHashCode() {
                return font.GetHashCode() ^ codepoint.GetHashCode();
            }

            public override bool Equals(object other) {
                if (other is GlyphPair) {
                    return Equals((GlyphPair)other);
                }

                return false;
            }

            public bool Equals(GlyphPair other) {
                return codepoint == other.codepoint && font == other.font;
            }
        }

        struct GlyphInfo {
            public Font font;
            public int codepoint;
            public Glyph glyph;
            public GlyphMetrics metrics;

            public GlyphInfo(Font font, int codepoint, Glyph glyph, GlyphMetrics metrics) {
                this.font = font;
                this.codepoint = codepoint;
                this.glyph = glyph;
                this.metrics = metrics;
            }
        }

        bool disposed;

        Engine engine;
        List<GlyphCachePage> pages;
        HashSet<int> pageUpdates;
        List<GlyphInfo> glyphUpdates;
        Dictionary<GlyphPair, GlyphMetrics> infoMap;
        VkaAllocation alloc;
        DescriptorPool pool;
        ImageView imageView;
        Sampler sampler;

        int padding = 1;
        float scale = 2f;

        public int PageSize { get; private set; }
        public int PageCount { get; private set; }
        public float Range { get; private set; }
        public Image Image { get; private set; }
        public DescriptorSetLayout DescriptorLayout { get; private set; }
        public DescriptorSet Descriptor { get; private set; }

        public GlyphCache(Engine engine, int pageSize, int pageCount, float range) {
            if (engine == null) throw new ArgumentNullException(nameof(engine));

            this.engine = engine;
            pages = new List<GlyphCachePage>();
            infoMap = new Dictionary<GlyphPair, GlyphMetrics>();
            pageUpdates = new HashSet<int>();
            glyphUpdates = new List<GlyphInfo>();
            PageSize = pageSize;
            Range = range;
            PageCount = pageCount;

            CreateSampler();
            CreateImage();
            CreateDescriptors();
            UpdateDescriptor();
        }

        public void AddString(Font font, string s) {
            for (int i = 0; i < s.Length; i++) {
                if (char.IsHighSurrogate(s[i])) continue;
                int c;
                if (char.IsLowSurrogate(s[i])) {
                    if (i == 0) continue;
                    c = char.ConvertToUtf32(s[i - 1], s[i]);
                } else {
                    c = s[i];
                }

                AddChar(font, c);
            }
        }

        public void AddChar(Font font, int codepoint) {
            var pair = new GlyphPair(font, codepoint);
            if (infoMap.ContainsKey(pair)) return;

            Glyph glyph = font.GetGlyph(codepoint);
            GlyphMetrics metrics = new GlyphMetrics();
            metrics.offset = new Vector2(glyph.Metrics.bearingX, glyph.Metrics.height - glyph.Metrics.bearingY) * scale;
            metrics.size = new Vector2(glyph.Metrics.width, glyph.Metrics.height) * scale;

            glyphUpdates.Add(new GlyphInfo(font, codepoint, glyph, metrics));
        }

        void AddGlyph(GlyphInfo info) {
            Font font = info.font;
            int codepoint = info.codepoint;
            Glyph glyph = info.glyph;
            GlyphMetrics metrics = info.metrics;

            Rectanglei rect = new Rectanglei(0, 0, (int)Math.Ceiling(metrics.size.X + Range * scale / 2) + padding * 2, (int)Math.Ceiling(metrics.size.Y + Range * scale / 2) + padding * 2);

            AddToPage(glyph, ref metrics, rect);

            infoMap.Add(new GlyphPair(info.font, info.codepoint), metrics);
        }

        void AddToPage(Glyph glyph, ref GlyphMetrics info, Rectanglei rect) {
            for (int i = 0; i < pages.Count; i++) {
                if (pages[i].AttemptAdd(ref rect)) {
                    Render(pages[i], i, glyph, info, rect);
                    info.uvPosition = new Vector3(rect.X, rect.Y, i);
                    return;
                }
            }

            GlyphCachePage newPage = new GlyphCachePage(PageSize, PageSize);
            newPage.AttemptAdd(ref rect);
            pages.Add(newPage);
            Render(newPage, pages.Count - 1, glyph, info, rect);
            info.uvPosition = new Vector3(rect.X, rect.Y, pages.Count - 1);
        }

        void Render(GlyphCachePage page, int pageIndex, Glyph glyph, GlyphMetrics info, Rectanglei rect) {
            Rectangle rectf = new Rectangle(rect.X + padding, rect.Y + padding, rect.Width - padding, rect.Height - padding);
            MSDF.GenerateMSDF(page.Bitmap, glyph.Shape, rectf, Range, new Vector2(scale, scale), new Vector2(-info.offset.X + padding + Range * scale / 4, info.offset.Y + padding + Range * scale / 4), 1.000001);
            pageUpdates.Add(pageIndex);
        }

        public GlyphMetrics GetInfo(Font font, int codepoint) {
            return infoMap[new GlyphPair(font, codepoint)];
        }

        public void Update() {
            if (glyphUpdates.Count > 0) {
                glyphUpdates.Sort((GlyphInfo a, GlyphInfo b) => {
                    float aArea = a.metrics.size.X * a.metrics.size.Y;
                    float bArea = b.metrics.size.X * b.metrics.size.Y;
                    return bArea.CompareTo(aArea);
                });

                for (int i = 0; i < glyphUpdates.Count; i++) {
                    AddGlyph(glyphUpdates[i]);
                }

                glyphUpdates.Clear();
            }
            if (PageCount < pages.Count) {
                while (PageCount < pages.Count) {
                    PageCount *= 2;
                }

                CreateImage();
                UpdateDescriptor();

                for (int i = 0; i < pages.Count; i++) {
                    UpdatePage(i);
                }

                pageUpdates.Clear();    //since image was recreated, all pages have been updated
                return;
            }

            foreach (int index in pageUpdates) {
                UpdatePage(index);
            }

            pageUpdates.Clear();
        }

        void UpdatePage(int index) {
            VkImageCopy region = new VkImageCopy();
            region.dstSubresource.aspectMask = VkImageAspectFlags.ColorBit;
            region.dstSubresource.baseArrayLayer = (uint)index;
            region.dstSubresource.layerCount = 1;
            region.dstSubresource.mipLevel = 0;
            region.extent.width = (uint)PageSize;
            region.extent.height = (uint)PageSize;
            region.extent.depth = 1;

            engine.Graphics.TransferNode.Transfer(pages[index].Bitmap, Image, region, VkImageLayout.ShaderReadOnlyOptimal);
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

            sampler = new Sampler(engine.Graphics.Device, info);
        }

        void CreateImage() {
            imageView?.Dispose();
            Image?.Dispose();

            ImageCreateInfo info = new ImageCreateInfo();
            info.imageType = VkImageType._2d;
            info.format = VkFormat.R8g8b8a8Unorm;
            info.extent.width = (uint)PageSize;
            info.extent.height = (uint)PageSize;
            info.extent.depth = 1;
            info.mipLevels = 1;
            info.arrayLayers = (uint)PageCount;
            info.samples = VkSampleCountFlags._1Bit;
            info.tiling = VkImageTiling.Optimal;
            info.usage = VkImageUsageFlags.SampledBit | VkImageUsageFlags.TransferDstBit;
            info.sharingMode = VkSharingMode.Exclusive;
            info.initialLayout = VkImageLayout.Undefined;

            Image = new Image(engine.Graphics.Device, info);

            engine.Graphics.Allocator.Free(alloc);
            alloc = engine.Graphics.Allocator.Alloc(Image.Requirements, VkMemoryPropertyFlags.DeviceLocalBit);

            Image.Bind(alloc.memory, alloc.offset);

            ImageViewCreateInfo viewInfo = new ImageViewCreateInfo();
            viewInfo.image = Image;
            viewInfo.viewType = VkImageViewType._2dArray;
            viewInfo.format = VkFormat.R8g8b8a8Unorm;
            viewInfo.subresourceRange.aspectMask = VkImageAspectFlags.ColorBit;
            viewInfo.subresourceRange.baseArrayLayer = 0;
            viewInfo.subresourceRange.layerCount = (uint)PageCount;
            viewInfo.subresourceRange.baseMipLevel = 0;
            viewInfo.subresourceRange.levelCount = 1;

            imageView = new ImageView(engine.Graphics.Device, viewInfo);
        }

        void CreateDescriptors() {
            DescriptorSetLayoutCreateInfo layoutInfo = new DescriptorSetLayoutCreateInfo();
            layoutInfo.bindings = new List<VkDescriptorSetLayoutBinding> {
                new VkDescriptorSetLayoutBinding {
                    binding = 0,
                    descriptorCount = 1,
                    descriptorType = VkDescriptorType.CombinedImageSampler,
                    stageFlags = VkShaderStageFlags.FragmentBit
                }
            };

            DescriptorLayout = new DescriptorSetLayout(engine.Graphics.Device, layoutInfo);

            DescriptorPoolCreateInfo poolInfo = new DescriptorPoolCreateInfo();
            poolInfo.maxSets = 1;
            poolInfo.poolSizes = new List<VkDescriptorPoolSize> {
                new VkDescriptorPoolSize {
                    descriptorCount = 1,
                    type = VkDescriptorType.CombinedImageSampler
                }
            };

            pool = new DescriptorPool(engine.Graphics.Device, poolInfo);

            DescriptorSetAllocateInfo setInfo = new DescriptorSetAllocateInfo();
            setInfo.descriptorSetCount = 1;
            setInfo.setLayouts = new List<DescriptorSetLayout> { DescriptorLayout };

            Descriptor = pool.Allocate(setInfo)[0];
        }

        void UpdateDescriptor() {
            DescriptorSet.Update(engine.Graphics.Device, new List<WriteDescriptorSet> {
                new WriteDescriptorSet {
                    dstBinding = 0,
                    dstSet = Descriptor,
                    descriptorType = VkDescriptorType.CombinedImageSampler,
                    imageInfo = new List<DescriptorImageInfo> {
                        new DescriptorImageInfo {
                            imageLayout = VkImageLayout.ShaderReadOnlyOptimal,
                            imageView = imageView,
                            sampler = sampler
                        }
                    }
                }
            });
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposing) {
            if (disposed) return;

            pool.Dispose();
            DescriptorLayout.Dispose();
            imageView.Dispose();
            Image.Dispose();
            engine.Graphics.Allocator.Free(alloc);
            sampler.Dispose();

            disposed = true;
        }

        ~GlyphCache() {
            Dispose(false);
        }
    }
}
