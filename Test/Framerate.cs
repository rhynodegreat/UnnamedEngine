using System;
using System.Collections.Generic;
using System.Numerics;

using UnnamedEngine.Core;
using UnnamedEngine.UI;

namespace Test {
    public class Framerate : ISystem {
        Panel panel;

        public int Priority { get; set; }

        public Framerate(Engine engine, Panel panel) {
            engine.FrameLoop.Add(this);
            this.panel = panel;
        }

        public void Update(float delta) {
            float fps = 1f / delta;
            panel.Size = new Vector2(fps, panel.Size.Y);
        }
    }
}
