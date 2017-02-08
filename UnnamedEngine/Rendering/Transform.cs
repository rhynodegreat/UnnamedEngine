using System;
using System.Collections.Generic;
using System.Numerics;

namespace UnnamedEngine.Rendering {
    public class Transform {
        Transform parent;
        List<Transform> children;
        Matrix4x4 worldTransform;

        public Transform() {
            children = new List<Transform>();
            worldTransform = Matrix4x4.Identity;
        }

        public Transform Parent {
            get {
                return parent;
            }
            set {
                lock (this)
                lock (value) {
                    if (value.IsDescendantOf(this)) throw new TransformException("Can not set parent of transform to it's descendant");
                    parent?.RemoveChild(this);
                    parent = value;
                    parent?.AddChild(this);
                }
            }
        }

        public Matrix4x4 WorldTransform {
            get {
                return worldTransform;
            }
            set {
                lock (this) {
                    RecalcChildrenTransform(value);
                    worldTransform = value;
                }
            }
        }

        public Matrix4x4 LocalTransform {
            get {
                if (parent == null) return WorldTransform;
                Matrix4x4 invParent;
                Matrix4x4.Invert(parent.WorldTransform, out invParent);
                return WorldTransform * invParent;
            }
            set {
                lock (this) {
                    WorldTransform = value * parent.WorldTransform;
                }
            }
        }

        public Vector3 Position {
            get {
                return WorldTransform.Translation;
            }
            set {
                Vector3 pos, scale;
                Quaternion rot;
                lock (this) {
                    Matrix4x4.Decompose(WorldTransform, out scale, out rot, out pos);
                    WorldTransform = Matrix4x4.CreateScale(scale) * Matrix4x4.CreateFromQuaternion(rot) * Matrix4x4.CreateTranslation(value);
                }
            }
        }

        public Quaternion Rotation {
            get {
                Vector3 pos, scale;
                Quaternion rot;
                Matrix4x4.Decompose(WorldTransform, out scale, out rot, out pos);
                return rot;
            }
            set {
                Vector3 pos, scale;
                Quaternion rot;
                lock (this) {
                    Matrix4x4.Decompose(WorldTransform, out scale, out rot, out pos);
                    WorldTransform = Matrix4x4.CreateScale(scale) * Matrix4x4.CreateFromQuaternion(value) * Matrix4x4.CreateTranslation(pos);
                }
            }
        }

        public Vector3 Scale {
            get {
                Vector3 pos, scale;
                Quaternion rot;
                Matrix4x4.Decompose(WorldTransform, out scale, out rot, out pos);
                return scale;
            }
            set {
                Vector3 pos, scale;
                Quaternion rot;
                lock (this) {
                    Matrix4x4.Decompose(WorldTransform, out scale, out rot, out pos);
                    WorldTransform = Matrix4x4.CreateScale(value) * Matrix4x4.CreateFromQuaternion(rot) * Matrix4x4.CreateTranslation(pos);
                }
            }
        }

        public Vector3 LocalPosition {
            get {
                return LocalTransform.Translation;
            }
            set {
                Vector3 pos, scale;
                Quaternion rot;
                lock (this) {
                    Matrix4x4.Decompose(LocalTransform, out scale, out rot, out pos);
                    LocalTransform = Matrix4x4.CreateScale(scale) * Matrix4x4.CreateFromQuaternion(rot) * Matrix4x4.CreateTranslation(value);
                }
            }
        }

        public Quaternion LocalRotation {
            get {
                Vector3 pos, scale;
                Quaternion rot;
                Matrix4x4.Decompose(LocalTransform, out scale, out rot, out pos);
                return rot;
            }
            set {
                Vector3 pos, scale;
                Quaternion rot;
                lock (this) {
                    Matrix4x4.Decompose(LocalTransform, out scale, out rot, out pos);
                    LocalTransform = Matrix4x4.CreateScale(scale) * Matrix4x4.CreateFromQuaternion(value) * Matrix4x4.CreateTranslation(pos);
                }
            }
        }

        public Vector3 LocalScale {
            get {
                Vector3 pos, scale;
                Quaternion rot;
                Matrix4x4.Decompose(LocalTransform, out scale, out rot, out pos);
                return scale;
            }
            set {
                Vector3 pos, scale;
                Quaternion rot;
                lock (this) {
                    Matrix4x4.Decompose(LocalTransform, out scale, out rot, out pos);
                    LocalTransform = Matrix4x4.CreateScale(value) * Matrix4x4.CreateFromQuaternion(rot) * Matrix4x4.CreateTranslation(pos);
                }
            }
        }

        public Vector3 Forward {
            get {
                return new Vector3(-worldTransform.M13, -worldTransform.M23, -worldTransform.M33);
            }
        }

        public Vector3 Up {
            get {
                return new Vector3(worldTransform.M12, worldTransform.M22, worldTransform.M32);
            }
        }

        public Vector3 Right {
            get {
                return new Vector3(worldTransform.M11, worldTransform.M21, worldTransform.M31);
            }
        }

        public int ChildCount {
            get { return children.Count; }
        }

        public Transform this[int i] {
            get { return children[i]; }
        }

        void AddChild(Transform other) {
            children.Add(other);
        }

        void RemoveChild(Transform other) {
            children.Remove(other);
        }

        protected virtual void RecalcChildrenTransform(Matrix4x4 newWorldTransform) {
            for (int i = 0; i < children.Count; i++) {
                var child = children[i];
                Matrix4x4 local = child.LocalTransform;
                child.WorldTransform = local * newWorldTransform;
            }
        }

        public bool IsDescendantOf(Transform other) {
            Transform p = parent;
            while (p != null) {
                if (p == other) return true;
                p = p.Parent;
            }
            return false;
        }
    }

    public class TransformException : Exception {
        public TransformException(string message) : base(message) { }
    }
}
