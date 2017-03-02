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

        public void AddComponent(object component) {
            if (component == null) throw new ArgumentNullException(nameof(component));

            components.Add(component);
            if (Manager != null) Manager.AddComponent(this, component);
        }

        public bool RemoveComponent(object component) {
            if (components.Remove(component)) {
                if (Manager != null) Manager.RemoveComponent(this, component);
                return true;
            }
            return false;
        }
    }
}
