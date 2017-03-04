using System;
using System.Collections.Generic;
using System.Numerics;

namespace UnnamedEngine.Rendering {
    public class OrthographicCamera : Camera {
        Matrix4x4 projection;
        Matrix4x4 view;

        public float Width { get; set; }
        public float Height { get; set; }
        public float Near { get; set; }
        public float Far { get; set; }

        public OrthographicCamera(float width, float height, float near, float far) {
            Width = width;
            Height = height;
            Near = near;
            Far = far;
        }

        public OrthographicCamera(float width, float height, float near, float far, Transform transform) : base(transform) {
            Width = width;
            Height = height;
            Near = near;
            Far = far;
        }

        protected override void Update() {
            projection = Matrix4x4.CreateOrthographic(Width, Height, -Near, -Far);
            projection.M22 *= -1;
            view = Matrix4x4.CreateLookAt(Transform.Position, Transform.Position + Transform.Forward, Transform.Up);
            ProjectionView = view * projection;
        }
    }
}
