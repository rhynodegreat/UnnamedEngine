using System;
using System.Collections.Generic;

using CSGL.Vulkan;

using UnnamedEngine.Core;

namespace UnnamedEngine.Rendering {
    public class PresentNode : RenderNode, IDisposable {
        bool disposed;
        Renderer renderer;

        List<RenderNode> input;
        List<CommandBuffer> commandBuffers;

        SubmitInfo submitInfo;
        List<Semaphore> waitSemaphores;

        PresentInfo presentInfo;
        Semaphore renderDoneSemaphore;
        Semaphore[] renderDoneSemaphores;

        internal PresentNode(Engine engine) {
            renderer = engine.Renderer;

            input = new List<RenderNode>();
            commandBuffers = new List<CommandBuffer>();
            waitSemaphores = new List<Semaphore>();
            renderDoneSemaphore = new Semaphore(engine.Renderer.Device);
            renderDoneSemaphores = new Semaphore[] { renderDoneSemaphore };

            submitInfo = new SubmitInfo();
            submitInfo.commandBuffers = new CommandBuffer[1];
            submitInfo.signalSemaphores = renderDoneSemaphores;

            presentInfo = new PresentInfo();
            presentInfo.imageIndices = new uint[1];
            presentInfo.swapchains = new Swapchain[] { engine.Window.Swapchain };
            presentInfo.waitSemaphores = renderDoneSemaphores;

            CreateCommandBuffers(renderer, engine.Window.SwapchainImages);
        }

        void CreateCommandBuffers(Renderer renderer, IList<Image> images) {
            commandBuffers = new List<CommandBuffer>(renderer.InternalCommandPool.Allocate(VkCommandBufferLevel.Primary, images.Count));

            CommandBufferBeginInfo beginInfo = new CommandBufferBeginInfo();
            beginInfo.flags = VkCommandBufferUsageFlags.SimultaneousUseBit;
            
            VkImageSubresourceRange subresourceRange = new VkImageSubresourceRange();
            subresourceRange.aspectMask = VkImageAspectFlags.ColorBit;
            subresourceRange.baseMipLevel = 0;
            subresourceRange.levelCount = 1;
            subresourceRange.baseArrayLayer = 0;
            subresourceRange.layerCount = 1;

            ImageMemoryBarrier colorToPresent = new ImageMemoryBarrier();
            colorToPresent.srcAccessMask = VkAccessFlags.MemoryWriteBit | VkAccessFlags.MemoryReadBit;
            colorToPresent.dstAccessMask = VkAccessFlags.MemoryReadBit;
            colorToPresent.oldLayout = VkImageLayout.ColorAttachmentOptimal;
            colorToPresent.newLayout = VkImageLayout.PresentSrcKhr;
            colorToPresent.srcQueueFamilyIndex = renderer.GraphicsQueue.FamilyIndex;
            colorToPresent.dstQueueFamilyIndex = renderer.PresentQueue.FamilyIndex;
            //clearToPresentBarrier.image set in loop
            colorToPresent.subresourceRange = subresourceRange;

            ImageMemoryBarrier[] colorToPresentBarriers = new ImageMemoryBarrier[] { colorToPresent };

            for (int i = 0; i < images.Count; i++) {
                var commandBuffer = commandBuffers[i];
                commandBuffer.Begin(beginInfo);

                colorToPresent.image = images[i];

                commandBuffer.PipelineBarrier(VkPipelineStageFlags.BottomOfPipeBit, VkPipelineStageFlags.BottomOfPipeBit,
                    VkDependencyFlags.None,
                    null, null, colorToPresentBarriers);

                commandBuffer.End();
            }
        }

        internal void Present(uint i) {
            presentInfo.imageIndices[0] = i;
            renderer.PresentQueue.Present(presentInfo);
        }

        public override SubmitInfo GetSubmitInfo() {
            throw new NotImplementedException();
        }

        void UpdateWaitSemaphores() {
            submitInfo.waitSemaphores = waitSemaphores.ToArray();
            var masks = new VkPipelineStageFlags[waitSemaphores.Count];
            for (int i = 0; i < waitSemaphores.Count; i++) {
                masks[i] = VkPipelineStageFlags.BottomOfPipeBit;
            }
            submitInfo.waitDstStageMask = masks;
        }

        public SubmitInfo GetSubmitInfo(uint index) {
            submitInfo.commandBuffers[0] = commandBuffers[(int)index];
            return submitInfo;
        }

        public override void AddInput(RenderNode node) {
            if (input.Contains(node)) return;
            input.Add(node);
            waitSemaphores.Add(node.SignalSemaphore);
            UpdateWaitSemaphores();
        }

        public override void RemoveInput(RenderNode node) {
            if (input.Remove(node)) {
                waitSemaphores.Remove(node.SignalSemaphore);
                UpdateWaitSemaphores();
            }
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposing) {
            if (disposed) return;

            if (disposing) {
                renderDoneSemaphore.Dispose();
            }

            disposed = true;
        }

        ~PresentNode() {
            Dispose(false);
        }
    }
}
