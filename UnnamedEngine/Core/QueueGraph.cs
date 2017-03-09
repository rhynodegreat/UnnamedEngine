using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using CSGL.Vulkan;

using UnnamedEngine.Core;
using UnnamedEngine.Utilities;

namespace UnnamedEngine.Core {
    public class QueueGraph : IDisposable {
        bool disposed;
        Graphics graphics;
        
        List<NodeInfo> nodeList;
        List<Queue> queues;
        Dictionary<QueueNode, SubmitInfo> nodeMap;
        Dictionary<Queue, List<SubmitInfo>> queueMap;
        ParallelOptions options;
        List<Fence> fences;
        HashSet<Semaphore> localSemaphores;

        struct NodeInfo {
            public QueueNode node;
            public int submitIndex;
        }

        public QueueGraph(Engine engine) {
            if (engine == null) throw new ArgumentNullException(nameof(engine));

            graphics = engine.Graphics;
            nodeList = new List<NodeInfo>();
            options = new ParallelOptions();
            options.MaxDegreeOfParallelism = Environment.ProcessorCount;
            nodeMap = new Dictionary<QueueNode, SubmitInfo>();
            queueMap = new Dictionary<Queue, List<SubmitInfo>>();
            queues = new List<Queue>();
            localSemaphores = new HashSet<Semaphore>();

            fences = new List<Fence>();
        }

        public void Add(QueueNode node) {
            var info = new SubmitInfo();
            info.signalSemaphores = new List<Semaphore>();
            info.waitDstStageMask = new List<VkPipelineStageFlags>();
            info.waitSemaphores = new List<Semaphore>();
            nodeMap.Add(node, info);
        }

        public void Remove(QueueNode node) {
            nodeMap.Remove(node);
        }

        public void Bake() {
            ClearInternal();

            foreach (var fence in fences) {
                fence.Dispose();
            }

            fences.Clear();

            HashSet<QueueNode> nodeSet = new HashSet<QueueNode>(nodeMap.Keys);
            Stack<QueueNode> stack = DirectedAcyclicGraph<QueueNode>.TopologicalSort(nodeSet, (QueueNode node) => { return node.Output; });

            while (stack.Count > 0) {
                var node = stack.Pop();

                if (!queueMap.ContainsKey(node.Queue)) {
                    queueMap.Add(node.Queue, new List<SubmitInfo>());
                    queues.Add(node.Queue);

                    FenceCreateInfo info = new FenceCreateInfo();
                    info.Flags = VkFenceCreateFlags.SignaledBit;

                    fences.Add(new Fence(graphics.Device, info));
                }

                nodeList.Add(new NodeInfo { node = node, submitIndex = Bake(node) });
            }
        }

        public void Clear() {
            ClearInternal();
            nodeMap.Clear();
        }

        void ClearInternal() {
            foreach (var sem in localSemaphores) {
                sem.Dispose();
            }
            localSemaphores.Clear();

            foreach (var infos in queueMap.Values) {
                foreach (var info in infos) {
                    Clear(info);
                }
                infos.Clear();
            }

            nodeList.Clear();
        }

        void Clear(SubmitInfo info) {

            info.signalSemaphores.Clear();
            info.waitDstStageMask.Clear();
            info.waitSemaphores.Clear();
        }

        int Bake(QueueNode node) {
            List<SubmitInfo> queueList = queueMap[node.Queue];

            var info = nodeMap[node];

            foreach (var input in node.Input) {
                var sem = new Semaphore(graphics.Device);
                localSemaphores.Add(sem);
                info.waitSemaphores.Add(sem);
                info.waitDstStageMask.Add(input.SignalStage);
                nodeMap[input].signalSemaphores.Add(sem);
            }

            foreach (var input in node.ExtraInput) {
                info.waitSemaphores.Add(input.semaphore);
                info.waitDstStageMask.Add(input.flags);
            }

            foreach (var output in node.ExtraOutput) {
                info.signalSemaphores.Add(output);
            }

            queueList.Add(info);
            return queueList.Count - 1;
        }

        public void Wait() {
            Fence.Wait(graphics.Device, fences, true, ulong.MaxValue);
            Fence.Reset(graphics.Device, fences);
        }
        
        public void Submit() {
            for (int i = 0; i < nodeList.Count; i++) {
                nodeList[i].node.PreSubmit();
            }

            Parallel.For(0, nodeList.Count, options, GetCommands);

            for (int i = 0; i < queues.Count; i++) {
                queues[i].Submit(queueMap[queues[i]], fences[i]);
            }

            for (int i = 0; i < nodeList.Count; i++) {
                nodeList[i].node.PostSubmit();
            }
        }

        void GetCommands(int i) {
            SubmitInfo info = queueMap[nodeList[i].node.Queue][nodeList[i].submitIndex];
            info.commandBuffers = null;
            try {
                info.commandBuffers = nodeList[i].node.GetCommands();
            }
            catch (Exception e) {
                Console.WriteLine(e);
            }
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposing) {
            if (disposed) return;

            if (disposing) {
                foreach (var node in nodeMap.Keys) {
                    node.Dispose();
                }
            }

            foreach (var fence in fences) {
                fence.Dispose();
            }

            Clear();
            
            disposed = true;
        }

        ~QueueGraph() {
            Dispose(false);
        }
    }

    public class CommandGraphException : Exception {
        public CommandGraphException(string message) : base(message) { }
        public CommandGraphException(string format, params object[] args) : base(string.Format(format, args)) { }
    }
}
