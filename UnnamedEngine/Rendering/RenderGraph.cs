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

        public void Bake() {
            infos = new SubmitInfo[nodeMap.Count];
            Queue<RenderNode> eventQueue = new Queue<RenderNode>();
            
            foreach (var pair in nodeMap) {
                if (pair.Key.Input.Count == 0) {    //find the nodes that have no input and queue them for later
                    eventQueue.Enqueue(pair.Key);
                }

                var info = pair.Value;

                Semaphore[] waitSemaphores = new Semaphore[pair.Key.Input.Count];
                VkPipelineStageFlags[] waitFlags = new VkPipelineStageFlags[pair.Key.Input.Count];
                for (int j = 0; j < waitSemaphores.Length; j++) {
                    waitSemaphores[j] = pair.Key.Input[j].SignalSemaphore;
                    waitFlags[j] = pair.Key.Input[j].SignalStage;
                }

                info.waitSemaphores = waitSemaphores;
                info.waitDstStageMask = waitFlags;
                info.signalSemaphores = new Semaphore[] { pair.Key.SignalSemaphore };
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
            infos[i].commandBufferCount = count;
            if (count == -1 && commands == null) {
                infos[i].signalCount = 0;
            }
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
