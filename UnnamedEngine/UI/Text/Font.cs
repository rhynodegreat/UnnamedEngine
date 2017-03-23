using System;
using System.Collections.Generic;
using System.IO;

using SharpFont;
using MSDFGen;

namespace UnnamedEngine.UI.Text {
    public class Font : IDisposable {
        bool disposed;

        static Library library;
        Face face;
        Dictionary<int, Glyph> glyphMap;
        Glyph unknownGlyph;

        public Font(byte[] data, int faceIndex) {
            if (library == null) {
                library = new Library();
            }

            lock (library) {
                face = new Face(library, data, faceIndex);
            }

            glyphMap = new Dictionary<int, Glyph>();
        }

        public Font(byte[] data) : this(data, 0) { }
        public Font(string path) : this(File.ReadAllBytes(path), 0) { }
        public Font(string path, int faceIndex) : this(File.ReadAllBytes(path), faceIndex) { }

        public Glyph GetGlyph(int codepoint) {
            if (glyphMap.ContainsKey(codepoint)) {
                return glyphMap[codepoint];
            } else {
                return LoadGlyph(codepoint);
            }
        }

        Glyph LoadGlyph(int codepoint) {
            lock (face) {
                int glyphIndex = (int)face.GetCharIndex((uint)codepoint);
                if (glyphIndex == 0 && unknownGlyph != null) {
                    glyphMap.Add(codepoint, unknownGlyph);
                }

                Shape shape = MSDF.LoadGlyph(face, codepoint);
                shape.InverseYAxis = true;
                MSDF.EdgeColoringSimple(shape, 3, 0);

                SharpFont.GlyphMetrics metrics = face.Glyph.Metrics;
                float width = metrics.Width.ToSingle();
                float height = metrics.Height.ToSingle();
                float bearingX = metrics.HorizontalBearingX.ToSingle();
                float bearingY = metrics.HorizontalBearingY.ToSingle();
                float advance = metrics.HorizontalAdvance.ToSingle();

                Metrics info = new Metrics(width, height, bearingX, bearingY, advance);

                Glyph result = new Glyph(this, shape, glyphIndex, info);
                glyphMap.Add(codepoint, result);
                if (glyphIndex == 0) unknownGlyph = result;

                return result;
            }
        }

        public void Dispose() {
            Dispose(false);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposing) {
            if (disposed) return;

            lock (library) {
                face.Dispose();
            }

            disposed = true;
        }

        ~Font() {
            Dispose(false);
        }
    }
}
