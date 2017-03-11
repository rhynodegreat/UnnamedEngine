using System;
using System.Collections.Generic;

using CSGL;
using CSGL.Vulkan;
using Buffer = CSGL.Vulkan.Buffer;

using UnnamedEngine.Core;

namespace UnnamedEngine.Resources {
    public class UniformBuffer<T> : IDisposable where T : struct {
        bool disposed;

        Engine engine;
        
        int alignedSize;
        VkBufferUsageFlags usage;
        Buffer buffer;
        int count;
        List<T> data;

        public UniformBuffer(Engine engine, int count, VkBufferUsageFlags usage) {
            if (engine == null) throw new ArgumentNullException(nameof(engine));
            if (count < 1) throw new ArgumentOutOfRangeException(nameof(count));

            this.engine = engine;

            data = new List<T>(count);
            this.count = count;
            this.usage = usage;

            int alignment = (int)engine.Graphics.PhysicalDevice.Properties.Limits.minUniformBufferOffsetAlignment;
            alignedSize = 0;
            while (alignedSize < Interop.SizeOf<T>()) alignedSize += alignment;

            CreateBuffer();
        }

        void CreateBuffer() {
            engine.Memory.FreeUniform(buffer);

            BufferCreateInfo info = new BufferCreateInfo() {
                usage = usage | VkBufferUsageFlags.UniformBufferBit,
                size = (uint)(count * alignedSize),
                sharingMode = VkSharingMode.Exclusive
            };

            buffer = engine.Memory.AllocUniform(info);
        }

        void WriteData() {

        }

        public void Update() {
            if (count < data.Count) {
                count = data.Count;
                CreateBuffer();
            }

            WriteData();
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposing) {
            if (disposed) return;

            disposed = true;
        }

        ~UniformBuffer() {
            Dispose(false);
        }
    }
}
