using System;
using System.Collections.Generic;

using CSGL.GLFW;
using CSGL.Vulkan;
using Buffer = CSGL.Vulkan.Buffer;

using UnnamedEngine.Core;
using UWindow = UnnamedEngine.Core.Window;
using UnnamedEngine.Resources;
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

            StagingNode staging = new StagingNode(engine);
            presentNode.AddInput(staging);

            CommandGraph graph = engine.CommandGraph;
            graph.Add(acquireImageNode);
            graph.Add(presentNode);
            graph.Add(staging);
            graph.Bake();

            using (engine)
            using (commandPool) {
                engine.Run();
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
