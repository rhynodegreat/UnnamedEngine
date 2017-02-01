using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace UnnamedEngine.Core {
    public class Clock {
        Stopwatch watch;
        float last;

        public float ActualTime {
            get {
                return (float)watch.Elapsed.TotalSeconds;
            }
        }

        public float FrameDelta { get; private set; }

        internal Clock() {
            watch = new Stopwatch();
            watch.Start();
        }

        internal void FrameUpdate() {
            float now = ActualTime;
            FrameDelta = now - last;
            last = now;
        }
    }
}
