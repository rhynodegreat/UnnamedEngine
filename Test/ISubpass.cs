using System;
using System.Collections.Generic;

using CSGL.Vulkan1;

namespace Test {
    public interface ISubpass : IRenderer {
        void Bake(RenderPass renderPass, uint subpassIndex);
    }
}
