using System;
using System.Numerics;
using System.Collections.Generic;

using UnnamedEngine.Rendering;

namespace UnnamedEngine.UI {
    public abstract class UIElement : IDisposable {
        public Transform Transform { get; private set; }
        public Vector2 Size { get; set; }
        public UIElement Parent { get; private set; }

        public int ChildCount {
            get {
                return children.Count;
            }
        }

        List<UIElement> children;

        protected UIElement() {
            children = new List<UIElement>();
            Transform = new Transform();
        }

        public void AddChild(UIElement element) {
            element.Parent?.RemoveChild(element);
            children.Add(element);
        }

        public void RemoveChild(UIElement element) {
            children.Remove(element);
        }

        public UIElement this[int index] {
            get {
                return children[index];
            }
        }

        public virtual void Dispose() {
            //does nothing
        }
    }
}
