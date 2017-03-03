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
        
        float lookX, lookY;

        public FreeCam(Engine engine, Camera camera) {
            if (engine == null) throw new ArgumentNullException(nameof(engine));

            cam = camera;
            input = engine.Window.Input;

            engine.FrameLoop.Add(this);
        }

        public void Update(float delta) {
            float x = 0;
            float y = 0;
            float z = 0;

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

            if (input.FirstDown(KeyCode.Escape)) {
                input.CursorMode = CursorMode.Normal;
            }

            if (input.FirstDown(MouseButton.Button1)) {
                input.CursorMode = CursorMode.Disabled;
            }

            if (x != 0 || y != 0 || z != 0) {
                Vector3 dir = Vector3.Normalize(new Vector3(x, y, z));
                x = dir.X;
                y = dir.Y;
                z = dir.Z;
            }

            lookX += input.MouseDelta.X;
            lookY += input.MouseDelta.Y;
            
            lookY = Math.Min(Math.Max(lookY, -90), 90);

            cam.Transform.Position = cam.Transform.Position
                + x * delta * cam.Transform.Right
                + y * delta * cam.Transform.Up
                + z * delta * cam.Transform.Forward;

            float radY = lookY * (float)(Math.PI / 180f);
            float radX = lookX * (float)(Math.PI / 180f);

            cam.Transform.Rotation = Quaternion.CreateFromYawPitchRoll(0, radY, 0)
                * Quaternion.CreateFromYawPitchRoll(radX, 0, 0);
        }
    }
}
