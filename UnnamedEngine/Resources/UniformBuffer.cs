﻿using System;
using System.Collections.Generic;

using CSGL;
using CSGL.Vulkan1;
using Buffer = CSGL.Vulkan1.Buffer;

using UnnamedEngine.Core;
using UnnamedEngine.Memory;

namespace UnnamedEngine.Resources {
    public class UniformBuffer<T> : IDisposable where T : struct {
        bool disposed;

        Engine engine;

        Page page;
        DescriptorSet set;
        WriteDescriptorSet write;

        public int AlignedSize { get; private set; }
        VkBufferUsageFlags usage;
        public Buffer Buffer { get; private set; }
        public int Count { get; private set; }
        int capacity;
        List<T> data;

        public UniformBuffer(Engine engine, int count, VkBufferUsageFlags usage, DescriptorSet set, WriteDescriptorSet write) {
            if (engine == null) throw new ArgumentNullException(nameof(engine));
            if (count < 1) throw new ArgumentOutOfRangeException(nameof(count));

            this.engine = engine;
            this.set = set;
            this.write = write;

            data = new List<T>(count);
            Count = count;
            capacity = data.Capacity;
            this.usage = usage;

            int alignment = (int)engine.Graphics.PhysicalDevice.Properties.Limits.minUniformBufferOffsetAlignment;
            AlignedSize = 0;
            while (AlignedSize < Interop.SizeOf<T>()) AlignedSize += alignment;

            CreateBuffer();
        }

        void CreateBuffer() {
            engine.Memory.FreeUniform(Buffer);

            BufferCreateInfo info = new BufferCreateInfo() {
                usage = usage | VkBufferUsageFlags.UniformBufferBit,
                size = (uint)(Count * AlignedSize),
                sharingMode = VkSharingMode.Exclusive
            };

            Buffer = engine.Memory.AllocUniform(info);
            page = engine.Memory.GetUniformPage(Buffer.Memory);

            write.bufferInfo = new List<DescriptorBufferInfo> {
                new DescriptorBufferInfo {
                    buffer = Buffer,
                    offset = 0,
                    range = (uint)Interop.SizeOf<T>()
                }
            };

            set.Update(new List<WriteDescriptorSet> { write });
        }

        void WriteData() {
            for (int i = 0; i < Count; i++) {
                IntPtr ptr = page.Mapping + (int)Buffer.Offset + i * AlignedSize;
                Interop.Copy(data[i], ptr);
            }
        }

        public void Update() {
            Count = data.Count;
            if (capacity < data.Capacity) {
                capacity = data.Capacity;
                CreateBuffer();
            }

            WriteData();
        }

        public T this[int i] {
            get {
                return data[i];
            }
            set {
                data[i] = value;
            }
        }

        public void Add() {
            data.Add(default(T));
        }

        public void Remove() {
            data.RemoveAt(data.Count - 1);
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposing) {
            if (disposed) return;

            engine.Memory.FreeUniform(Buffer);

            disposed = true;
        }

        ~UniformBuffer() {
            Dispose(false);
        }
    }
}