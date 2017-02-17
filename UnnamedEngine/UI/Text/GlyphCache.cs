﻿using System;
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
        Dictionary<GlyphPair, GlyphInfo> infoMap;
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
