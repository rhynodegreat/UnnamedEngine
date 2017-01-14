using System;
using System.Collections.Generic;

using UnnamedEngine.Core;

namespace UnnamedEngine.Rendering {
    public class RenderGraph {
        List<RenderNode> start;

        Renderer renderer;

        public RenderGraph(Renderer renderer) {
            if (renderer == null) throw new ArgumentNullException(nameof(renderer));
            this.renderer = renderer;
            start = new List<RenderNode>();
        }

        public void AddStart(RenderNode node) {
            if (node == null) throw new ArgumentNullException(nameof(node));
            if (start.Contains(node)) return;
            start.Add(node);
        }

        public void RemoveStart(RenderNode node) {
            if (node == null) throw new ArgumentNullException(nameof(node));
            if (!start.Contains(node)) return;
            start.Remove(node);
        }
    }

    public class RenderGraphException : Exception {
        public RenderGraphException(string message) : base(message) { }
        public RenderGraphException(string format, params object[] args) : base(string.Format(format, args)) { }
    }
}
