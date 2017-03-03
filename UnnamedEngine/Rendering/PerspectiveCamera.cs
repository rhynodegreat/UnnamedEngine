using System;
using System.Collections.Generic;
using System.Numerics;

using UnnamedEngine.Core;

namespace UnnamedEngine.Rendering {
    public class PerspectiveCamera : Camera {
        Window window;

        Matrix4x4 projection;
        Matrix4x4 view;
        
        public float FOV { get; set; }
        public float Near { get; set; }
        public float Far { get; set; }
        public bool Infinite { get; set; }

        public PerspectiveCamera(Window window, float fov, float near, float far, bool infinite) : base(new Transform()) {
            if (window == null) throw new ArgumentNullException(nameof(window));
            
            this.window = window;

            FOV = fov;
            Near = near;
            Far = far;
            Infinite = infinite;

            Update();
        }

        public PerspectiveCamera(Window window, float fov, float near) : this(window, fov, near, 0, true) { }
        public PerspectiveCamera(Window window, float fov, float near, float far) : this(window, fov, near, far, false) { }

        protected override void Update() {
            if (Infinite) {
                projection = CreatePerspectiveInfinite(FOV, window.Width / (float)window.Height, Near);
            } else {
                projection = CreatePerspective(FOV, window.Width / (float)window.Height, Near, Far);
            }

            projection.M22 *= -1;
            view = Matrix4x4.CreateLookAt(Transform.Position, Transform.Position + Transform.Forward, Transform.Up);

            ProjectionView = view * projection;
        }

        Matrix4x4 CreatePerspectiveInfinite(float fov, float aspect, float near) {
            //creates projection matrix with reversed Z and infinite far plane
            //https://nlguillemot.wordpress.com/2016/12/07/reversed-z-in-opengl/
            //http://dev.theomader.com/depth-precision/

            float fovRad = fov * (float)(Math.PI / 180);
            float f = 1f / (float)Math.Tan(fovRad / 2);

            return new Matrix4x4(
                f / aspect, 0, 0, 0,
                0, f, 0, 0,
                0, 0, 0, -1f,
                0, 0, near, 0);
        }

        Matrix4x4 CreatePerspective(float fov, float aspect, float near, float far) {
            //creates projection matrix with reversed Z and infinite far plane
            //http://dev.theomader.com/depth-precision/

            float fovRad = fov * (float)(Math.PI / 180);
            float f = 1f / (float)Math.Tan(fovRad / 2);

            float a = -(Far / (Near - Far));

            return new Matrix4x4(
                f / aspect, 0, 0, 0,
                0, f, 0, 0,
                0, 0, a - 1, -1f,
                0, 0, near * a, 0);
        }
    }
}
