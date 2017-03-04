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

        CommandPool pool;
        CommandBuffer commandBuffer;
        VkaAllocation stencilAlloc;
        Dictionary<Type, IRenderer> rendererMap;
        CommandBufferBeginInfo beginInfo;

        public Image Stencil { get; private set; }
        public ImageView StencilView { get; private set; }

        public Camera Camera { get; set; }
        public bool Clear { get; set; }
        public int Width { get; private set; }
        public int Height { get; private set; }

        public EntityManager Manager { get; private set; }
        public Entity Root { get; private set; }

        public Screen(Engine engine, Camera camera, int width, int height, bool clear) {
            this.engine = engine;

            Camera = camera;
            Clear = clear;
            Width = width;
            Height = height;

            rendererMap = new Dictionary<Type, IRenderer>();

            Manager = new EntityManager();
            Root = new Entity();
            Root.AddComponent(new Transform());
            Root.AddComponent(new UIRoot(this));
            Manager.AddEntity(Root);

            CreateCommandPool();
            CreateStencil();

            beginInfo = new CommandBufferBeginInfo();
            beginInfo.flags = VkCommandBufferUsageFlags.OneTimeSubmitBit;
        }

        public void AddRenderer(Type type, IRenderer renderer) {
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
        }

        public void Render() {
            commandBuffer.Reset(VkCommandBufferResetFlags.None);
            commandBuffer.Begin(beginInfo);

            Transform root = Root.GetFirst<Transform>();
            Stack<Transform> stack = new Stack<Transform>();
            stack.Push(root);

            while (stack.Count > 0) {
                Transform current = stack.Pop();

                for (int i = current.ChildCount - 1; i >= 0; i++) {
                    stack.Push(current[i]);
                    Render(Manager.GetEntity(current));
                }
            }

            commandBuffer.End();
        }

        void Render(Entity e) {
            UIElement element = e.GetFirst<UIElement>();
            if (element == null) return;

            Type type = element.GetType();
            if (!rendererMap.ContainsKey(type)) return;

            rendererMap[type].Render(commandBuffer);
        }

        void CreateCommandPool() {
            CommandPoolCreateInfo info = new CommandPoolCreateInfo();
            info.flags = VkCommandPoolCreateFlags.ResetCommandBufferBit;
            info.queueFamilyIndex = engine.Graphics.GraphicsQueue.FamilyIndex;

            pool = new CommandPool(engine.Graphics.Device, info);

            commandBuffer = pool.Allocate(VkCommandBufferLevel.Primary);
        }

        void CreateStencil() {
            engine.Graphics.Allocator.Free(stencilAlloc);
            Stencil?.Dispose();
            StencilView?.Dispose();

            ImageCreateInfo info = new ImageCreateInfo();
            info.imageType = VkImageType._2d;
            info.format = VkFormat.D32SfloatS8Uint;
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
            viewInfo.format = VkFormat.D32SfloatS8Uint;
            viewInfo.subresourceRange.aspectMask = VkImageAspectFlags.StencilBit;
            viewInfo.subresourceRange.baseArrayLayer = 0;
            viewInfo.subresourceRange.layerCount = 1;
            viewInfo.subresourceRange.baseMipLevel = 0;
            viewInfo.subresourceRange.levelCount = 1;

            StencilView = new ImageView(engine.Graphics.Device, viewInfo);
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposing) {
            if (disposed) return;

            pool.Dispose();
            StencilView.Dispose();
            Stencil.Dispose();
            engine.Graphics.Allocator.Free(stencilAlloc);

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
