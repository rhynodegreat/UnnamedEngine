using System;
using System.Collections.Generic;

using CSGL.Vulkan;

using UnnamedEngine.Core;

namespace Test {
    public class AcquireImageNode : CommandNode, IDisposable {
        bool disposed;
        Engine engine;
        Graphics graphics;
        Window window;
        CommandPool pool;

        Semaphore acquireImageSemaphore;
        List<CommandBuffer> commandBuffers;
        List<CommandBuffer> submitBuffers;

        uint imageIndex;
        public uint ImageIndex {
            get {
                return imageIndex;
            }
        }

        public AcquireImageNode(Engine engine, CommandPool commandPool) : base(engine.Graphics.Device, VkPipelineStageFlags.TransferBit) {
            if (engine == null) throw new ArgumentNullException(nameof(engine));

            this.engine = engine;
            graphics = engine.Graphics;
            window = engine.Window;
            pool = commandPool;

            acquireImageSemaphore = new Semaphore(engine.Graphics.Device);
            submitBuffers = new List<CommandBuffer> { null };
            CreateCommandBuffer();

            AddInput(acquireImageSemaphore, VkPipelineStageFlags.TopOfPipeBit);

            window.OnSizeChanged += (int x, int y) => { CreateCommandBuffer(); };
        }

        void CreateCommandBuffer() {
            if (commandBuffers != null) pool.Free(commandBuffers);
            commandBuffers = new List<CommandBuffer>(pool.Allocate(VkCommandBufferLevel.Primary, window.SwapchainImages.Count));

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

            var subresourceRanges = new List<VkImageSubresourceRange> { subresourceRange };

            ImageMemoryBarrier presentToClear = new ImageMemoryBarrier();
            presentToClear.srcAccessMask = VkAccessFlags.MemoryReadBit;
            presentToClear.dstAccessMask = VkAccessFlags.TransferReadBit;
            presentToClear.oldLayout = VkImageLayout.Undefined;
            presentToClear.newLayout = VkImageLayout.TransferDstOptimal;
            presentToClear.srcQueueFamilyIndex = graphics.PresentQueue.FamilyIndex;
            presentToClear.dstQueueFamilyIndex = graphics.GraphicsQueue.FamilyIndex;
            //presentToClear.image set in loop
            presentToClear.subresourceRange = subresourceRange;

            List<ImageMemoryBarrier> presentToClearBarriers = new List<ImageMemoryBarrier> { presentToClear };

            ImageMemoryBarrier clearToFB = new ImageMemoryBarrier();
            clearToFB.srcAccessMask = VkAccessFlags.TransferWriteBit;
            clearToFB.dstAccessMask = VkAccessFlags.MemoryWriteBit | VkAccessFlags.MemoryReadBit;
            clearToFB.oldLayout = VkImageLayout.TransferDstOptimal;
            clearToFB.newLayout = VkImageLayout.ColorAttachmentOptimal;
            clearToFB.srcQueueFamilyIndex = graphics.GraphicsQueue.FamilyIndex;
            clearToFB.dstQueueFamilyIndex = graphics.GraphicsQueue.FamilyIndex;
            //clearToPresentBarrier.image set in loop
            clearToFB.subresourceRange = subresourceRange;

            List<ImageMemoryBarrier> clearToFBBarriers = new List<ImageMemoryBarrier> { clearToFB };

            for (int i = 0; i < window.SwapchainImages.Count; i++) {
                var commandBuffer = commandBuffers[i];
                commandBuffer.Begin(beginInfo);
                var image = window.SwapchainImages[i];

                presentToClear.image = image;
                clearToFB.image = image;

                commandBuffer.PipelineBarrier(VkPipelineStageFlags.TransferBit, VkPipelineStageFlags.TransferBit,
                    VkDependencyFlags.None,
                    null, null, presentToClearBarriers);

                commandBuffer.ClearColorImage(image, VkImageLayout.TransferDstOptimal, ref clearColor, subresourceRanges);

                commandBuffer.PipelineBarrier(VkPipelineStageFlags.TransferBit, VkPipelineStageFlags.TopOfPipeBit,
                    VkDependencyFlags.None,
                    null, null, clearToFBBarriers);

                commandBuffer.End();
            }
        }

        public override void PreRender() {
            engine.Window.Swapchain.AcquireNextImage(ulong.MaxValue, acquireImageSemaphore, out imageIndex);
        }

        public override List<CommandBuffer> GetCommands() {
            submitBuffers[0] = commandBuffers[(int)imageIndex];
            return submitBuffers;
        }

        public new void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected override void Dispose(bool disposing) {
            if (disposed) return;
            
            acquireImageSemaphore.Dispose();

            disposed = true;
        }

        ~AcquireImageNode() {
            Dispose(false);
        }
    }
}
