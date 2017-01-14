using System;
using System.Collections.Generic;

namespace UnnamedEngine.Rendering {
    public class RenderGraph {
        List<RenderNode> start;

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
