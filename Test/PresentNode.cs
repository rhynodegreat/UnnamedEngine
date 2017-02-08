using System;
using System.Collections.Generic;

using CSGL.Vulkan;

using UnnamedEngine.Core;

namespace Test {
    public class PresentNode : CommandNode, IDisposable {
        bool disposed;
        Graphics graphics;
        AcquireImageNode acquireImageNode;
        Window window;
        CommandPool commandPool;
        
        List<CommandBuffer> commandBuffers;
        List<CommandBuffer> submitBuffers;
        Semaphore renderDoneSemaphore;

        PresentInfo presentInfo;

        uint index;

        public PresentNode(Engine engine, AcquireImageNode acquireImageNode, CommandPool commandPool) : base(engine.Graphics.Device, engine.Graphics.GraphicsQueue, VkPipelineStageFlags.BottomOfPipeBit) {
            if (engine == null) throw new ArgumentNullException(nameof(engine));
            if (acquireImageNode == null) throw new ArgumentNullException(nameof(acquireImageNode));
            graphics = engine.Graphics;
            this.acquireImageNode = acquireImageNode;
            window = engine.Window;
            this.commandPool = commandPool;
            
            commandBuffers = new List<CommandBuffer>();
            submitBuffers = new List<CommandBuffer> { null };
            renderDoneSemaphore = new Semaphore(graphics.Device);

            presentInfo = new PresentInfo();
            presentInfo.imageIndices = new List<uint> { 0 };
            presentInfo.swapchains = new List<Swapchain> { engine.Window.Swapchain };
            presentInfo.waitSemaphores = new List<Semaphore> { renderDoneSemaphore };

            AddOutput(renderDoneSemaphore);

            CreateCommandBuffers();
            window.OnSizeChanged += (int x, int y) => {
                CreateCommandBuffers();
                
                presentInfo = new PresentInfo();
                presentInfo.imageIndices = new List<uint> { 0 };
                presentInfo.swapchains = new List<Swapchain> { engine.Window.Swapchain };
                presentInfo.waitSemaphores = new List<Semaphore> { renderDoneSemaphore };
            };
        }

        void CreateCommandBuffers() {
            if (commandBuffers.Count > 0) commandPool.Free(commandBuffers);
            commandBuffers = new List<CommandBuffer>(commandPool.Allocate(VkCommandBufferLevel.Primary, window.SwapchainImages.Count));

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
            colorToPresent.srcQueueFamilyIndex = graphics.GraphicsQueue.FamilyIndex;
            colorToPresent.dstQueueFamilyIndex = graphics.PresentQueue.FamilyIndex;
            //clearToPresentBarrier.image set in loop
            colorToPresent.subresourceRange = subresourceRange;

            List<ImageMemoryBarrier> colorToPresentBarriers = new List<ImageMemoryBarrier> { colorToPresent };

            for (int i = 0; i < window.SwapchainImages.Count; i++) {
                var commandBuffer = commandBuffers[i];
                commandBuffer.Begin(beginInfo);

                colorToPresent.image = window.SwapchainImages[i];

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
            graphics.PresentQueue.Present(presentInfo);
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
