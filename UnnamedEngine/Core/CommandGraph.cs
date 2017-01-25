using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using CSGL.Vulkan;

using UnnamedEngine.Core;
using UnnamedEngine.Utilities;

namespace UnnamedEngine.Core {
    public class CommandGraph : IDisposable {
        bool disposed;
        Renderer renderer;

        Pool<Semaphore> pool;
        HashSet<Semaphore> semaphores;
        List<SubmitInfo> infos;
        List<CommandNode> nodeList;
        Dictionary<CommandNode, SubmitInfo> nodeMap;
        ParallelOptions options;
        Fence fence;

        public CommandGraph(Engine engine) {
            if (engine == null) throw new ArgumentNullException(nameof(engine));

            renderer = engine.Renderer;
            semaphores = new HashSet<Semaphore>();
            pool = new Pool<Semaphore>(() => {
                var sem = new Semaphore(engine.Renderer.Device);
                semaphores.Add(sem);
                return sem;
            });
            nodeList = new List<CommandNode>();
            infos = new List<SubmitInfo>();
            options = new ParallelOptions();
            options.MaxDegreeOfParallelism = Environment.ProcessorCount;
            nodeMap = new Dictionary<CommandNode, SubmitInfo>();

            FenceCreateInfo info = new FenceCreateInfo();
            info.Flags = VkFenceCreateFlags.SignaledBit;
            fence = new Fence(renderer.Device, info);
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

            nodeList.Clear();
            infos.Clear();

            while (stack.Count > 0) {
                var node = stack.Pop();
                nodeList.Add(node);
                infos.Add(nodeMap[node]);
                Bake(node);
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
            foreach (var info in infos) {
                Clear(info);
            }

            infos.Clear();
            nodeList.Clear();
            nodeMap.Clear();
        }

        void Clear(SubmitInfo info) {
            foreach (var sem in info.signalSemaphores) {
                if (semaphores.Contains(sem)) pool.Free(sem);
            }

            foreach (var sem in info.waitSemaphores) {
                if (semaphores.Contains(sem)) pool.Free(sem);
            }

            info.signalSemaphores.Clear();
            info.waitDstStageMask.Clear();
            info.waitSemaphores.Clear();
        }

        void Bake(CommandNode node) {
            var info = nodeMap[node];

            foreach (var input in node.Input) {
                var sem = pool.Get();
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
        }
        
        public void Render() {
            fence.Wait();
            fence.Reset();

            for (int i = 0; i < nodeList.Count; i++) {
                nodeList[i].PreRender();
            }

            Parallel.For(0, nodeList.Count, options, Render);
            renderer.GraphicsQueue.Submit(infos, fence);

            for (int i = 0; i < nodeList.Count; i++) {
                nodeList[i].PostRender();
            }
        }

        void Render(int i) {
            infos[i].commandBuffers = null;
            try {
                infos[i].commandBuffers = nodeList[i].GetCommands();
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

            foreach (var sem in semaphores) {
                sem.Dispose();
            }

            fence.Dispose();
            
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
