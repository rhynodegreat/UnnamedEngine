using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using CSGL.Vulkan;

using UnnamedEngine.Core;
using UnnamedEngine.Utilities;

namespace UnnamedEngine.Core {
    public class CommandGraph : IDisposable {
        bool disposed;
        Graphics graphics;
        
        List<NodeInfo> nodeList;
        List<Queue> queues;
        Dictionary<CommandNode, SubmitInfo> nodeMap;
        Dictionary<Queue, List<SubmitInfo>> queueMap;
        ParallelOptions options;
        List<Fence> fences;
        HashSet<Semaphore> localSemaphores;

        struct NodeInfo {
            public CommandNode node;
            public int submitIndex;
        }

        public CommandGraph(Engine engine) {
            if (engine == null) throw new ArgumentNullException(nameof(engine));

            graphics = engine.Graphics;
            nodeList = new List<NodeInfo>();
            options = new ParallelOptions();
            options.MaxDegreeOfParallelism = Environment.ProcessorCount;
            nodeMap = new Dictionary<CommandNode, SubmitInfo>();
            queueMap = new Dictionary<Queue, List<SubmitInfo>>();
            queues = new List<Queue>();
            localSemaphores = new HashSet<Semaphore>();

            fences = new List<Fence>();
        }

        public void Add(CommandNode node) {
            var info = new SubmitInfo();
            info.signalSemaphores = new List<Semaphore>();
            info.waitDstStageMask = new List<VkPipelineStageFlags>();
            info.waitSemaphores = new List<Semaphore>();
            nodeMap.Add(node, info);
        }

        public void Remove(CommandNode node) {
            nodeMap.Remove(node);
        }

        struct SortState {
            public bool open;
            public bool finished;
        }

        public void Bake() {
            //https://en.wikipedia.org/wiki/Topological_sorting#Depth-first_search
            Stack<CommandNode> stack = new Stack<CommandNode>();
            Dictionary<CommandNode, SortState> sortState = new Dictionary<CommandNode, SortState>();

            foreach (var node in nodeMap.Keys) {
                sortState.Add(node, new SortState());
            }

            foreach (var node in nodeMap.Keys) {
                Visit(stack, sortState, node);
            }

            foreach (var infos in queueMap.Values) {
                foreach (var info in infos) {
                    Clear(info);
                }
                infos.Clear();
            }
            nodeList.Clear();

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

        void Visit(Stack<CommandNode> stack, Dictionary<CommandNode, SortState> sortState, CommandNode node) {
            if (sortState[node].open) throw new CommandGraphException("Nodes do not form Direct Acyclic Graph");
            if (!sortState[node].finished) {
                var state = sortState[node];
                state.open = true;
                sortState[node] = state;

                foreach (var output in node.Output) {
                    Visit(stack, sortState, output);
                }

                state.open = false;
                state.finished = true;
                sortState[node] = state;

                stack.Push(node);
            }
        }

        public void Clear() {
            foreach (var infos in queueMap.Values) {
                foreach (var info in infos) {
                    Clear(info);
                }
                infos.Clear();
            }
            nodeList.Clear();

            nodeMap.Clear();
        }

        void Clear(SubmitInfo info) {
            foreach (var sem in localSemaphores) {
                sem.Dispose();
            }
            localSemaphores.Clear();

            info.signalSemaphores.Clear();
            info.waitDstStageMask.Clear();
            info.waitSemaphores.Clear();
        }

        int Bake(CommandNode node) {
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
        
        public void Submit() {
            Fence.Wait(graphics.Device, fences, true, ulong.MaxValue);
            Fence.Reset(graphics.Device, fences);

            for (int i = 0; i < nodeList.Count; i++) {
                nodeList[i].node.PreRender();
            }

            Parallel.For(0, nodeList.Count, options, Render);
            for (int i = 0; i < queues.Count; i++) {
                queues[i].Submit(queueMap[queues[i]], fences[i]);
            }

            for (int i = 0; i < nodeList.Count; i++) {
                nodeList[i].node.PostRender();
            }
        }

        void Render(int i) {
            SubmitInfo info = queueMap[nodeList[i].node.Queue][nodeList[i].submitIndex];
            info.commandBuffers = null;
            try {
                info.commandBuffers = nodeList[i].node.GetCommands();
            }
            catch (Exception e) {
                Console.WriteLine(e);
            }
        }

        class Node : IDisposable {
            public List<Semaphore> signalSemaphores;
            public SubmitInfo info;

            public Node(Device device, CommandNode node) {
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

        ~CommandGraph() {
            Dispose(false);
        }
    }

    public class CommandGraphException : Exception {
        public CommandGraphException(string message) : base(message) { }
        public CommandGraphException(string format, params object[] args) : base(string.Format(format, args)) { }
    }
}
