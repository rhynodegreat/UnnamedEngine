using System;
using System.Collections.Generic;

using CSGL.Vulkan;

using UnnamedEngine.Core;
using UnnamedEngine.Rendering;
using UnnamedEngine.Utilities;
using UnnamedEngine.ECS;

namespace UnnamedEngine.UI {
    public class Screen : IDisposable {
        bool disposed;

        Engine engine;
        SubmitNode submitNode;
        
        VkaAllocation stencilAlloc;
        Dictionary<Type, UIRenderer> rendererMap;
        CommandPool pool;
        CommandBuffer commandBuffer;
        CommandBufferBeginInfo beginInfo;

        public Image Stencil { get; private set; }
        public ImageView StencilView { get; private set; }

        public Camera Camera { get; set; }
        public int Width { get; private set; }
        public int Height { get; private set; }

        public EntityManager Manager { get; private set; }
        public Entity Root { get; private set; }
        public VkFormat StencilFormat { get; private set; } = VkFormat.D32SfloatS8Uint;

        public Screen(Engine engine, SubmitNode submitNode, Camera camera, int width, int height) {
            if (engine == null) throw new ArgumentNullException(nameof(engine));
            if (submitNode == null) throw new ArgumentNullException(nameof(submitNode));

            this.engine = engine;
            this.submitNode = submitNode;

            Camera = camera;
            Width = width;
            Height = height;

            rendererMap = new Dictionary<Type, UIRenderer>();

            Manager = new EntityManager();
            Root = new Entity();
            Root.AddComponent(new Transform());
            Root.AddComponent(new UIRoot(this));
            Manager.AddEntity(Root);

            CommandPoolCreateInfo poolInfo = new CommandPoolCreateInfo();
            poolInfo.queueFamilyIndex = engine.Graphics.GraphicsQueue.FamilyIndex;
            poolInfo.flags = VkCommandPoolCreateFlags.ResetCommandBufferBit;

            pool = new CommandPool(engine.Graphics.Device, poolInfo);
            commandBuffer = pool.Allocate(VkCommandBufferLevel.Primary);

            beginInfo = new CommandBufferBeginInfo();
            beginInfo.flags = VkCommandBufferUsageFlags.OneTimeSubmitBit;
            
            CreateStencil();
            CreateCommandBuffer();
        }

        public void AddRenderer(Type type, UIRenderer renderer) {
            if (rendererMap.ContainsKey(type)) throw new ScreenException("Type already has a renderer defined");
            rendererMap.Add(type, renderer);
        }

        public void RemoveRenderer(Type type) {
            rendererMap.Remove(type);
        }

        public void Recreate(int width, int height) {
            Width = width;
            Height = height;
            CreateStencil();
            CreateCommandBuffer();
        }

        public void Render(CommandBuffer commandBuffer) {
            Transform root = Root.GetFirst<Transform>();
            Stack<Transform> stack = new Stack<Transform>();
            stack.Push(root);

            while (stack.Count > 0) {
                Transform current = stack.Pop();

                for (int i = current.ChildCount - 1; i >= 0; i--) {
                    stack.Push(current[i]);
                }

                Render(commandBuffer, Manager.GetEntity(current));
            }
        }

        void Render(CommandBuffer commandBuffer, Entity e) {
            UIElement element = e.GetFirst<UIElement>();
            if (element == null) return;

            Type type = element.GetType();
            if (!rendererMap.ContainsKey(type)) return;

            rendererMap[type].Render(commandBuffer, e);
        }

        void CreateStencil() {
            engine.Graphics.Allocator.Free(stencilAlloc);
            Stencil?.Dispose();
            StencilView?.Dispose();

            ImageCreateInfo info = new ImageCreateInfo();
            info.imageType = VkImageType._2d;
            info.format = StencilFormat;
            info.extent.width = (uint)Width;
            info.extent.height = (uint)Height;
            info.extent.depth = 1;
            info.mipLevels = 1;
            info.arrayLayers = 1;
            info.samples = VkSampleCountFlags._1Bit;
            info.tiling = VkImageTiling.Optimal;
            info.usage = VkImageUsageFlags.DepthStencilAttachmentBit;
            info.sharingMode = VkSharingMode.Exclusive;
            info.initialLayout = VkImageLayout.Undefined;

            Stencil = new Image(engine.Graphics.Device, info);
            stencilAlloc = engine.Graphics.Allocator.Alloc(Stencil.Requirements, VkMemoryPropertyFlags.DeviceLocalBit);
            Stencil.Bind(stencilAlloc.memory, stencilAlloc.offset);

            ImageViewCreateInfo viewInfo = new ImageViewCreateInfo();
            viewInfo.image = Stencil;
            viewInfo.viewType = VkImageViewType._2d;
            viewInfo.format = StencilFormat;
            viewInfo.subresourceRange.aspectMask = VkImageAspectFlags.DepthBit | VkImageAspectFlags.StencilBit;
            viewInfo.subresourceRange.baseArrayLayer = 0;
            viewInfo.subresourceRange.layerCount = 1;
            viewInfo.subresourceRange.baseMipLevel = 0;
            viewInfo.subresourceRange.levelCount = 1;

            StencilView = new ImageView(engine.Graphics.Device, viewInfo);
        }

        void CreateCommandBuffer() {
            commandBuffer.Reset(VkCommandBufferResetFlags.None);

            commandBuffer.Begin(beginInfo);

            commandBuffer.PipelineBarrier(VkPipelineStageFlags.TopOfPipeBit, VkPipelineStageFlags.TopOfPipeBit, VkDependencyFlags.None,
                null, null, new List<ImageMemoryBarrier> {
                    new ImageMemoryBarrier {
                        image = Stencil,
                        oldLayout = VkImageLayout.Undefined,
                        newLayout = VkImageLayout.DepthStencilAttachmentOptimal,
                        srcQueueFamilyIndex = uint.MaxValue,
                        dstQueueFamilyIndex = uint.MaxValue,
                        subresourceRange = StencilView.SubresourceRange,
                        srcAccessMask = VkAccessFlags.None,
                        dstAccessMask = VkAccessFlags.DepthStencilAttachmentReadBit | VkAccessFlags.DepthStencilAttachmentWriteBit
                    }
                }
            );

            commandBuffer.End();

            submitNode.SubmitOnce(commandBuffer);
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposing) {
            if (disposed) return;
            
            StencilView.Dispose();
            Stencil.Dispose();
            engine.Graphics.Allocator.Free(stencilAlloc);
            pool.Dispose();

            foreach (var renderer in rendererMap.Values) renderer.Dispose();

            disposed = true;
        }

        ~Screen() {
            Dispose(true);
        }
    }

    public class ScreenException : Exception {
        public ScreenException(string message) : base(message) { }
    }
}
