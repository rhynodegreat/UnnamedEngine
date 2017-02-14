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

            Camera camera = new Camera(engine, window, 90, .1f);
            engine.Camera = camera;
            camera.Transform.Position = new Vector3(0, 0, 1);

            FreeCam freeCam = new FreeCam(engine);

            CommandPoolCreateInfo info = new CommandPoolCreateInfo();
            info.queueFamilyIndex = graphics.GraphicsQueue.FamilyIndex;
            CommandPool commandPool = new CommandPool(graphics.Device, info);

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

            TriangleRenderer triangle = new TriangleRenderer(engine, deferred);
            BasicRenderer basic = new BasicRenderer(engine, deferred, mesh);
            StarRenderer stars = new StarRenderer(engine, deferred);

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

            renderer.Bake();

            QueueGraph graph = engine.QueueGraph;
            graph.Add(renderer);
            graph.Bake();

            Console.WriteLine(mesh.VertexData.VertexCount);
            Console.WriteLine(mesh.IndexData.IndexCount);

            GlyphCache cache = new GlyphCache(engine);
            Font font = new Font("C:/Windows/Fonts/arialbd.ttf");
            string letters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789.,<>\"'?/:;\\+-*/_[]{}|!@#$%^&*()`~";
            cache.Add(font, letters);
            Bitmap<Color3> bitmap = cache.Bitmaps[0];

            System.Drawing.Bitmap output = new System.Drawing.Bitmap(bitmap.Width, bitmap.Height);
            for (int x = 0; x < bitmap.Width; x++) {
                for (int y = 0; y < bitmap.Height; y++) {
                    float red = bitmap[x, y].r * 255;
                    float green = bitmap[x, y].g * 255;
                    float blue = bitmap[x, y].b * 255;

                    int r = (int)Math.Min(Math.Max(red, 0), 255);
                    int g = (int)Math.Min(Math.Max(green, 0), 255);
                    int b = (int)Math.Min(Math.Max(blue, 0), 255);

                    output.SetPixel(x, y, System.Drawing.Color.FromArgb(r, g, b));
                }
            }

            output.Save("output.png");

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
