using System;
using System.Collections.Generic;

using CSGL.GLFW;
using CSGL.Vulkan;
using Buffer = CSGL.Vulkan.Buffer;

using UnnamedEngine.Core;
using UnnamedEngine.Rendering;
using UWindow = UnnamedEngine.Core.Window;
using UnnamedEngine.Utilities;

namespace Test {
    class Program {
        static void Main(string[] args) {
            new Program().Run();
        }

        string[] layers = {
            "VK_LAYER_LUNARG_standard_validation",
            //"VK_LAYER_LUNARG_api_dump"
        };

        float[] data = {
            1, 2, 3
        };

        void Run() {
            GLFW.Init();

            Instance instance = CreateInstance();
            PhysicalDevice physicalDevice = PickPhysicalDevice(instance);

            Renderer renderer = new Renderer(instance, physicalDevice);
            Engine engine = new Engine(renderer);

            UWindow window = new UWindow(engine, 800, 600, "Test");
            engine.Window = window;

            CommandPoolCreateInfo info = new CommandPoolCreateInfo();
            info.queueFamilyIndex = renderer.GraphicsQueue.FamilyIndex;
            CommandPool commandPool = new CommandPool(renderer.Device, info);

            AcquireImageNode acquireImageNode = new AcquireImageNode(engine, commandPool);
            PresentNode presentNode = new PresentNode(engine, acquireImageNode, commandPool);
            presentNode.AddInput(acquireImageNode);

            RenderGraph graph = engine.RenderGraph;
            graph.Add(acquireImageNode);
            graph.Add(presentNode);
            graph.Bake();

            VkAllocator vkAlloc = new VkAllocator(renderer.Device, 32 * 1024 * 1024);

            BufferCreateInfo bufferInfo = new BufferCreateInfo();
            bufferInfo.queueFamilyIndices = new List<uint> { renderer.GraphicsQueue.FamilyIndex };
            bufferInfo.usage = VkBufferUsageFlags.VertexBufferBit;
            bufferInfo.sharingMode = VkSharingMode.Exclusive;
            bufferInfo.size = (ulong)CSGL.Interop.SizeOf(this.data);
            Buffer buffer = new Buffer(renderer.Device, bufferInfo);
            
            var alloc = vkAlloc.Alloc(buffer.Requirements, VkMemoryPropertyFlags.HostCoherentBit | VkMemoryPropertyFlags.HostVisibleBit);
            buffer.Bind(alloc.memory, alloc.offset);
            IntPtr data = alloc.memory.Map(alloc.offset, alloc.size);
            CSGL.Interop.Copy(this.data, data);
            alloc.memory.Unmap();

            using (engine)
            using (commandPool)
            using (vkAlloc) {
                using (buffer) {
                    engine.Run();
                }
                vkAlloc.Free(alloc);
            }

            GLFW.Terminate();
        }

        Instance CreateInstance() {
            ApplicationInfo appInfo = new ApplicationInfo(new VkVersion(1, 0, 0), new VkVersion(0, 0, 0), new VkVersion(0, 1, 0), "Test", "Unnamed Engine");
            InstanceCreateInfo info = new InstanceCreateInfo(appInfo, new List<string>(GLFW.GetRequiredInstanceExceptions()), new List<string>(layers));
            return new Instance(info);
        }

        PhysicalDevice PickPhysicalDevice(Instance instance) {
            return instance.PhysicalDevices[0];
        }
    }
}
