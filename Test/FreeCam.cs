using System;
using System.Collections.Generic;
using System.Numerics;

using CSGL.Input;

using UnnamedEngine.Core;
using UnnamedEngine.Rendering;

namespace Test {
    public class FreeCam : ISystem {
        public int Priority { get; set; }

        Camera cam;
        Input input;

        float x, y, z;
        float lookX, lookY;

        public FreeCam(Engine engine) {
            if (engine == null) throw new ArgumentNullException(nameof(engine));

            cam = engine.Camera;
            input = engine.Window.Input;

            engine.FrameLoop.Add(this);
        }

        public void Update(float delta) {
            x = 0;
            y = 0;
            z = 0;

            if (input.Hold(KeyCode.W)) {
                z += 1;
            }
            if (input.Hold(KeyCode.S)) {
                z -= 1;
            }
            if (input.Hold(KeyCode.A)) {
                x -= 1;
            }
            if (input.Hold(KeyCode.D)) {
                x += 1;
            }
            if (input.Hold(KeyCode.Q)) {
                y -= 1;
            }
            if (input.Hold(KeyCode.E)) {
                y += 1;
            }

            lookX += input.MouseDelta.X / 64f;
            lookY += input.MouseDelta.Y / 64f;
            
            lookY = Math.Min(Math.Max(lookY, (float)-Math.PI / 2), (float)Math.PI / 2);

            cam.Transform.Position = cam.Transform.Position
                + x * delta * cam.Transform.Right
                + y * delta * cam.Transform.Up
                + z * delta * cam.Transform.Forward;

            cam.Transform.Rotation = Quaternion.CreateFromYawPitchRoll(0, lookY, 0)
                * Quaternion.CreateFromYawPitchRoll(lookX, 0, 0);
        }
    }
}
