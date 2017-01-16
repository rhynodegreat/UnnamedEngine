using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using CSGL.Vulkan;

using UnnamedEngine.Core;
using UnnamedEngine.Utilities;

namespace UnnamedEngine.Rendering {
    public class RenderGraph : IDisposable {
        bool disposed;
        Renderer renderer;
        Dictionary<RenderNode, SubmitInfo> nodeMap;
        SubmitInfo[] infos;
        List<RenderNode> nodeList;

        int coreCount;

        public RenderGraph(Engine engine) {
            renderer = engine.Renderer;
            nodeMap = new Dictionary<RenderNode, SubmitInfo>();
            nodeList = new List<RenderNode>();
            coreCount = Environment.ProcessorCount;
        }

        public void Add(RenderNode node) {
            if (nodeMap.ContainsKey(node)) return;

            SubmitInfo info = new SubmitInfo();
            nodeMap.Add(node, info);
        }

        public SubmitInfo GetSubmitInfo(RenderNode node) {
            return nodeMap[node];
        }

        public void ResetSubmitInfo(RenderNode node) {
            SubmitInfo info = nodeMap[node];
            Semaphore[] waitSemaphores = new Semaphore[node.Input.Count];
            VkPipelineStageFlags[] waitFlags = new VkPipelineStageFlags[node.Input.Count];
            for (int j = 0; j < waitSemaphores.Length; j++) {
                waitSemaphores[j] = node.Input[j].SignalSemaphore;
                waitFlags[j] = node.Input[j].SignalStage;
            }

            info.waitSemaphores = waitSemaphores;
            info.waitDstStageMask = waitFlags;
            info.signalSemaphores = new Semaphore[] { node.SignalSemaphore };
        }

        public void Bake() {
            infos = new SubmitInfo[nodeMap.Count];
            Queue<RenderNode> eventQueue = new Queue<RenderNode>();
            
            foreach (var node in nodeMap.Keys) {
                if (node.Input.Count == 0) {    //find the nodes that have no input and queue them for later
                    eventQueue.Enqueue(node);
                }

                ResetSubmitInfo(node);
                node.OnBake(this);
            }

            nodeList.Clear();

            int i = 0;
            while (eventQueue.Count > 0) {
                RenderNode node = eventQueue.Dequeue();
                nodeList.Add(node);
                infos[i] = nodeMap[node];
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

            //Parallel.For(0, nodeList.Count, Render);
            for (int i = 0; i < nodeList.Count; i++) {
                Render(i);
            }
            renderer.GraphicsQueue.Submit(infos);

            for (int i = 0; i < nodeList.Count; i++) {
                nodeList[i].PostRender();
            }
        }

        void Render(int i) {
            int count;
            CommandBuffer[] commands = nodeList[i].GetCommands(out count);
            infos[i].commandBuffers = commands;
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposing) {
            if (disposed) return;

            if (disposing) {
                foreach (var node in nodeList) {
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
