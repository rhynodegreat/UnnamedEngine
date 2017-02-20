using System;
using System.Collections.Generic;
using CSGL.Vulkan;

using UnnamedEngine.Core;

namespace Test {
    public class Renderer : QueueNode, IDisposable {
        bool disposed;
        Engine engine;
        Window window;

        CommandGraph graph;
        Semaphore acquireImageSemaphore;
        Semaphore renderDoneSemaphore;
        PresentInfo presentInfo;

        uint imageIndex;
        public uint ImageIndex {
            get {
                return imageIndex;
            }
        }

        public Renderer(Engine engine) : base(engine.Graphics.Device, engine.Graphics.GraphicsQueue, VkPipelineStageFlags.BottomOfPipeBit) {
            if (engine == null) throw new ArgumentNullException(nameof(window));
            if (engine.Window == null) throw new ArgumentNullException(nameof(engine.Window));

            this.engine = engine;
            window = engine.Window;

            graph = new CommandGraph();
            acquireImageSemaphore = new Semaphore(engine.Graphics.Device);
            renderDoneSemaphore = new Semaphore(engine.Graphics.Device);

            AddInput(acquireImageSemaphore, VkPipelineStageFlags.FragmentShaderBit);
            AddOutput(renderDoneSemaphore);

            presentInfo = new PresentInfo();
            presentInfo.swapchains = new List<Swapchain> { window.Swapchain };
            presentInfo.waitSemaphores = new List<Semaphore> { renderDoneSemaphore };
            presentInfo.imageIndices = new List<uint> { 0 };

            window.OnSizeChanged += (int x, int y) => {
                presentInfo.swapchains[0] = window.Swapchain;
            };
        }

        public void Bake() {
            graph.Bake();
        }

        public void AddNode(CommandNode node) {
            graph.AddNode(node);
        }

        public void RemoveNode(CommandNode node) {
            graph.RemoveNode(node);
        }

        public override void PreSubmit() {
            var result = window.Swapchain.AcquireNextImage(ulong.MaxValue, acquireImageSemaphore, out imageIndex);
        }

        public override void PostSubmit() {
            presentInfo.imageIndices[0] = ImageIndex;
            engine.Graphics.PresentQueue.Present(presentInfo);
        }

        public override List<CommandBuffer> GetCommands() {
            return graph.GetCommands();
        }

        public new void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected override void Dispose(bool disposing) {
            if (disposed) return;

            base.Dispose(disposing);
            graph.Dispose();
            renderDoneSemaphore.Dispose();
            acquireImageSemaphore.Dispose();

            disposed = true;
        }

        ~Renderer() {
            Dispose(false);
        }
    }
}
