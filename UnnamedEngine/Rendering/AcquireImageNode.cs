using System;
using System.Collections.Generic;

using CSGL.Vulkan;
using UnnamedEngine.Core;

namespace UnnamedEngine.Rendering {
    public class AcquireImageNode : RenderNode, IDisposable {
        bool disposed;
        Renderer renderer;
        Swapchain swapchain;

        Semaphore acquireImageSemaphore;
        CommandBuffer[] commandBuffers;
        CommandBuffer[] submitBuffers;

        uint imageIndex;
        public uint ImageIndex {
            get {
                return imageIndex;
            }
        }

        public AcquireImageNode(Engine engine) : base(engine.Renderer.Device, VkPipelineStageFlags.TopOfPipeBit) {
            if (engine == null) throw new ArgumentNullException(nameof(engine));
            renderer = engine.Renderer;
            swapchain = engine.Window.Swapchain;

            acquireImageSemaphore = new Semaphore(engine.Renderer.Device);
            submitBuffers = new CommandBuffer[1];
            CreateCommandBuffer(renderer, engine.Window.SwapchainImages);
        }

        void CreateCommandBuffer(Renderer renderer, IList<Image> images) {
            commandBuffers = renderer.InternalCommandPool.Allocate(VkCommandBufferLevel.Primary, images.Count);

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

        public override void OnBake(RenderGraph graph) {
            SubmitInfo info = graph.GetSubmitInfo(this);
            int waitCount = info.waitSemaphores.Length;
            Semaphore[] wait = new Semaphore[waitCount + 1];
            VkPipelineStageFlags[] flags = new VkPipelineStageFlags[waitCount + 1];

            for (int i = 0; i < waitCount; i++) {
                wait[i] = info.waitSemaphores[i];
                flags[i] = info.waitDstStageMask[i];
            }

            wait[waitCount] = acquireImageSemaphore;
            flags[waitCount] = VkPipelineStageFlags.TopOfPipeBit;

            info.waitSemaphores = wait;
            info.waitDstStageMask = flags;
        }

        public override void PreRender() {
            swapchain.AcquireNextImage(ulong.MaxValue, acquireImageSemaphore, out imageIndex);
        }

        public override CommandBuffer[] GetCommands(out int count) {
            count = 1;
            submitBuffers[0] = commandBuffers[imageIndex];
            return submitBuffers;
        }

        public new void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected override void Dispose(bool disposing) {
            if (disposed) return;

            if (disposing) {
                base.Dispose(true);
                acquireImageSemaphore.Dispose();
            }

            disposed = true;
        }

        ~AcquireImageNode() {
            Dispose(false);
        }
    }
}
