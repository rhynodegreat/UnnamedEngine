using System;
using System.Collections.Generic;

namespace UnnamedEngine.ECS {
    public class EntityManager {
        HashSet<Entity> entities;
        Dictionary<Type, List<object>> componentMap;
        HashSet<object> componentSet;

        public event Action<Entity> OnEntityAdded = delegate { };
        public event Action<Entity> OnEntityRemoved = delegate { };

        public event Action<Entity, object> OnComponentAdded = delegate { };
        public event Action<Entity, object> OnComponentRemoved = delegate { };

        public EntityManager() {
            entities = new HashSet<Entity>();
            componentMap = new Dictionary<Type, List<object>>();
            componentSet = new HashSet<object>();
        }

        public void AddEntity(Entity entity) {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            if (entity.Manager != null && entity.Manager != this) throw new EntityManagerException("Entity has already been added to a manager");

            entities.Add(entity);
            OnEntityAdded(entity);

            for (int i = 0; i < entity.Components.Count; i++) {
                AddComponent(entity, entity.Components[i]);
            }
        }

        public bool RemoveEntity(Entity entity) {
            if (entities.Remove(entity)) {
                entity.Manager = null;

                for (int i = 0; i < entity.Components.Count; i++) {
                    RemoveComponent(entity, entity.Components[i]);
                }

                OnEntityRemoved(entity);

                return true;
            }
            return false;
        }

        public bool ContainsEntity(Entity entity) {
            return entities.Contains(entity);
        }

        internal void AddComponent(Entity entity, object component) {
            if (componentSet.Contains(component)) throw new EntityManagerException("Component has already been added to an entity");

            componentSet.Add(component);

            List<object> list = GetList(component);
            list.Add(component);

            OnComponentAdded(entity, component);
        }

        internal void RemoveComponent(Entity entity, object component) {
            componentSet.Remove(component);

            List<object> list = GetList(component);
            list.Remove(component);

            OnComponentRemoved(entity, component);
        }

        List<object> GetList(object component) {
            Type t = component.GetType();
            if (componentMap.ContainsKey(t)) {
                return componentMap[t];
            } else {
                List<object> list = new List<object>();
                componentMap.Add(t, list);
                return list;
            }
        }
    }

    public class EntityManagerException : Exception {
        public EntityManagerException(string message) { }
    }
}
