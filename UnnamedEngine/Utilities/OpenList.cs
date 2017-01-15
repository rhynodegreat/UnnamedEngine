using System;
using System.Collections;
using System.Collections.Generic;

namespace UnnamedEngine.Utilities {
    public class OpenList<T> : IList<T> {
        T[] items;
        int count;

        public T[] Items {
            get {
                return items;
            }
        }

        public OpenList() {
            items = new T[0];
        }

        public OpenList(int count) {
            items = new T[count];
        }

        public OpenList(T[] array) {
            if (array == null) {
                items = new T[0];
            } else {
                items = new T[array.Length];
            }

            for (int i = 0; i < items.Length; i++) {
                items[i] = array[i];
            }
        }

        public T this[int index] {
            get {
                return items[index];
            }
            set {
                items[index] = value;
            }
        }

        public int Count {
            get {
                return count;
            }
        }

        public bool IsReadOnly {
            get {
                return false;
            }
        }

        public void Add(T item) {
            if (count >= items.Length) {
                var old = items;
                items = new T[old.Length];

                for (int i = 0; i < count; i++) {
                    items[i] = old[i];
                }
            }

            items[count] = item;
            count++;
        }

        public void Clear() {
            count = 0;
        }

        public void Shrink() {
            if (items.Length > count) {
                var old = items;
                items = new T[count];
                for (int i = 0; i < count; i++) {
                    items[i] = old[i];
                }
            }
        }

        public bool Contains(T item) {
            for (int i = 0; i < items.Length; i++) {
                if (items[i].Equals(item)) {
                    return true;
                }
            }

            return false;
        }

        public void CopyTo(T[] array, int arrayIndex) {
            items.CopyTo(array, arrayIndex);
        }

        public IEnumerator<T> GetEnumerator() {
            throw new NotImplementedException();
        }

        public int IndexOf(T item) {
            int index = -1;
            for (int i = 0; i < items.Length; i++) {
                if (items[i].Equals(item)) {
                    index = i;
                    break;
                }
            }

            return index;
        }

        public void Insert(int index, T item) {
            throw new NotImplementedException();
        }

        public bool Remove(T item) {
            throw new NotImplementedException();
        }

        public void RemoveAt(int index) {
            throw new NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            throw new NotImplementedException();
        }
    }
}
