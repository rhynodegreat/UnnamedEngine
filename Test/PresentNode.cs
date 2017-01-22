using System;
using System.Collections.Generic;

using CSGL.Vulkan;

using UnnamedEngine.Core;

namespace Test {
    public class PresentNode : CommandNode, IDisposable {
        bool disposed;
        Renderer renderer;
        AcquireImageNode acquireImageNode;
        
        List<CommandBuffer> commandBuffers;
        List<CommandBuffer> submitBuffers;
        Semaphore renderDoneSemaphore;

        PresentInfo presentInfo;

        uint index;

        public PresentNode(Engine engine, AcquireImageNode acquireImageNode, CommandPool commandPool) : base(engine.Renderer.Device, VkPipelineStageFlags.BottomOfPipeBit) {
            if (engine == null) throw new ArgumentNullException(nameof(engine));
            if (acquireImageNode == null) throw new ArgumentNullException(nameof(acquireImageNode));
            renderer = engine.Renderer;
            this.acquireImageNode = acquireImageNode;
            
            commandBuffers = new List<CommandBuffer>();
            submitBuffers = new List<CommandBuffer> { null };
            renderDoneSemaphore = new Semaphore(renderer.Device);

            presentInfo = new PresentInfo();
            presentInfo.imageIndices = new List<uint> { 0 };
            presentInfo.swapchains = new List<Swapchain> { engine.Window.Swapchain };
            presentInfo.waitSemaphores = new List<Semaphore> { renderDoneSemaphore };

            AddOutput(renderDoneSemaphore);

            CreateCommandBuffers(renderer, engine.Window.SwapchainImages, commandPool);
        }

        void CreateCommandBuffers(Renderer renderer, IList<Image> images, CommandPool commandPool) {
            commandBuffers = new List<CommandBuffer>(commandPool.Allocate(VkCommandBufferLevel.Primary, images.Count));

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

        public override void PreRender() {
            index = acquireImageNode.ImageIndex;
        }

        public override List<CommandBuffer> GetCommands() {
            submitBuffers[0] = commandBuffers[(int)index];
            return submitBuffers;
        }

        public override void PostRender() {
            presentInfo.imageIndices[0] = index;
            renderer.PresentQueue.Present(presentInfo);
        }

        public new void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected override void Dispose(bool disposing) {
            if (disposed) return;
            
            renderDoneSemaphore.Dispose();

            disposed = true;
        }

        ~PresentNode() {
            Dispose(false);
        }
    }
}
