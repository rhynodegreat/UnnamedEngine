using System;
using System.Collections.Generic;
using System.Numerics;

using UnnamedEngine.Core;
using UnnamedEngine.UI;

namespace Test {
    public class Framerate : ISystem {
        Panel panel;
        Label label;
        float counter;

        public int Priority { get; set; }

        public Framerate(Engine engine, Panel panel, Label label) {
            engine.FrameLoop.Add(this);
            this.panel = panel;
            this.label = label;
        }

        public void Update(float delta) {
            float fps = 1f / delta;
            panel.Size = new Vector2(fps, panel.Size.Y);
            counter += delta;
            if (counter >= 1f/4) {
                label.Text = string.Format("{0:n1}", fps);
                counter = 0;
            }
        }
    }
}
