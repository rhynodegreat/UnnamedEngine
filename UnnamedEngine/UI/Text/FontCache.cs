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
        public Vector2 uvExtent;
    }

    public class FontCache : IDisposable {
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
        List<FontCachePage> pages;
        Dictionary<GlyphPair, GlyphInfo> infoMap;
        int padding = 2;
        double range = 4;
        int pageSize = 1024;

        public FontCache(Engine engine) {
            if (engine == null) throw new ArgumentNullException(nameof(engine));

            this.engine = engine;
            pages = new List<FontCachePage>();
            infoMap = new Dictionary<GlyphPair, GlyphInfo>();
        }

        public void AddGlyph(Font font, int codepoint) {
            var pair = new GlyphPair(font, codepoint);
            if (infoMap.ContainsKey(pair)) return;

            Glyph glyph = font.GetGlyph(codepoint);
            GlyphInfo info = new GlyphInfo();
            info.offset = new Vector2(glyph.Metrics.bearingX, glyph.Metrics.height - glyph.Metrics.bearingY);
            info.size = new Vector2(glyph.Metrics.width, glyph.Metrics.height);

            Rectanglei rect = new Rectanglei(0, 0, (int)Math.Ceiling(info.size.X) + padding, (int)Math.Ceiling(info.size.Y) + padding);

            AddToPage(glyph, ref info, ref rect);
        }

        void AddToPage(Glyph glyph, ref GlyphInfo info, ref Rectanglei rect) {
            for (int i = 0; i < pages.Count; i++) {
                if (pages[i].AttemptAdd(ref rect)) {
                    MSDF.GenerateMSDF(pages[i].Bitmap, glyph.Shape, new Rectangle(rect), range, Vector2.One, -info.offset, 1.000001);
                    info.uvPosition = new Vector3(rect.X, rect.Y, i);
                    return;
                }
            }

            FontCachePage newPage = new FontCachePage(pageSize, pageSize);
            MSDF.GenerateMSDF(newPage.Bitmap, glyph.Shape, new Rectangle(rect), range, Vector2.One, -info.offset, 1.000001);
            info.uvPosition = new Vector3(rect.X, rect.Y, pages.Count);
            pages.Add(newPage);
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

        ~FontCache() {
            Dispose(false);
        }
    }
}
