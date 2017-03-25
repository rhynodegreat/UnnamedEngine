using System;
using System.Collections.Generic;
using System.Threading;

namespace UnnamedEngine.Resources {
    public class AssetCache {

        Dictionary<string, object> assetMap;
        ReaderWriterLockSlim locker;

        public AssetCache() {
            assetMap = new Dictionary<string, object>();
            locker = new ReaderWriterLockSlim();
        }

        public void Add(string name, object asset) {
            if (name == null) throw new ArgumentNullException(nameof(name));

            try {
                locker.EnterWriteLock();
                if (assetMap.ContainsKey(name)) {
                    throw new AssetCacheException($"Cache already contains item called \"{name}\"");
                }

                assetMap[name] = asset;
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

                return assetMap[name];
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
    }

    public class AssetCacheException :Exception {
        public AssetCacheException(string message) : base(message) { }
    }
}
