using System;
using System.Collections.Generic;

using CSGL.Vulkan;

namespace Test {
    public interface ISubpass : IRenderer {
        void Bake(RenderPass renderPass, uint subpassIndex);
    }
}
