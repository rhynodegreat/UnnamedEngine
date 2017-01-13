using System;
using System.Collections.Generic;

using CSGL.GLFW;
using CSGL.Vulkan;

using UnnamedEngine.Core;

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
            Device device = null;

            Renderer renderer = new Renderer(instance, device);
            Engine engine = new Engine(renderer);

            GLFW.Terminate();
        }

        Instance CreateInstance() {
            ApplicationInfo appInfo = new ApplicationInfo(new VkVersion(1, 0, 0), new VkVersion(0, 0, 0), new VkVersion(0, 1, 0), "Test", "Unnamed Engine");
            InstanceCreateInfo info = new InstanceCreateInfo(appInfo, GLFW_VK.GetRequiredInstanceExceptions(), layers);
            return new Instance(info);
        }

        Device CreateDevice(Instance instance) {
            return null;
        }
    }
}
