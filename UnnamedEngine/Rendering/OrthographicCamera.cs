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
            Recreate((int)width, (int)height);
        }

        public OrthographicCamera(float width, float height, float near, float far, Transform transform) : base(transform) {
            Width = width;
            Height = height;
            Near = near;
            Far = far;
            Recreate((int)width, (int)height);
        }

        public override void Recreate(int width, int height) {
            Width = width;
            Height = height;
            projection = CreateOrthographic(0, Width, Height, 0, Near, Far);
        }

        protected override void Update() {
            view = Matrix4x4.CreateLookAt(Transform.Position, Transform.Position + Transform.Forward, Transform.Up);
            ProjectionView = view * projection;
        }

        Matrix4x4 CreateOrthographic(float left, float right, float top, float bottom, float near, float far) {
            return new Matrix4x4(
                2 / (right - left), 0, 0, 0,
                0, 2 / (top - bottom), 0, 0,
                0, 0, 2 / (near - far), 0,
                -(right + left) / (right - left), -(top + bottom) / (top - bottom), -(far + near) / (far - near), 1
            );
        }
    }
}
