using System;
using System.Collections.Generic;

using CSGL.Graphics;
using CSGL.Math;

namespace UnnamedEngine.UI.Text {
    internal class GlyphCachePage {
        List<Rectanglei> free;
        List<Rectanglei> taken;
        int width;
        int height;

        public Bitmap<Color4b> Bitmap { get; private set; }

        public GlyphCachePage(int width, int height) {
            this.width = width;
            this.height = height;

            Bitmap = new Bitmap<Color4b>(width, height);
            free = new List<Rectanglei>();
            free.Add(new Rectanglei(0, 0, width, height));
            taken = new List<Rectanglei>();
        }

        public bool AttemptAdd(ref Rectanglei rect) {
            for (int i = 0; i < free.Count; i++) {
                Rectanglei f = free[i];
                Rectanglei temp = rect;
                for (temp.X = f.X; temp.Right < width; temp.X++) {
                    for (temp.Y = f.Y; temp.Bottom < height; temp.Y++) {
                        //TODO: fix this
                        if (Test(temp)) {
                            rect = temp;
                            taken.Add(rect);

                            for (int j = free.Count - 1; j >= 0; j--) {
                                if (free[j].Intersects(rect)) {
                                    Split(j, rect);
                                }
                            }

                            return true;
                        }
                    }
                }
            }

            return false;
        }

        bool Test(Rectanglei rect) {
            for (int i = taken.Count - 1; i >= 0; i--) {    //iterates backwards, hopefully intersecting rectangles are closer to the end of the list
                if (taken[i].Intersects(rect)) return false;
            }

            return true;
        }

        void Split(int i, Rectanglei rect) {
            Rectanglei existing = free[i];

            int freeTop = 0;
            int freeLeft = 0;
            int freeRight = 0;
            int freeBottom = 0;

            freeTop = rect.Top - existing.Top;
            freeLeft = rect.Left - existing.Left;
            freeRight = existing.Right - rect.Right;
            freeBottom = existing.Bottom - rect.Bottom;

            free.RemoveAt(i);
            if (freeTop > 0) free.Add(new Rectanglei(
                rect.X - freeLeft > 0 ? 0 : freeLeft,
                rect.Y - freeTop,
                rect.Width + freeRight > 0 ? 0 : freeRight,
                freeTop
            ));
            if (freeLeft > 0) free.Add(new Rectanglei(
                existing.X,
                existing.Y,
                freeLeft,
                rect.Height + freeTop + freeBottom
            ));
            if (freeRight > 0) free.Add(new Rectanglei(
                rect.X + rect.Width,
                rect.Y - freeTop,
                freeRight,
                rect.Height + freeTop + freeBottom
            ));
            if (freeBottom > 0) free.Add(new Rectanglei(
                rect.X - freeLeft > 0 ? 0 : freeLeft,
                rect.Y + rect.Height,
                rect.Width + freeRight > 0 ? 0 : freeRight,
                freeBottom
            ));
        }
    }
}
