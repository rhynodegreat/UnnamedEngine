using System;
using System.Collections.Generic;
using System.Numerics;

using CSGL.GLFW;
using CSGL.Vulkan;
using Buffer = CSGL.Vulkan.Buffer;

using UnnamedEngine.Core;
using UWindow = UnnamedEngine.Core.Window;
using UnnamedEngine.Resources;
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

        float[] data = {
            1, 2, 3
        };

        void Run() {
            GLFW.Init();

            Instance instance = CreateInstance();
            PhysicalDevice physicalDevice = PickPhysicalDevice(instance);

            Graphics renderer = new Graphics(instance, physicalDevice);
            Engine engine = new Engine(renderer);

            UWindow window = new UWindow(engine, 800, 600, "Test");
            engine.Window = window;

            GBuffer gbuffer = new GBuffer(engine, window);

            Camera camera = new Camera(engine, window, 90, .1f, 100);
            engine.Camera = camera;

            CommandPoolCreateInfo info = new CommandPoolCreateInfo();
            info.queueFamilyIndex = renderer.GraphicsQueue.FamilyIndex;
            CommandPool commandPool = new CommandPool(renderer.Device, info);

            AcquireImageNode acquireImageNode = new AcquireImageNode(engine, commandPool);
            PresentNode presentNode = new PresentNode(engine, acquireImageNode, commandPool);
            presentNode.AddInput(acquireImageNode);

            StagingNode staging = new StagingNode(engine);

            StarNode stars = new StarNode(engine, acquireImageNode, staging, camera);
            presentNode.AddInput(stars);

            TriangleNode triangle = new TriangleNode(engine, acquireImageNode, staging, camera);
            presentNode.AddInput(triangle);
            triangle.AddInput(stars);

            DeferredNode deferred = new DeferredNode(engine, gbuffer);

            CommandGraph graph = engine.CommandGraph;
            graph.Add(acquireImageNode);
            graph.Add(presentNode);
            graph.Add(staging);
            graph.Add(stars);
            graph.Add(triangle);
            graph.Add(deferred);
            graph.Bake();


            using (engine)
            using (commandPool)
            using (camera)
            using (gbuffer) {
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
