﻿using System;
using System.Collections.Generic;

using CSGL.Vulkan;

namespace Test {
    public interface IRenderer {
        CommandBuffer GetCommandBuffer();
    }
}
