using System;
using System.Collections.Generic;
using System.Threading;

namespace UnnamedEngine.Resources {
    public class AssetCache {
        class AssetRef {
            public object reference;
            public WeakReference weakReference;

            public AssetRef(object asset) {
                reference = null;
                weakReference = new WeakReference(asset);
            }
        }

        Dictionary<string, AssetRef> assetMap;
        ReaderWriterLockSlim locker;

        public AssetCache() {
            assetMap = new Dictionary<string, AssetRef>();
            locker = new ReaderWriterLockSlim();
        }

        public void Add(string name, object asset, bool strongReference = false) {
            if (name == null) throw new ArgumentNullException(nameof(name));

            try {
                locker.EnterWriteLock();
                if (assetMap.ContainsKey(name) && assetMap[name].weakReference.Target != null) {
                    throw new AssetCacheException($"Cache already contains item called \"{name}\"");
                }

                AssetRef _ref = new AssetRef(asset);
                if (strongReference) {
                    _ref.reference = asset;
                }

                assetMap[name] = _ref;
            }
            finally {
                locker.ExitWriteLock();
            }
        }

        public object Get(string name) {
            if (name == null) throw new ArgumentNullException(nameof(name));

            try {
                locker.EnterReadLock();

                if (!assetMap.ContainsKey(name)) {
                    throw new AssetCacheException($"Cache does not contain item called \"{name}\"");
                }

                object result = assetMap[name].weakReference.Target;
                if (result == null) {
                    throw new AssetCacheException($"Item \"{name}\" has been removed from cache");
                }

                return result;
            }
            finally {
                locker.ExitReadLock();
            }
        }

        public bool Remove(string name) {
            try {
                locker.EnterWriteLock();
                return assetMap.Remove(name);
            }
            finally {
                locker.ExitWriteLock();
            }
        }

        public void RemoveUnused() {
            try {
                locker.EnterWriteLock();

                List<string> toRemove = new List<string>();
                foreach (var key in assetMap.Keys) {
                    if (assetMap[key].weakReference.Target == null) {
                        toRemove.Add(key);
                    }
                }

                foreach (var key in toRemove) {
                    assetMap.Remove(key);
                }
            }
            finally {
                locker.ExitWriteLock();
            }
        }

        public void MakeStrong(string name) {
            if (name == null) throw new ArgumentNullException(nameof(name));

            try {
                locker.EnterReadLock(); //modifying the referenced value, not the dictionary, so a read lock is ok

                if (!assetMap.ContainsKey(name)) {
                    throw new AssetCacheException($"Cache does not contain item called \"{name}\"");
                }

                AssetRef _ref = assetMap[name];
                _ref.reference = _ref.weakReference.Target;
            }
            finally {
                locker.ExitReadLock();
            }
        }

        public void MakeWeak(string name) {
            if (name == null) throw new ArgumentNullException(nameof(name));

            try {
                locker.EnterReadLock(); //modifying the referenced value, not the dictionary, so a read lock is ok

                if (!assetMap.ContainsKey(name)) {
                    throw new AssetCacheException($"Cache does not contain item called \"{name}\"");
                }

                AssetRef _ref = assetMap[name];
                _ref.reference = null;
            }
            finally {
                locker.ExitReadLock();
            }
        }
    }

    public class AssetCacheException :Exception {
        public AssetCacheException(string message) : base(message) { }
    }
}
