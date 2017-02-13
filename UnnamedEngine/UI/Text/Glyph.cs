using System;
using System.Collections.Generic;

using MSDFGen;

namespace UnnamedEngine.UI.Text {
    public struct Metrics {
        public float width;
        public float height;
        public float bearingX;
        public float bearingY;
        public float advance;

        public Metrics(float width, float height, float bearingX, float bearingY, float advance) {
            this.width = width;
            this.height = height;
            this.bearingX = bearingX;
            this.bearingY = bearingY;
            this.advance = advance;
        }
    }

    public class Glyph {
        public Font Font { get; private set; }
        public Shape Shape { get; private set; }
        public int Codepoint { get; private set; }
        public int GlyphIndex { get; private set; }
        public Metrics Metrics { get; private set; }

        internal Glyph(Font font, Shape shape, int codepoint, int glyphIndex, Metrics info) {
            Font = font;
            Shape = shape;
            Codepoint = codepoint;
            GlyphIndex = glyphIndex;
            Metrics = info;
        }
    }
}
