using System;
using System.Collections.Generic;

namespace UnnamedEngine.ECS {
    public class Entity {
        public EntityManager Manager { get; internal set; }
        public IList<object> Components { get; private set; }

        List<object> components;

        public Entity() {
            components = new List<object>();
            Components = components.AsReadOnly();
        }

        public void AddComponent<T>(T component) where T : class {
            if (component == null) throw new ArgumentNullException(nameof(component));

            components.Add(component);
            if (Manager != null) Manager.AddComponent(this, component);
        }

        public bool RemoveComponent<T>(T component) where T : class {
            if (components.Remove(component)) {
                if (Manager != null) Manager.RemoveComponent(this, component);
                return true;
            }
            return false;
        }

        public T GetFirst<T>() where T : class {
            for (int i = 0; i < components.Count; i++) {
                if (components[i] is T) return (T)components[i];
            }

            return null;
        }

        public List<T> GetAll<T>() where T : class {
            List<T> result = new List<T>();
            for (int i = 0; i < components.Count; i++) {
                if (components[i] is T) result.Add((T)components[i]);
            }

            return result;
        }

        public void SetIndex<T>(T component, int index) where T : class {
            if (component == null) throw new ArgumentNullException(nameof(component));
            if (components.Remove(component)) {
                components.Insert(index, component);
            }
        }
    }
}
