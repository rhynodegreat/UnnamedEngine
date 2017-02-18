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
    public struct GlyphInfo {
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

        bool disposed;

        Engine engine;
        List<GlyphCachePage> pages;
        HashSet<int> pageUpdates;
        Dictionary<GlyphPair, GlyphInfo> infoMap;
        VkaAllocation alloc;
        DescriptorPool pool;
        public List<Bitmap<Color4b>> Bitmaps { get; private set; }

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
            infoMap = new Dictionary<GlyphPair, GlyphInfo>();
            pageUpdates = new HashSet<int>();
            Bitmaps = new List<Bitmap<Color4b>>();
            PageSize = pageSize;
            Range = range;
            PageCount = pageCount;

            CreateImage();
            CreateDescriptors();
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
            GlyphInfo info = new GlyphInfo();
            info.offset = new Vector2(glyph.Metrics.bearingX, glyph.Metrics.height - glyph.Metrics.bearingY);
            info.size = new Vector2(glyph.Metrics.width, glyph.Metrics.height);

            Rectanglei rect = new Rectanglei(0, 0, (int)Math.Ceiling(info.size.X), (int)Math.Ceiling(info.size.Y));

            AddToPage(glyph, ref info, rect);

            infoMap.Add(pair, info);
        }

        void AddToPage(Glyph glyph, ref GlyphInfo info, Rectanglei rect) {
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
            Bitmaps.Add(newPage.Bitmap);
            Render(newPage, pages.Count - 1, glyph, info, rect);
            info.uvPosition = new Vector3(rect.X, rect.Y, pages.Count - 1);
        }

        void Render(GlyphCachePage page, int pageIndex, Glyph glyph, GlyphInfo info, Rectanglei rect) {
            MSDF.GenerateMSDF(page.Bitmap, glyph.Shape, new Rectangle(rect), Range, Vector2.One, new Vector2(-info.offset.X, info.offset.Y), 1.000001);
            pageUpdates.Add(pageIndex);
        }

        public GlyphInfo GetInfo(Font font, int codepoint) {
            return infoMap[new GlyphPair(font, codepoint)];
        }

        public void Update() {
            if (PageCount < pages.Count) {
                PageCount *= 2;
                CreateImage();

                pageUpdates.Clear();    //since image was recreated, all pages have been updated
                return;
            }

            foreach (int index in pageUpdates) {
                VkImageCopy region = new VkImageCopy();
                region.dstSubresource.aspectMask = VkImageAspectFlags.ColorBit;
                region.dstSubresource.baseArrayLayer = (uint)index;
                region.dstSubresource.layerCount = 1;
                region.dstSubresource.mipLevel = 0;

                engine.Graphics.TransferNode.Transfer(pages[index].Bitmap, Image, region, VkImageLayout.ShaderReadOnlyOptimal);
            }

            pageUpdates.Clear();
        }

        void CreateImage() {
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
            alloc = engine.Graphics.Allocator.Alloc(Image.MemoryRequirements, VkMemoryPropertyFlags.DeviceLocalBit);

            Image.Bind(alloc.memory, alloc.offset);
        }

        void CreateDescriptors() {
            DescriptorSetLayoutCreateInfo layoutInfo = new DescriptorSetLayoutCreateInfo();
            layoutInfo.bindings = new List<VkDescriptorSetLayoutBinding> {
                new VkDescriptorSetLayoutBinding {
                    binding = 0,
                    descriptorCount = 1,
                    descriptorType = VkDescriptorType.SampledImage,
                    stageFlags = VkShaderStageFlags.FragmentBit
                },
                new VkDescriptorSetLayoutBinding {
                    binding = 1,
                    descriptorCount = 1,
                    descriptorType = VkDescriptorType.Sampler,
                    stageFlags = VkShaderStageFlags.FragmentBit
                }
            };

            DescriptorLayout = new DescriptorSetLayout(engine.Graphics.Device, layoutInfo);

            DescriptorPoolCreateInfo poolInfo = new DescriptorPoolCreateInfo();
            poolInfo.maxSets = 1;
            poolInfo.poolSizes = new List<VkDescriptorPoolSize> {
                new VkDescriptorPoolSize {
                    descriptorCount = 1,
                    type = VkDescriptorType.SampledImage
                },
                new VkDescriptorPoolSize {
                    descriptorCount = 1,
                    type = VkDescriptorType.Sampler
                }
            };

            pool = new DescriptorPool(engine.Graphics.Device, poolInfo);

            DescriptorSetAllocateInfo setInfo = new DescriptorSetAllocateInfo();
            setInfo.descriptorSetCount = 1;
            setInfo.setLayouts = new List<DescriptorSetLayout> { DescriptorLayout };

            Descriptor = pool.Allocate(setInfo)[0];
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposing) {
            if (disposed) return;

            pool.Dispose();
            DescriptorLayout.Dispose();
            Image.Dispose();
            engine.Graphics.Allocator.Free(alloc);

            disposed = true;
        }

        ~GlyphCache() {
            Dispose(false);
        }
    }
}
