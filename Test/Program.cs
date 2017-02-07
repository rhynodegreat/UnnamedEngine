using System;
using System.Collections.Generic;
using System.Numerics;
using System.IO;

using CSGL.GLFW;
using CSGL.Vulkan;

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

        void Run() {
            GLFW.Init();

            Instance instance = CreateInstance();
            PhysicalDevice physicalDevice = PickPhysicalDevice(instance);

            Graphics renderer = new Graphics(instance, physicalDevice);
            Engine engine = new Engine(renderer);

            UWindow window = new UWindow(engine, 800, 600, "Test");
            engine.Window = window;

            GBuffer gbuffer = new GBuffer(engine, window);

            Camera camera = new Camera(engine, window, 90, .1f);
            engine.Camera = camera;
            camera.Transform.Position = new Vector3(0, 0, 1);

            FreeCam freeCam = new FreeCam(engine);

            CommandPoolCreateInfo info = new CommandPoolCreateInfo();
            info.queueFamilyIndex = renderer.GraphicsQueue.FamilyIndex;
            CommandPool commandPool = new CommandPool(renderer.Device, info);

            AcquireImageNode acquireImageNode = new AcquireImageNode(engine, commandPool);
            PresentNode presentNode = new PresentNode(engine, acquireImageNode, commandPool);
            presentNode.AddInput(acquireImageNode);

            DeferredNode deferred = new DeferredNode(engine, gbuffer);
            ToneMapNode toneMap = new ToneMapNode(engine, acquireImageNode, gbuffer);
            toneMap.AddInput(deferred);
            presentNode.AddInput(toneMap);
            
            Mesh mesh;
            using (var stream = File.OpenRead("box.mesh")) {
                mesh = new Mesh(engine, stream);
            }

            TriangleRenderer triangle = new TriangleRenderer(engine, deferred);
            BasicRenderer basic = new BasicRenderer(engine, deferred, mesh);
            StarRenderer stars = new StarRenderer(engine, deferred);

            Light light1 = new Light();
            light1.Color = new CSGL.Graphics.Color(0.125f, 0.125f, 0.125f, 0);

            Light light2 = new Light();
            light2.Color = new CSGL.Graphics.Color(0.5f, 0.5f, 0.5f, 0);
            
            Light light3 = new Light();
            light3.Color = new CSGL.Graphics.Color(0.25f, 0.25f, 0.25f, 0);

            light3.Transform.Rotation = Quaternion.CreateFromYawPitchRoll((float)Math.PI * .65f, 0, 0) *
                                        Quaternion.CreateFromYawPitchRoll(0, -(float)Math.PI * .25f, 0);

            Ambient ambient = new Ambient(engine, deferred);
            deferred.Lighting.AddLighting(ambient);

            ambient.AddLight(light1);

            Directional directional = new Directional(engine, deferred, camera);
            deferred.Lighting.AddLighting(directional);

            directional.AddLight(light3);
            directional.AddLight(light2);

            CommandGraph graph = engine.CommandGraph;
            graph.Add(acquireImageNode);
            graph.Add(presentNode);
            graph.Add(deferred);
            graph.Add(toneMap);
            graph.Bake();

            Console.WriteLine(mesh.VertexData.VertexCount);
            Console.WriteLine(mesh.IndexData.IndexCount);

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
