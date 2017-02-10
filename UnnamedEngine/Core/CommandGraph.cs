using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using CSGL.Vulkan;

namespace UnnamedEngine.Core {
    public class CommandGraph : IDisposable {
        bool disposed;

        HashSet<CommandNode> nodes;
        List<CommandNode> nodeList;
        List<CommandBuffer> commands;

        public CommandGraph() {
            nodes = new HashSet<CommandNode>();
            nodeList = new List<CommandNode>();
            commands = new List<CommandBuffer>();
        }

        public void AddNode(CommandNode node) {
            nodes.Add(node);
        }

        public void RemoveNode(CommandNode node) {
            nodes.Remove(node);
        }

        struct SortState {
            public bool open;
            public bool finished;
        }

        public void Bake() {
            //https://en.wikipedia.org/wiki/Topological_sorting#Depth-first_search
            Stack<CommandNode> stack = new Stack<CommandNode>();
            Dictionary<CommandNode, SortState> sortState = new Dictionary<CommandNode, SortState>();

            foreach (var node in nodes) {
                sortState.Add(node, new SortState());
            }

            foreach (var node in nodes) {
                Visit(stack, sortState, node);
            }

            nodeList.Clear();
            commands.Clear();

            while (stack.Count > 0) {
                var node = stack.Pop();

                nodeList.Add(node);
                node.Bake();
                commands.Add(null);
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

        public List<CommandBuffer> GetCommands() {
            Parallel.For(0, nodeList.Count, GetCommands);
            return commands;
        }

        void GetCommands(int i) {
            commands[i] = null;
            try {
                commands[i] = nodeList[i].GetCommands();
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
                foreach (var node in nodes) {
                    node.Dispose();
                }
            }

            disposed = true;
        }

        ~CommandGraph() {
            Dispose(false);
        }
    }
}
