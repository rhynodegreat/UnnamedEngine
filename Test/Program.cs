using System;
using System.Collections.Generic;
using System.Numerics;
using System.IO;

using CSGL.GLFW;
using CSGL.Graphics;
using CSGL.Vulkan;

using UnnamedEngine.Core;
using UWindow = UnnamedEngine.Core.Window;
using UnnamedEngine.Resources;
using UnnamedEngine.Rendering;
using UnnamedEngine.UI.Text;
using UnnamedEngine.ECS;
using UnnamedEngine.UI;

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

            Graphics graphics = new Graphics(instance, physicalDevice);
            Engine engine = new Engine(graphics);

            UWindow window = new UWindow(engine, 800, 600, "Test");
            engine.Window = window;

            GBuffer gbuffer = new GBuffer(engine, window);

            PerspectiveCamera camera = new PerspectiveCamera(90, .1f);
            engine.Cameras.AddCamera(camera);
            camera.Transform.Position = new Vector3(0, 0, 1);

            camera.Recreate(window.Width, window.Height);
            window.OnSizeChanged += camera.Recreate;

            FreeCam freeCam = new FreeCam(engine, camera);

            CommandPoolCreateInfo info = new CommandPoolCreateInfo();
            info.queueFamilyIndex = graphics.GraphicsQueue.FamilyIndex;

            Mesh mesh;
            using (var stream = File.OpenRead("box.mesh")) {
                mesh = new Mesh(engine, stream);
            }

            Renderer renderer = new Renderer(engine);
            renderer.AddInput(engine.Graphics.TransferNode);

            Deferred deferred = new Deferred(engine, gbuffer);
            renderer.AddNode(deferred);

            ToneMapper toneMapper = new ToneMapper(engine, renderer, gbuffer);
            renderer.AddNode(toneMapper);
            toneMapper.AddInput(deferred);

            TriangleRenderer triangle = new TriangleRenderer(engine, deferred, camera);
            BasicRenderer basic = new BasicRenderer(engine, deferred, mesh, camera);
            StarRenderer stars = new StarRenderer(engine, deferred, camera);

            Light light1 = new Light();
            light1.Color = new CSGL.Graphics.Color4(0.125f, 0.125f, 0.125f, 0);

            Light light2 = new Light();
            light2.Color = new CSGL.Graphics.Color4(0.5f, 0.5f, 0.5f, 0);
            
            Light light3 = new Light();
            light3.Color = new CSGL.Graphics.Color4(0.25f, 0.25f, 0.25f, 0);

            light3.Transform.Rotation = Quaternion.CreateFromYawPitchRoll((float)Math.PI * .65f, 0, 0) *
                                        Quaternion.CreateFromYawPitchRoll(0, -(float)Math.PI * .25f, 0);

            Light light4 = new Light();
            light4.Color = new CSGL.Graphics.Color4(0.125f, 0.125f, 0.125f, 0);
            light4.Transform.Position = new Vector3(0, 0.6f, 0.25f);

            Ambient ambient = new Ambient(engine, deferred);
            deferred.Lighting.AddLighting(ambient);

            //ambient.AddLight(light1);

            Directional directional = new Directional(engine, deferred, 32);
            deferred.Lighting.AddLighting(directional);

            //directional.AddLight(light3);
            //directional.AddLight(light2);

            Point point = new Point(engine, deferred, camera, 4096);
            deferred.Lighting.AddLighting(point);

            point.AddLight(light4);

            Camera uiCam = new OrthographicCamera(window.Width, window.Height, -1, 1);
            window.OnSizeChanged += uiCam.Recreate;
            engine.Cameras.AddCamera(uiCam);
            FullscreenUI ui = new FullscreenUI(engine, uiCam, renderer);
            ui.AddInput(toneMapper);

            PanelRenderer panelRenderer = new PanelRenderer(engine, window, ui.Screen, ui.RenderPass);
            ui.Screen.AddRenderer(typeof(Panel), panelRenderer);

            Entity e = new Entity();
            Transform t = new Transform();
            Panel p = new Panel();
            p.Size = new Vector2(300, 25);
            p.Color = new Color4(1, 0, 0, 1);
            e.AddComponent(t);
            e.AddComponent(p);
            t.Parent = ui.Screen.Root.GetFirst<Transform>();
            ui.Screen.Manager.AddEntity(e);
            t.Rotation = Quaternion.CreateFromYawPitchRoll(0, 0, 1);
            t.Position = new Vector3(100, 50, 0);

            int pageSize = 1024;
            float range = 4;
            int padding = 1;
            float scale = 2;
            float threshold = 1.000001f;

            GlyphCache cache = new GlyphCache(engine, 1, pageSize, range, padding, scale, threshold);
            Font font = new Font("C:/Windows/Fonts/arialbd.ttf");
            for (int i = 33; i < 127; i++) {    //ascii 33 (!) to 126 (~)
                cache.AddChar(font, i);
            }
            cache.Update();

            renderer.AddNode(ui);
            renderer.Bake();

            QueueGraph graph = engine.QueueGraph;
            graph.Add(renderer);
            graph.Bake();

            CommandPoolCreateInfo poolInfo = new CommandPoolCreateInfo();
            poolInfo.flags = VkCommandPoolCreateFlags.ResetCommandBufferBit;
            poolInfo.queueFamilyIndex = engine.Graphics.GraphicsQueue.FamilyIndex;
            CommandPool pool = new CommandPool(engine.Graphics.Device, poolInfo);

            CommandBuffer commandBuffer = pool.Allocate(VkCommandBufferLevel.Primary);

            CommandBufferBeginInfo beginInfo = new CommandBufferBeginInfo();
            beginInfo.flags = VkCommandBufferUsageFlags.OneTimeSubmitBit;
            commandBuffer.Begin(beginInfo);
            commandBuffer.PipelineBarrier(VkPipelineStageFlags.TopOfPipeBit, VkPipelineStageFlags.TopOfPipeBit, VkDependencyFlags.None,
                null, null, new List<ImageMemoryBarrier> {
                    new ImageMemoryBarrier {
                        image = ui.Screen.Stencil,
                        oldLayout = VkImageLayout.Undefined,
                        newLayout = VkImageLayout.DepthStencilAttachmentOptimal,
                        srcQueueFamilyIndex = uint.MaxValue,
                        dstQueueFamilyIndex = uint.MaxValue,
                        subresourceRange = ui.Screen.StencilView.SubresourceRange,
                        srcAccessMask = VkAccessFlags.None,
                        dstAccessMask = VkAccessFlags.DepthStencilAttachmentReadBit | VkAccessFlags.DepthStencilAttachmentWriteBit
                    }
                }
            );
            commandBuffer.End();

            renderer.SubmitOnce(commandBuffer);

            using (engine)
            using (gbuffer)
            using (pool)
            using (cache) {
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
