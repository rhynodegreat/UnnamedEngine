using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using CSGL.Vulkan;

using UnnamedEngine.Utilities;

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

        public void Bake() {
            nodeList.Clear();
            commands.Clear();

            Stack<CommandNode> stack = DirectedAcyclicGraph<CommandNode>.TopologicalSort(nodes, (CommandNode node) => { return node.Output; });

            while (stack.Count > 0) {
                var node = stack.Pop();

                nodeList.Add(node);
                node.Bake();
                commands.Add(null);
            }
        }

        public List<CommandBuffer> GetCommands() {
            for (int i = 0; i < nodeList.Count; i++) {
                nodeList[i].ResetEvent();
            }

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
