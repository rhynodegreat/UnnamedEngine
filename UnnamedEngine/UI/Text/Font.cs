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
                Shape shape = MSDF.LoadGlyph(face, codepoint);
                Glyph result = new Glyph(this, shape, codepoint, glyphIndex);
                glyphMap.Add(codepoint, result);

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
