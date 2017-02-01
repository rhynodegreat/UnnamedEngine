using System;
using System.Collections.Generic;

namespace UnnamedEngine.Core {
    public interface ISystem {
        void Update(float delta);
        int Priority { get; set; }
    }
}
