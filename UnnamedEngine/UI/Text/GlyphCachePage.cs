using System;
using System.Collections.Generic;

using CSGL.Graphics;
using CSGL.Math;

namespace UnnamedEngine.UI.Text {
    internal class GlyphCachePage {
        List<Rectanglei> free;
        int width;
        int height;

        public Bitmap<Color3b> Bitmap { get; private set; }

        public GlyphCachePage(int width, int height) {
            this.width = width;
            this.height = height;

            Bitmap = new Bitmap<Color3b>(width, height);
            free = new List<Rectanglei>();
            free.Add(new Rectanglei(0, 0, width, height));
        }

        public bool AttemptAdd(ref Rectanglei rect) {
            for (int i = 0; i < free.Count; i++) {
                if (free[i].Width >= rect.Width && free[i].Height >= rect.Height) {
                    Rectanglei node = free[i];
                    free.RemoveAt(i);

                    int freeRight = node.Width - rect.Width;
                    int freeBottom = node.Height - rect.Height;

                    if (freeRight > 0) {
                        free.Add(new Rectanglei(node.X + rect.Width, node.Y, freeRight, rect.Height + freeBottom));
                    }

                    if (freeBottom > 0) {
                        free.Add(new Rectanglei(node.X, node.Y + rect.Height, rect.Width, freeBottom));
                    }

                    rect.X = node.X;
                    rect.Y = node.Y;

                    return true;
                }
            }

            return false;
        }
    }
}
