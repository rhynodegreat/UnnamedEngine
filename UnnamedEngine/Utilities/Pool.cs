using System;
using System.Collections.Generic;

namespace UnnamedEngine.Utilities {
    public class Pool<T> {
        Stack<T> stack;
        Func<T> create;
        object locker;
        int size;

        public int Count {
            get {
                return stack.Count;
            }
        }

        public Pool(Func<T> creationCallback) {
            if (creationCallback == null) throw new ArgumentNullException(nameof(creationCallback));
            create = creationCallback;

            stack = new Stack<T>();
            locker = new object();
            size = 4;

            for (int i = 0; i < size; i++) {
                stack.Push(create());
            }
        }

        public T Get() {
            if (stack.Count == 0) {
                for (int i = 0; i < size; i++) {
                    stack.Push(create());
                }
                size *= 2;
            }
            return stack.Pop();
        }

        public void Free(T item) {
            stack.Push(item);
        }
    }
}
