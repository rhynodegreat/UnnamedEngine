using System;
using System.Collections.Generic;

using CSGL.Graphics;

using UnnamedEngine.Rendering;

namespace Test {
    public enum LightType {
        Ambient,
        Directional,
    }

    public class Light {
        public LightType Type { get; set; }
        public Color4 Color { get; set; }
        public Transform Transform { get; set; }

        public Light() {
            Transform = new Transform();
        }
    }
}
