using System;
using System.Collections.Generic;
using System.Numerics;

using CSGL.Graphics;
using CSGL.Vulkan;
using CSGL.Math;

using MSDFGen;

using UnnamedEngine.Core;

namespace UnnamedEngine.UI.Text {
    public struct GlyphInfo {
        public Vector2 offset;
        public Vector2 size;
        public Vector3 uvPosition;
    }

    public class GlyphCache : IDisposable {
        struct GlyphPair {
            public Font font;
            public int codepoint;

            public GlyphPair(Font font, int codepoint) {
                this.font = font;
                this.codepoint = codepoint;
            }

            public override int GetHashCode() {
                return font.GetHashCode() ^ codepoint.GetHashCode();
            }
        }

        bool disposed;

        Engine engine;
        List<GlyphCachePage> pages;
        Dictionary<GlyphPair, GlyphInfo> infoMap;
        int padding = 1;
        public List<Bitmap<Color3b>> Bitmaps { get; private set; }

        public int PageSize { get; private set; }
        public float Range { get; private set; }

        public GlyphCache(Engine engine, int pageSize, float range) {
            if (engine == null) throw new ArgumentNullException(nameof(engine));

            this.engine = engine;
            pages = new List<GlyphCachePage>();
            infoMap = new Dictionary<GlyphPair, GlyphInfo>();
            Bitmaps = new List<Bitmap<Color3b>>();
            PageSize = pageSize;
            Range = range;
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

            Rectanglei rect = new Rectanglei(0, 0, (int)Math.Ceiling(info.size.X) + padding * 2, (int)Math.Ceiling(info.size.Y) + padding * 2);

            AddToPage(glyph, ref info, ref rect);

            infoMap.Add(pair, info);
        }

        void AddToPage(Glyph glyph, ref GlyphInfo info, ref Rectanglei rect) {
            for (int i = 0; i < pages.Count; i++) {
                if (pages[i].AttemptAdd(ref rect)) {
                    Render(pages[i], i, glyph, ref info, rect);
                    return;
                }
            }

            GlyphCachePage newPage = new GlyphCachePage(PageSize, PageSize);
            newPage.AttemptAdd(ref rect);
            pages.Add(newPage);
            Bitmaps.Add(newPage.Bitmap);
            Render(newPage, pages.Count - 1, glyph, ref info, rect);
        }

        void Render(GlyphCachePage page, int pageIndex, Glyph glyph, ref GlyphInfo info, Rectanglei rect) {
            MSDF.GenerateMSDF(page.Bitmap, glyph.Shape, new Rectangle(rect), Range, Vector2.One, new Vector2(-info.offset.X + padding, info.offset.Y + padding), 1.000001);
            info.uvPosition = new Vector3(rect.X, rect.Y, pageIndex);
        }

        public GlyphInfo GetInfo(Font font, int codepoint) {
            return infoMap[new GlyphPair(font, codepoint)];
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposing) {
            if (disposed) return;

            disposed = true;
        }

        ~GlyphCache() {
            Dispose(false);
        }
    }
}
