using System;
using System.Collections.Generic;

using CSGL.Vulkan;

using UnnamedEngine.Core;

namespace UnnamedEngine.Rendering {
    public class ClearNode : RenderNode {
        Renderer renderer;
        Swapchain swapchain;
        AcquireImageNode acquireImageNode;

        CommandBuffer[] commandBuffers;
        CommandBuffer[] submitBuffers;

        uint index;

        public ClearNode(Engine engine, AcquireImageNode acquireImageNode) : base(engine.Renderer.Device, VkPipelineStageFlags.TopOfPipeBit) {
            renderer = engine.Renderer;
            swapchain = engine.Window.Swapchain;
            this.acquireImageNode = acquireImageNode;

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

        public override void PreRender() {
            index = acquireImageNode.ImageIndex;
        }

        public override CommandBuffer[] GetCommands(out int count) {
            count = 1;
            submitBuffers[0] = commandBuffers[(int)index];
            return submitBuffers;
        }
    }
}
