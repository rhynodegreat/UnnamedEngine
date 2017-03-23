using System;
using System.Collections.Generic;

using CSGL.Graphics;

using UnnamedEngine.UI.Text;

namespace UnnamedEngine.UI {
    public class Label : UIElement {
        public string Text { get; set; }
        public Font Font { get; set; }
        public float FontSize { get; set; }
        public float Outline { get; set; }
        public float Thickness { get; set; }
        public Color4 Color { get; set; }
        public Color4 OutlineColor { get; set; }
    }
}
