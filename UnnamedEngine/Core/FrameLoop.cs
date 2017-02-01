using System;
using System.Collections.Generic;

namespace UnnamedEngine.Core {
    public class FrameLoop {
        List<ISystem> systems;

        internal FrameLoop() {
            systems = new List<ISystem>();
        }

        public void Add(ISystem system) {
            if (systems.Contains(system)) return;
            systems.Add(system);

            Sort();
        }

        public void Remove(ISystem system) {
            systems.Remove(system);
        }

        public void Sort() {
            systems.Sort((ISystem a, ISystem b) => {
                return a.Priority.CompareTo(b.Priority);
            });
        }

        internal void Update(float delta) {
            for (int i = 0; i < systems.Count; i++) {
                systems[i].Update(delta);
            }
        }
    }
}
