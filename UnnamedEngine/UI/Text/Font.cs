﻿using System;
using System.Collections.Generic;
using System.IO;

using SharpFont;
using MSDFGen;

namespace UnnamedEngine.UI.Text {
    public class Font : IDisposable {
        bool disposed;

        static Library library;
        Face face;

        public Font(byte[] data, int faceIndex) {
            if (library == null) {
                library = new Library();
            }

            lock (library) {
                face = new Face(library, data, faceIndex);
            }
        }

        public Font(byte[] data) : this(data, 0) { }
        public Font(string path) : this(File.ReadAllBytes(path), 0) { }
        public Font(string path, int faceIndex) : this(File.ReadAllBytes(path), faceIndex) { }

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
