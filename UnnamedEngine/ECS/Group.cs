using System;
using System.Collections.Generic;

namespace UnnamedEngine.ECS {
    public class Group {
        HashSet<Type> typesSet;
        Dictionary<Type, IList<object>> componentMap;
        Dictionary<Type, List<object>> realComponentMap;
        Dictionary<object, Entity> reverseComponentMap;
        HashSet<Entity> entitySet;
        List<Entity> realEntities;

        public IList<Entity> Entities;
        public IList<Type> Types { get; private set; }

        public Group(EntityManager manager, params Type[] types) {
            if (manager == null) throw new ArgumentNullException(nameof(manager));
            if (types == null) throw new ArgumentNullException(nameof(manager));

            typesSet = new HashSet<Type>();
            realComponentMap = new Dictionary<Type, List<object>>();
            componentMap = new Dictionary<Type, IList<object>>();
            reverseComponentMap = new Dictionary<object, Entity>();
            entitySet = new HashSet<Entity>();
            realEntities = new List<Entity>();

            Entities = realEntities.AsReadOnly();
            Types = new List<Type>(types).AsReadOnly();

            for (int i = 0; i < types.Length; i++) {
                typesSet.Add(types[i]);
                List<object> list = new List<object>();
                realComponentMap.Add(types[i], list);
                componentMap.Add(types[i], list.AsReadOnly());
            }

            manager.OnComponentAdded += OnComponentAdded;
            manager.OnComponentRemoved += OnComponentRemoved;
        }

        void OnComponentAdded(Entity entity, object component) {
            if (typesSet.Contains(component.GetType())) {
                Type t = component.GetType();
                List<object> list = realComponentMap[t];
                list.Add(component);
                reverseComponentMap.Add(component, entity);

                entitySet.Add(entity);
                realEntities.Add(entity);
            }
        }

        void OnComponentRemoved(Entity entity, object component) {
            if (typesSet.Contains(component.GetType()) && entitySet.Contains(entity)) {
                Type t = component.GetType();
                List<object> list = realComponentMap[t];
                list.Remove(component);
                reverseComponentMap.Remove(component);
                
                //make sure entities with multiple of the same type of component are not removed
                for (int i = 0; i < entity.Components.Count; i++) {
                    if (entity.Components[i].GetType() == t) return;
                }

                entitySet.Remove(entity);
                realEntities.Remove(entity);
            }
        }

        public IList<object> GetComponents(Type type) {
            return componentMap[type];
        }

        public IList<object> GetComponent<T>() {
            return componentMap[typeof(T)];
        }
    }
}
