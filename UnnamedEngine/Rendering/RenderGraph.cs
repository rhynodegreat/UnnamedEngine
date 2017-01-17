using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using CSGL.Vulkan;

using UnnamedEngine.Core;

namespace UnnamedEngine.Rendering {
    public class RenderGraph : IDisposable {
        bool disposed;
        Renderer renderer;
        HashSet<RenderNode> nodes;
        List<SubmitInfo> infos;
        List<RenderNode> nodeList;

        int coreCount;

        public RenderGraph(Engine engine) {
            if (engine == null) throw new ArgumentNullException(nameof(engine));
            if (engine.Renderer == null) throw new ArgumentNullException(nameof(engine.Renderer));

            renderer = engine.Renderer;
            nodes = new HashSet<RenderNode>();
            nodeList = new List<RenderNode>();
            infos = new List<SubmitInfo>();
            coreCount = Environment.ProcessorCount;
        }

        public void Add(RenderNode node) {
            nodes.Add(node);
        }

        public void Bake() {
            infos.Clear();
            Queue<RenderNode> eventQueue = new Queue<RenderNode>();
            
            foreach (var node in nodes) {
                if (node.Input.Count == 0) {    //find the nodes that have no input and queue them for later
                    eventQueue.Enqueue(node);
                }
            }

            nodeList.Clear();

            int i = 0;
            while (eventQueue.Count > 0) {
                RenderNode node = eventQueue.Dequeue();
                nodeList.Add(node);
                infos.Add(node.SubmitInfo);
                for (int j = 0; j < node.Output.Count; j++) {
                    eventQueue.Enqueue(node.Output[j]);
                }
                i++;
            }
        }

        public void Render() {
            for (int i = 0; i < nodeList.Count; i++) {
                nodeList[i].PreRender();
            }

            Parallel.For(0, nodeList.Count, Render);
            //for (int i = 0; i < nodeList.Count; i++) {
            //    Render(i);
            //}
            renderer.GraphicsQueue.Submit(infos);

            for (int i = 0; i < nodeList.Count; i++) {
                nodeList[i].PostRender();
            }
        }

        void Render(int i) {
            List<CommandBuffer> commands = nodeList[i].GetCommands();
            infos[i].commandBuffers = commands;
        }

        class Node : IDisposable {
            public List<Semaphore> signalSemaphores;
            public SubmitInfo info;

            public Node(Device device, RenderNode node) {
                signalSemaphores = new List<Semaphore>(node.Output.Count);
                info = new SubmitInfo();

                for (int i = 0; i < signalSemaphores.Count; i++) {
                    signalSemaphores.Add(new Semaphore(device));
                }
            }

            public void Dispose() {
                for (int i = 0; i < signalSemaphores.Count; i++) {
                    signalSemaphores[i].Dispose();
                }
            }
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposing) {
            if (disposed) return;

            if (disposing) {
                foreach (var node in nodes) {
                    node.Dispose();
                }
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
