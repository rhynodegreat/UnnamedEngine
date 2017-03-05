using System;
using System.Collections.Generic;
using System.Numerics;

using CSGL.Vulkan;

namespace UnnamedEngine.Rendering {
    public abstract class Camera {
        public DescriptorSetLayout Layout { get; internal set; }
        public DescriptorSet Descriptor { get; internal set; }
        public int Index { get; internal set; }
        public Matrix4x4 ProjectionView { get; protected set; }
        public Transform Transform { get; private set; }

        public Camera() {
            Transform = new Transform();
        }

        public Camera(Transform transform) {
            if (transform == null) throw new ArgumentNullException(nameof(transform));
            Transform = transform;
        }

        public abstract void Recreate(int width, int height);
        protected abstract void Update();

        internal void InternalUpdate() {
            Update();
        }
    }
}
