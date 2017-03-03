using System;
using System.Collections.Generic;
using System.Reflection;

namespace UnnamedEngine.ECS {
    public class Group : IDisposable {
        bool disposed;

        struct Pair {
            public Action<Entity, object> add;
            public Action<Entity, object> remove;
        }

        EntityManager manager;
        HashSet<Type> typesSet;
        Dictionary<Type, object> componentMap;
        Dictionary<object, Entity> reverseComponentMap;
        HashSet<Entity> entitySet;
        List<Entity> realEntities;
        Dictionary<Type, Pair> delegateMap;

        public IList<Entity> Entities;
        public IList<Type> Types { get; private set; }

        public Group(EntityManager manager, params Type[] types) {
            if (manager == null) throw new ArgumentNullException(nameof(manager));
            if (types == null) throw new ArgumentNullException(nameof(manager));

            this.manager = manager;

            typesSet = new HashSet<Type>();
            componentMap = new Dictionary<Type, object>();
            reverseComponentMap = new Dictionary<object, Entity>();
            entitySet = new HashSet<Entity>();
            realEntities = new List<Entity>();
            delegateMap = new Dictionary<Type, Pair>();

            Entities = realEntities.AsReadOnly();
            Types = new List<Type>(types).AsReadOnly();

            for (int i = 0; i < types.Length; i++) {
                typesSet.Add(types[i]);
                Type listType = typeof(List<>).MakeGenericType(types[i]);
                object list = Activator.CreateInstance(listType);
                componentMap.Add(types[i], list);
                CreateDelegates(types[i]);
            }

            manager.OnComponentAdded += OnComponentAdded;
            manager.OnComponentRemoved += OnComponentRemoved;
        }

        void OnComponentAdded(Entity entity, object component) {
            if (typesSet.Contains(component.GetType())) {
                Type t = component.GetType();
                delegateMap[t].add(entity, component);

                entitySet.Add(entity);
                realEntities.Add(entity);
            }
        }

        void OnComponentRemoved(Entity entity, object component) {
            if (typesSet.Contains(component.GetType()) && entitySet.Contains(entity)) {
                Type t = component.GetType();
                delegateMap[t].remove(entity, component);
                
                //make sure entities with multiple of the same type of component are not removed
                for (int i = 0; i < entity.Components.Count; i++) {
                    if (entity.Components[i].GetType() == t) return;
                }

                entitySet.Remove(entity);
                realEntities.Remove(entity);
            }
        }

        void CreateDelegates(Type t) {
            MethodInfo add = typeof(Group).GetMethod(nameof(AddComponent), BindingFlags.NonPublic);
            add.MakeGenericMethod(t);
            Action<Entity, object> addDelegate = (Action<Entity, object>)Delegate.CreateDelegate(typeof(Action<Entity, object>), add);

            MethodInfo remove = typeof(Group).GetMethod(nameof(RemoveComponent), BindingFlags.NonPublic);
            remove.MakeGenericMethod(t);
            Action<Entity, object> removeDelegate = (Action<Entity, object>)Delegate.CreateDelegate(typeof(Action<Entity, object>), remove);

            delegateMap.Add(t, new Pair { add = addDelegate, remove = removeDelegate });
        }

        void AddComponent<T>(Entity entity, object component) {
            List<T> list = (List<T>)componentMap[typeof(T)];
            list.Add((T)component);
            reverseComponentMap.Add(component, entity);
        }

        void RemoveComponent<T>(Entity entity, object component) {
            List<T> list = (List<T>)componentMap[typeof(T)];
            list.Remove((T)component);
            reverseComponentMap.Remove(componentMap);
        }

        public List<T> GetComponents<T>() {
            if (!typesSet.Contains(typeof(T))) throw new GroupException("Type is not part of this group");
            return (List<T>)componentMap[typeof(T)];
        }

        public void Dispose() {
            Dispose(true);
        }

        void Dispose(bool disposing) {
            if (disposed) return;

            manager.OnComponentAdded -= OnComponentAdded;
            manager.OnComponentRemoved -= OnComponentRemoved;

            disposed = true;
        }

        ~Group() {
            Dispose(false);
        }
    }

    public class GroupException : Exception {
        public GroupException(string message) : base(message) { }
    }
}
