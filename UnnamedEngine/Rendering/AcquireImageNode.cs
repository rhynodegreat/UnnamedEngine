using System;
using System.Collections.Generic;

using CSGL.Vulkan;
using UnnamedEngine.Core;

namespace UnnamedEngine.Rendering {
    public class AcquireImageNode : RenderNode, IDisposable {
        bool disposed;
        Renderer renderer;
        Swapchain swapchain;
        
        List<CommandBuffer> commandBuffers;
        uint imageIndex;

        SubmitInfo info;
        CommandBuffer[] submitBuffers;
        Semaphore acquireImageSemaphore;

        internal AcquireImageNode(Engine engine) {
            renderer = engine.Renderer;
            swapchain = engine.Window.Swapchain;
            SignalSemaphore = new Semaphore(engine.Renderer.Device);
            acquireImageSemaphore = new Semaphore(engine.Renderer.Device);

            submitBuffers = new CommandBuffer[1];
            info = new SubmitInfo();
            info.commandBuffers = submitBuffers;
            info.signalSemaphores = new Semaphore[] { SignalSemaphore };
            info.waitDstStageMask = new VkPipelineStageFlags[] { VkPipelineStageFlags.TopOfPipeBit };
            info.waitSemaphores = new Semaphore[] { acquireImageSemaphore };

            CreateCommandBuffer(renderer, engine.Window.SwapchainImages);
        }

        void CreateCommandBuffer(Renderer renderer, IList<Image> images) {
            commandBuffers = new List<CommandBuffer>(renderer.InternalCommandPool.Allocate(VkCommandBufferLevel.Primary, images.Count));

            CommandBufferBeginInfo beginInfo = new CommandBufferBeginInfo();
            beginInfo.flags = VkCommandBufferUsageFlags.SimultaneousUseBit;

            VkClearColorValue clearColor = new VkClearColorValue();
            clearColor.float32_0 = 0;
            clearColor.float32_1 = 0;
            clearColor.float32_2 = 0;
            clearColor.float32_3 = 0;

            VkImageSubresourceRange subresourceRange = new VkImageSubresourceRange();
            subresourceRange.aspectMask = VkImageAspectFlags.ColorBit;
            subresourceRange.baseMipLevel = 0;
            subresourceRange.levelCount = 1;
            subresourceRange.baseArrayLayer = 0;
            subresourceRange.layerCount = 1;

            var subresourceRanges = new VkImageSubresourceRange[] { subresourceRange };

            ImageMemoryBarrier presentToClear = new ImageMemoryBarrier();
            presentToClear.srcAccessMask = VkAccessFlags.MemoryReadBit;
            presentToClear.dstAccessMask = VkAccessFlags.TransferReadBit;
            presentToClear.oldLayout = VkImageLayout.Undefined;
            presentToClear.newLayout = VkImageLayout.TransferDstOptimal;
            presentToClear.srcQueueFamilyIndex = renderer.PresentQueue.FamilyIndex;
            presentToClear.dstQueueFamilyIndex = renderer.GraphicsQueue.FamilyIndex;
            //presentToClear.image set in loop
            presentToClear.subresourceRange = subresourceRange;

            ImageMemoryBarrier[] presentToClearBarriers = new ImageMemoryBarrier[] { presentToClear };

            ImageMemoryBarrier clearToFB = new ImageMemoryBarrier();
            clearToFB.srcAccessMask = VkAccessFlags.TransferWriteBit;
            clearToFB.dstAccessMask = VkAccessFlags.MemoryWriteBit | VkAccessFlags.MemoryReadBit;
            clearToFB.oldLayout = VkImageLayout.TransferDstOptimal;
            clearToFB.newLayout = VkImageLayout.ColorAttachmentOptimal;
            clearToFB.srcQueueFamilyIndex = renderer.GraphicsQueue.FamilyIndex;
            clearToFB.dstQueueFamilyIndex = renderer.GraphicsQueue.FamilyIndex;
            //clearToPresentBarrier.image set in loop
            clearToFB.subresourceRange = subresourceRange;

            ImageMemoryBarrier[] clearToFBBarriers = new ImageMemoryBarrier[] { clearToFB };

            for (int i = 0; i < images.Count; i++) {
                var commandBuffer = commandBuffers[i];
                commandBuffer.Begin(beginInfo);

                presentToClear.image = images[i];
                clearToFB.image = images[i];

                commandBuffer.PipelineBarrier(VkPipelineStageFlags.TransferBit, VkPipelineStageFlags.TransferBit,
                    VkDependencyFlags.None,
                    null, null, presentToClearBarriers);

                commandBuffer.ClearColorImage(images[i], VkImageLayout.TransferDstOptimal, ref clearColor, subresourceRanges);

                commandBuffer.PipelineBarrier(VkPipelineStageFlags.TransferBit, VkPipelineStageFlags.TopOfPipeBit,
                    VkDependencyFlags.None,
                    null, null, clearToFBBarriers);

                commandBuffer.End();
            }
        }

        internal uint AcquireImage() {
            swapchain.AcquireNextImage(ulong.MaxValue, acquireImageSemaphore, out imageIndex);
            return imageIndex;
        }

        public override SubmitInfo GetSubmitInfo() {
            info.commandBuffers[0] = commandBuffers[(int)imageIndex];
            return info;
        }

        public override void AddInput(RenderNode node) {
            throw new RenderNodeException("Cannot add input to AcquireImage node");
        }

        public override void RemoveInput(RenderNode node) {
            throw new NotImplementedException();
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposing) {
            if (disposed) return;

            if (disposing) {
                acquireImageSemaphore.Dispose();
                SignalSemaphore.Dispose();
            }

            acquireImageSemaphore = null;

            disposed = true;
        }

        ~AcquireImageNode() {
            Dispose(false);
        }
    }
}
