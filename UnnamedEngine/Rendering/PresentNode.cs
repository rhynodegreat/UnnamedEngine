using System;
using System.Collections.Generic;

using CSGL.Vulkan;

using UnnamedEngine.Core;

namespace UnnamedEngine.Rendering {
    public class PresentNode : RenderNode {
        Renderer renderer;
        AcquireImageNode acquireImageNode;
        
        List<CommandBuffer> commandBuffers;
        CommandBuffer[] submitBuffers;

        PresentInfo presentInfo;

        uint index;

        public PresentNode(Engine engine, AcquireImageNode acquireImageNode) : base(engine.Renderer.Device, VkPipelineStageFlags.BottomOfPipeBit) {
            if (engine == null) throw new ArgumentNullException(nameof(engine));
            if (acquireImageNode == null) throw new ArgumentNullException(nameof(acquireImageNode));
            renderer = engine.Renderer;
            this.acquireImageNode = acquireImageNode;
            
            commandBuffers = new List<CommandBuffer>();
            submitBuffers = new CommandBuffer[1];

            presentInfo = new PresentInfo();
            presentInfo.imageIndices = new uint[1];
            presentInfo.swapchains = new Swapchain[] { engine.Window.Swapchain };
            presentInfo.waitSemaphores = new Semaphore[] { SignalSemaphore };;

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

        public override void PreRender() {
            index = acquireImageNode.ImageIndex;
        }

        public override CommandBuffer[] GetCommands(out int count) {
            submitBuffers[0] = commandBuffers[(int)index];
            count = 1;
            return submitBuffers;
        }

        public override void PostRender() {
            presentInfo.imageIndices[0] = index;
            renderer.PresentQueue.Present(presentInfo);
        }
    }
}
