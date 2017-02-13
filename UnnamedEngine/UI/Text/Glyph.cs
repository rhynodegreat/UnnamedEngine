using System;
using System.Collections.Generic;

using MSDFGen;

namespace UnnamedEngine.UI.Text {
    public class Glyph {
        public Font Font { get; private set; }
        public Shape Shape { get; private set; }
        public int Codepoint { get; private set; }

        internal Glyph(Font font, Shape shape, int codepoint) {
            Font = font;
            Shape = shape;
            Codepoint = codepoint;
        }
    }
}
