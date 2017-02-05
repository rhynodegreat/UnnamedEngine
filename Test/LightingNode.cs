using System;
using System.IO;
using System.Collections.Generic;

using CSGL.Vulkan;

using UnnamedEngine.Core;
using UnnamedEngine.Rendering;

namespace Test {
    public class LightingNode : RenderNode, IDisposable {
        bool disposed;

        Engine engine;
        CommandPool pool;
        GBuffer gbuffer;
        RenderPass renderPass;
        uint subpassIndex;
        DeferredNode deferred;
        
        List<CommandBuffer> submitBuffers;
        List<ISubpass> subpasses;

        public LightingNode(Engine engine, GBuffer gbuffer, DeferredNode deferred, CommandPool pool) {
            this.engine = engine;
            this.pool = pool;
            this.gbuffer = gbuffer;
            this.deferred = deferred;

            submitBuffers = new List<CommandBuffer>();
            subpasses = new List<ISubpass>();
        }

        protected override void Bake(RenderPass renderPass, uint subpassIndex) {
            this.renderPass = renderPass;
            this.subpassIndex = subpassIndex;

            Ambient ambient = new Ambient(engine, deferred);
            subpasses.Add(ambient);

            Light light = new Light();
            light.Color = new CSGL.Graphics.Color(0.5f, 0.5f, 0.5f, 1);
            ambient.AddLight(light);

            ambient.Bake(renderPass, subpassIndex);
        }

        public override List<CommandBuffer> GetCommands() {
            submitBuffers.Clear();

            for (int i = 0; i < subpasses.Count; i++) {
                submitBuffers.Add(subpasses[i].GetCommandBuffer());
            }

            return submitBuffers;
        }

        public new void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected override void Dispose(bool disposing) {
            if (disposed) return;

            foreach (var subpass in subpasses) subpass.Dispose();

            base.Dispose(disposing);

            disposed = true;
        }
    }
}
