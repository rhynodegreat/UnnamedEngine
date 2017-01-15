using System;
using System.Collections.Generic;

using CSGL.Vulkan;

using UnnamedEngine.Core;
using UnnamedEngine.Utilities;

namespace UnnamedEngine.Rendering {
    public class RenderGraph : IDisposable {
        bool disposed;
        Renderer renderer;
        OpenList<SubmitInfo> submitInfos;

        public AcquireImageNode Start { get; private set; }
        public PresentNode End { get; private set; }

        public RenderGraph(Engine engine) {
            renderer = engine.Renderer;
            submitInfos = new OpenList<SubmitInfo>(2);

            Start = new AcquireImageNode(engine);
            End = new PresentNode(engine);
        }

        public void Render() {
            submitInfos.Clear();
            uint index = Start.AcquireImage();
            submitInfos.Add(Start.GetSubmitInfo());
            submitInfos.Add(End.GetSubmitInfo(index));

            submitInfos.Shrink();
            renderer.GraphicsQueue.Submit(submitInfos.Items);

            End.Present(index);
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposing) {
            if (disposed) return;

            if (disposing) {
                Start.Dispose();
                End.Dispose();
            }

            disposed = true;
        }

        ~RenderGraph() {
            Dispose(false);
        }
    }

    public class RenderGraphException : Exception {
        public RenderGraphException(string message) : base(message) { }
        public RenderGraphException(string format, params object[] args) : base(string.Format(format, args)) { }
    }
}
