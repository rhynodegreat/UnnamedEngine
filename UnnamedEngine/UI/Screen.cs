using System;
using System.Collections.Generic;

using CSGL.Vulkan1;

using UnnamedEngine.Core;
using UnnamedEngine.Rendering;
using UnnamedEngine.Utilities;

namespace UnnamedEngine.UI {
    public class Screen : IDisposable {
        struct RenderInfo {
            public UIRenderer renderer;
            public UIElement element;

            public RenderInfo(UIRenderer renderer, UIElement element) {
                this.renderer = renderer;
                this.element = element;
            }
        }

        bool disposed;

        Engine engine;
        SubmitNode submitNode;
        
        Dictionary<Type, UIRenderer> rendererMap;
        List<UIRenderer> rendererList;
        CommandPool pool;
        CommandBuffer commandBuffer;
        CommandBufferBeginInfo beginInfo;

        Stack<UIElement> stack;
        List<RenderInfo> list;

        public Image Stencil { get; private set; }
        public ImageView StencilView { get; private set; }

        public Camera Camera { get; set; }
        public int Width { get; private set; }
        public int Height { get; private set; }

        public List<UIElement> roots;
        
        public VkFormat StencilFormat { get; private set; } = VkFormat.D32SfloatS8Uint;

        public event Action<int, int> OnSizeChanged = delegate { };

        public Screen(Engine engine, SubmitNode submitNode, Camera camera, int width, int height) {
            if (engine == null) throw new ArgumentNullException(nameof(engine));
            if (submitNode == null) throw new ArgumentNullException(nameof(submitNode));

            this.engine = engine;
            this.submitNode = submitNode;

            Camera = camera;
            Width = width;
            Height = height;

            roots = new List<UIElement>();

            rendererMap = new Dictionary<Type, UIRenderer>();
            rendererList = new List<UIRenderer>();

            stack = new Stack<UIElement>();
            list = new List<RenderInfo>();

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
            rendererList.Add(renderer);
        }

        public void RemoveRenderer(Type type) {
            if (rendererMap.ContainsKey(type)) {
                rendererList.Remove(rendererMap[type]);
                rendererMap.Remove(type);
            }
        }

        public void AddRoot(UIElement element) {
            if (roots.Contains(element)) throw new ScreenException("Element has already been added to this screen");
            roots.Add(element);
        }

        public bool RemoveRoot(UIElement element) {
            return roots.Remove(element);
        }

        public void Recreate(int width, int height) {
            Width = width;
            Height = height;
            CreateStencil();
            CreateCommandBuffer();

            OnSizeChanged(width, height);
        }

        public void PreRender() {
            stack.Clear();
            list.Clear();

            for (int i = roots.Count - 1; i >= 0; i--) {
                stack.Push(roots[i]);
            }

            while (stack.Count > 0) {
                UIElement element = stack.Pop();

                for (int i = element.ChildCount - 1; i >= 0; i--) {
                    stack.Push(element[i]);
                }

                Type type = element.GetType();
                if (!rendererMap.ContainsKey(type)) continue;

                UIRenderer renderer = rendererMap[type];
                renderer.PreRenderElement(element);
                list.Add(new RenderInfo(renderer, element));
            }

            for (int i = 0; i < rendererList.Count; i++) {
                rendererList[i].PreRender();
            }
        }

        public void Render(CommandBuffer commandBuffer) {
            for (int i = 0; i < list.Count; i++) {
                list[i].renderer.Render(commandBuffer, list[i].element);
            }
        }

        void CreateStencil() {
            engine.Memory.FreeDevice(Stencil);
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

            Stencil = engine.Memory.AllocDevice(info);

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
            engine.Memory.FreeDevice(Stencil);
            pool.Dispose();

            if (disposing) {
                foreach (var renderer in rendererMap.Values) renderer.Dispose();
            }

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
