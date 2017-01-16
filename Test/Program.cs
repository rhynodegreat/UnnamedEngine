using System;
using System.Collections.Generic;

using CSGL.GLFW;
using CSGL.Vulkan;

using UnnamedEngine.Core;
using UnnamedEngine.Rendering;

namespace Test {
    class Program {
        static void Main(string[] args) {
            new Program().Run();
        }

        string[] layers = {
            "VK_LAYER_LUNARG_standard_validation",
            //"VK_LAYER_LUNARG_api_dump"
        };

        void Run() {
            GLFW.Init();

            Instance instance = CreateInstance();
            PhysicalDevice physicalDevice = PickPhysicalDevice(instance);

            Renderer renderer = new Renderer(instance, physicalDevice);
            Engine engine = new Engine(renderer);

            Window window = new Window(engine, 800, 600, "Test");
            engine.Window = window;

            AcquireImageNode acquireImageNode = new AcquireImageNode(engine);
            PresentNode presentNode = new PresentNode(engine, acquireImageNode);
            presentNode.AddInput(acquireImageNode);

            RenderGraph graph = new RenderGraph(engine);
            engine.RenderGraph = graph;
            graph.Add(acquireImageNode);
            graph.Add(presentNode);
            graph.Bake();

            using (engine) {
                engine.Run();
            }

            GLFW.Terminate();
        }

        Instance CreateInstance() {
            ApplicationInfo appInfo = new ApplicationInfo(new VkVersion(1, 0, 0), new VkVersion(0, 0, 0), new VkVersion(0, 1, 0), "Test", "Unnamed Engine");
            InstanceCreateInfo info = new InstanceCreateInfo(appInfo, GLFW_VK.GetRequiredInstanceExceptions(), layers);
            return new Instance(info);
        }

        PhysicalDevice PickPhysicalDevice(Instance instance) {
            return instance.PhysicalDevices[0];
        }
    }
}
