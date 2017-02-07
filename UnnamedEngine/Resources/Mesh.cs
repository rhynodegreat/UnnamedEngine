using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using CSGL;
using CSGL.Vulkan;
using Buffer = CSGL.Vulkan.Buffer;

using UnnamedEngine.Core;
using UnnamedEngine.Utilities;

namespace UnnamedEngine.Resources {
    public class Mesh : IDisposable {
        bool disposed;

        Engine engine;
        TransferNode transferNode;

        VkaAllocation vertexAllocation;
        VkaAllocation indexAllocation;

        public Buffer VertexBuffer { get; private set; }
        public Buffer IndexBuffer { get; private set; }
        int lastVertexSize;
        int lastIndexSize;

        VertexData vertexData;
        public VertexData VertexData {  //vertex data can't be null
            get {
                return vertexData;
            }
            set {
                if (value == null) throw new ArgumentNullException(nameof(VertexData));
                vertexData = value;
            }
        }

        public IndexData IndexData { get; set; }    //index data can be null

        public Mesh(Engine engine, VertexData vertexData, IndexData indexData) {
            if (engine == null) throw new ArgumentNullException(nameof(engine));
            if (vertexData == null) throw new ArgumentNullException(nameof(vertexData));

            this.engine = engine;

            VertexData = vertexData;
            IndexData = indexData;
        }

        public Mesh(Engine engine, Stream stream) {
            if (engine == null) throw new ArgumentNullException(nameof(engine));

            this.engine = engine;

            ReadStream(stream, null, null);

            transferNode = engine.Graphics.TransferNode;
            Apply();
        }

        public Mesh(Engine engine, Stream stream, List<VkVertexInputBindingDescription> bindings, List<VkVertexInputAttributeDescription> attributes) {
            if (engine == null) throw new ArgumentNullException(nameof(engine));

            this.engine = engine;

            ReadStream(stream, bindings, attributes);

            transferNode = engine.Graphics.TransferNode;
            Apply();
        }

        void ReadStream(Stream stream, List<VkVertexInputBindingDescription> bindings, List<VkVertexInputAttributeDescription> attributes) {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            long startPos = stream.Position;

            using (var reader = new BinaryReader(stream, Encoding.UTF8, true)) {
                byte[] header = reader.ReadBytes(5);
                if (!(header[0] == 'M' && header[1] == 'e' && header[2] == 's' && header[3] == 'h' && header[4] == 0)) {
                    string format = Interop.GetString(header);
                    throw new MeshException(string.Format("Invalid format \"{0}\"", format));
                }

                byte major = reader.ReadByte();
                byte minor = reader.ReadByte();
                byte patch = reader.ReadByte();

                if (!(major == 1 && minor == 0 && patch == 0)) {
                    throw new MeshException(string.Format("Unsupported version \"{0}.{1}.{3}\"", major, minor, patch));
                }

                ulong attributesOffset = reader.ReadUInt64();
                ulong vertexOffset = reader.ReadUInt64();
                ulong indexOffset = reader.ReadUInt64();

                if (vertexOffset == 0) throw new MeshException("Mesh does not have a vertex section");

                if (attributesOffset == 0) {
                    stream.Position = (long)vertexOffset;
                    VertexData = new OpaqueVertexData(stream, bindings, attributes);
                } else {
                    VertexData = new OpaqueVertexData(stream);
                }

                if (indexOffset != 0) {
                    IndexData = new IndexData(stream);
                }
            }
        }

        public void Apply() {
            if (VertexData.Size > lastVertexSize) {
                VertexBuffer?.Dispose();
                engine.Graphics.Allocator.Free(vertexAllocation);

                BufferCreateInfo vertexInfo = new BufferCreateInfo();
                vertexInfo.usage = VkBufferUsageFlags.VertexBufferBit | VkBufferUsageFlags.TransferDstBit;
                vertexInfo.size = (ulong)VertexData.Size;
                vertexInfo.sharingMode = VkSharingMode.Exclusive;

                VertexBuffer = new Buffer(engine.Graphics.Device, vertexInfo);

                vertexAllocation = engine.Graphics.Allocator.Alloc(VertexBuffer.Requirements, VkMemoryPropertyFlags.DeviceLocalBit);
                VertexBuffer.Bind(vertexAllocation.memory, vertexAllocation.offset);

                lastVertexSize = VertexData.Size;
            }

            GCHandle vertexHandle = GCHandle.Alloc(VertexData.InternalData, GCHandleType.Pinned);
            transferNode.Transfer(vertexHandle.AddrOfPinnedObject(), (uint)VertexData.Size, VertexBuffer);
            vertexHandle.Free();

            if (IndexData != null) {
                if (IndexData.Size > lastIndexSize) {
                    IndexBuffer?.Dispose();
                    engine.Graphics.Allocator.Free(indexAllocation);

                    BufferCreateInfo indexInfo = new BufferCreateInfo();
                    indexInfo.usage = VkBufferUsageFlags.IndexBufferBit | VkBufferUsageFlags.TransferDstBit;
                    indexInfo.size = (ulong)IndexData.Size;
                    indexInfo.sharingMode = VkSharingMode.Exclusive;

                    IndexBuffer = new Buffer(engine.Graphics.Device, indexInfo);

                    indexAllocation = engine.Graphics.Allocator.Alloc(IndexBuffer.Requirements, VkMemoryPropertyFlags.DeviceLocalBit);
                    IndexBuffer.Bind(indexAllocation.memory, indexAllocation.offset);

                    lastIndexSize = IndexData.Size;
                }

                GCHandle indexHandle = GCHandle.Alloc(IndexData.InternalData, GCHandleType.Pinned);
                transferNode.Transfer(indexHandle.AddrOfPinnedObject(), (uint)IndexData.Size, IndexBuffer);
                indexHandle.Free();
            } else {
                IndexBuffer?.Dispose();
                IndexBuffer = null;
                engine.Graphics.Allocator.Free(indexAllocation);
                indexAllocation = new VkaAllocation();
            }
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposing) {
            if (disposed) return;

            VertexBuffer?.Dispose();
            IndexBuffer?.Dispose();
            engine.Graphics.Allocator.Free(vertexAllocation);
            engine.Graphics.Allocator.Free(indexAllocation);

            disposed = true;
        }

        ~Mesh() {
            Dispose(false);
        }
    }

    public class MeshException : Exception {
        public MeshException(string message) : base(message) { }
    }
}
