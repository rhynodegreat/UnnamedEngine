using System;
using System.Collections.Generic;

namespace UnnamedEngine.ECS {
    public class Group : IDisposable {
        interface IHolder {
            object Get();
            object GetReadOnly();
            void Add(object o);
            void Remove(object o);
        }

        class Holder<T> : IHolder {
            List<T> list;
            IList<T> readOnly;

            public Holder() {
                list = new List<T>();
                readOnly = list.AsReadOnly();
            }

            public object Get() {
                return list;
            }

            public object GetReadOnly() {
                return readOnly;
            }

            public void Add(object o) {
                list.Add((T)o);
            }

            public void Remove(object o) {
                list.Remove((T)o);
            }
        }

        bool disposed;

        EntityManager manager;
        HashSet<Type> typesSet;
        Dictionary<Type, IHolder> componentMap;
        Dictionary<object, Entity> reverseComponentMap;
        HashSet<Entity> entitySet;
        List<Entity> realEntities;

        public IList<Entity> Entities;
        public IList<Type> Types { get; private set; }

        public Group(EntityManager manager, params Type[] types) {
            if (manager == null) throw new ArgumentNullException(nameof(manager));
            if (types == null) throw new ArgumentNullException(nameof(manager));

            this.manager = manager;

            typesSet = new HashSet<Type>();
            componentMap = new Dictionary<Type, IHolder>();
            reverseComponentMap = new Dictionary<object, Entity>();
            entitySet = new HashSet<Entity>();
            realEntities = new List<Entity>();

            Entities = realEntities.AsReadOnly();
            Types = new List<Type>(types).AsReadOnly();

            for (int i = 0; i < types.Length; i++) {
                typesSet.Add(types[i]);
                List<object> list = new List<object>();

                Type holder = typeof(Holder<>).MakeGenericType(types[i]);
                componentMap.Add(types[i], (IHolder)Activator.CreateInstance(holder));
            }

            manager.OnComponentAdded += OnComponentAdded;
            manager.OnComponentRemoved += OnComponentRemoved;
        }

        void OnComponentAdded(Entity entity, object component) {
            if (typesSet.Contains(component.GetType())) {
                Type t = component.GetType();
                componentMap[t].Add(component);
                reverseComponentMap.Add(component, entity);

                entitySet.Add(entity);
                realEntities.Add(entity);
            }
        }

        void OnComponentRemoved(Entity entity, object component) {
            if (typesSet.Contains(component.GetType()) && entitySet.Contains(entity)) {
                Type t = component.GetType();
                componentMap[t].Remove(component);
                reverseComponentMap.Remove(component);
                
                //make sure entities with multiple of the same type of component are not removed
                for (int i = 0; i < entity.Components.Count; i++) {
                    if (entity.Components[i].GetType() == t) return;
                }

                entitySet.Remove(entity);
                realEntities.Remove(entity);
            }
        }

        public IList<T> GetComponent<T>() {
            return (IList<T>)componentMap[typeof(T)].GetReadOnly();
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
}
