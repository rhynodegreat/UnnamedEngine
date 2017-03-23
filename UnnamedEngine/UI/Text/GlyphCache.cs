using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;

using CSGL.Graphics;
using CSGL.Vulkan;
using CSGL.Math;

using MSDFGen;

using UnnamedEngine.Core;
using UnnamedEngine.Utilities;

namespace UnnamedEngine.UI.Text {
    public struct GlyphMetrics {
        public Vector3 offset;
        public Vector3 size;
        public Vector3 uvPosition;
        public float advance;
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

        struct RenderInfo {
            public int pageIndex;
            public Glyph glyph;
            public GlyphMetrics metrics;
            public Rectanglei rect;

            public RenderInfo(int pageIndex, Glyph glyph, GlyphMetrics metrics, Rectanglei rect) {
                this.pageIndex = pageIndex;
                this.glyph = glyph;
                this.metrics = metrics;
                this.rect = rect;
            }
        }

        bool disposed;

        Engine engine;
        List<GlyphCachePage> pages;
        HashSet<int> pageUpdates;
        List<GlyphInfo> glyphUpdates;
        HashSet<GlyphPair> glyphUpdateSet;
        object glyphUpdateLocker;
        Dictionary<GlyphPair, GlyphMetrics> infoMap;
        List<RenderInfo> renderQueue;
        DescriptorPool pool;
        ImageView imageView;
        Sampler sampler;
        float errorThreshold;

        public int PageSize { get; private set; }
        public int PageCount { get; private set; }
        public float Range { get; private set; }
        public int Padding { get; private set; }
        public float Scale { get; private set; }
        public float Threshold { get; private set; }
        public Image Image { get; private set; }
        public DescriptorSetLayout DescriptorLayout { get; private set; }
        public DescriptorSet Descriptor { get; private set; }

        public GlyphCache(Engine engine, int pageCount, int pageSize, float range, int padding, float scale, float threshold) {
            if (engine == null) throw new ArgumentNullException(nameof(engine));

            this.engine = engine;
            pages = new List<GlyphCachePage>();
            infoMap = new Dictionary<GlyphPair, GlyphMetrics>();
            pageUpdates = new HashSet<int>();
            glyphUpdates = new List<GlyphInfo>();
            glyphUpdateSet = new HashSet<GlyphPair>();
            glyphUpdateLocker = new object();
            renderQueue = new List<RenderInfo>();
            PageSize = pageSize;
            Range = range;
            PageCount = pageCount;
            Padding = padding;
            Scale = scale;
            Threshold = threshold;

            errorThreshold = Threshold / (Scale * Range);

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
            if (glyphUpdateSet.Contains(pair)) return;

            Glyph glyph = font.GetGlyph(codepoint);
            GlyphMetrics metrics = new GlyphMetrics();
            metrics.offset = new Vector3(glyph.Metrics.bearingX - Range, glyph.Metrics.height - glyph.Metrics.bearingY + Range, 0) * Scale;
            metrics.size = new Vector3(glyph.Metrics.width + Range * 2, glyph.Metrics.height + Range * 2, 0) * Scale;
            metrics.advance = glyph.Metrics.advance * Scale;

            lock (glyphUpdateLocker) {
                glyphUpdates.Add(new GlyphInfo(font, codepoint, glyph, metrics));
                glyphUpdateSet.Add(pair);
            }
        }

        void AddGlyph(GlyphInfo info) {
            Font font = info.font;
            int codepoint = info.codepoint;
            Glyph glyph = info.glyph;

            Rectanglei rect = new Rectanglei(0, 0, (int)Math.Ceiling(info.metrics.size.X + Range * Scale / 2) + Padding * 2, (int)Math.Ceiling(info.metrics.size.Y + Range * Scale / 2) + Padding * 2);

            AddToPage(glyph, ref info.metrics, rect);

            infoMap.Add(new GlyphPair(info.font, info.codepoint), info.metrics);
        }

        void AddToPage(Glyph glyph, ref GlyphMetrics info, Rectanglei rect) {
            for (int i = 0; i < pages.Count; i++) {
                if (pages[i].AttemptAdd(ref rect)) {
                    QueueRender(i, glyph, info, rect);
                    info.uvPosition = new Vector3(rect.X + Padding, rect.Y + Padding, i);
                    return;
                }
            }

            GlyphCachePage newPage = new GlyphCachePage(PageSize, PageSize);
            newPage.AttemptAdd(ref rect);
            pages.Add(newPage);
            QueueRender(pages.Count - 1, glyph, info, rect);
            info.uvPosition = new Vector3(rect.X + Padding, rect.Y + Padding, pages.Count - 1);
        }

        void QueueRender(int pageIndex, Glyph glyph, GlyphMetrics metrics, Rectanglei rect) {
            renderQueue.Add(new RenderInfo(pageIndex, glyph, metrics, rect));
        }

        void Render(int i) {
            RenderInfo info = renderQueue[i];
            int pageIndex = info.pageIndex;
            Glyph glyph = info.glyph;
            GlyphMetrics metrics = info.metrics;
            Rectanglei rect = info.rect;

            Rectangle rectf = new Rectangle(rect.X + Padding, rect.Y + Padding, rect.Width - Padding, rect.Height - Padding);
            MSDF.GenerateMSDF(pages[pageIndex].Bitmap, glyph.Shape, rectf, Range, new Vector2(Scale, Scale), new Vector2(-metrics.offset.X + Padding + Range * Scale / 4, metrics.offset.Y + Padding + Range * Scale / 4), Threshold);
            MSDF.CorrectErrors(pages[pageIndex].Bitmap, rect, new Vector2(errorThreshold, errorThreshold));

            pageUpdates.Add(pageIndex);
        }

        public GlyphMetrics GetInfo(Font font, int codepoint) {
            return infoMap[new GlyphPair(font, codepoint)];
        }

        public void Update() {
            lock (glyphUpdateLocker) {
                if (glyphUpdates.Count > 0) {
                    glyphUpdates.Sort((GlyphInfo a, GlyphInfo b) => {
                        float aArea = a.metrics.size.X * a.metrics.size.Y;
                        float bArea = b.metrics.size.X * b.metrics.size.Y;
                        return bArea.CompareTo(aArea);
                    });

                    for (int i = 0; i < glyphUpdates.Count; i++) {
                        AddGlyph(glyphUpdates[i]);
                    }

                    Parallel.For(0, renderQueue.Count, Render);

                    renderQueue.Clear();
                    glyphUpdates.Clear();
                }

                //pageUpdates does not need to be locked. It's only modified through the Parallel.For loop above
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
                glyphUpdateSet.Clear();
            }
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

            engine.Memory.TransferNode.Transfer(pages[index].Bitmap, Image, region, VkImageLayout.ShaderReadOnlyOptimal);
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
            if (Image != null) engine.Memory.FreeDevice(Image);

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

            Image = engine.Memory.AllocDevice(info);

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
            engine.Memory.FreeDevice(Image);
            sampler.Dispose();

            disposed = true;
        }

        ~GlyphCache() {
            Dispose(false);
        }
    }
}
