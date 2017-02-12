using System;
using System.Collections.Generic;

using CSGL.Graphics;
using CSGL.Math;

namespace UnnamedEngine.UI.Text {
    internal class FontCachePage {
        bool[] free;
        int width;
        int height;

        public Bitmap<Color3> Bitmap { get; private set; }

        public FontCachePage(int width, int height) {
            this.width = width;
            this.height = height;

            Bitmap = new Bitmap<Color3>(width, height);
            free = new bool[width * height];

            for (int i = 0; i < width * height; i++) {
                free[i] = true;
            }
        }

        int GetIndex(int x, int y) {
            return x + y * width;
        }

        public bool CanAdd(Rectangle rect) {
            return true;
        }
    }
}
