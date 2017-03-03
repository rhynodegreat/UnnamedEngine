using System;
using System.Collections.Generic;

namespace UnnamedEngine.Utilities {
    public static class DirectedAcyclicGraph<T> {
        struct SortState {
            public bool open;
            public bool finished;
        }

        public static Stack<T> TopologicalSort(HashSet<T> set, Func<T, IList<T>> getOutput) {
            //https://en.wikipedia.org/wiki/Topological_sorting#Depth-first_search
            Dictionary<T, SortState> sortState = new Dictionary<T, SortState>();
            Stack<T> stack = new Stack<T>();

            foreach (var node in set) {
                sortState.Add(node, new SortState());
            }

            foreach (var node in set) {
                Visit(stack, sortState, node, getOutput);
            }

            return stack;
        }

        static void Visit(Stack<T> stack, Dictionary<T, SortState> sortState, T node, Func<T, IList<T>> getOutput) {
            if (sortState[node].open) throw new GraphException("Nodes do not form a Directed Acyclic Graph");
            if (!sortState[node].finished) {
                var state = sortState[node];
                state.open = true;
                sortState[node] = state;

                foreach (var output in getOutput(node)) {
                    Visit(stack, sortState, output, getOutput);
                }

                state.open = false;
                state.finished = true;
                sortState[node] = state;

                stack.Push(node);
            }
        }
    }

    public class GraphException : Exception {
        public GraphException(string message) : base(message) { }
    }
}
