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

        VkaAllocation vertexAllocation;
        VkaAllocation indexAllocation;

        public Buffer VertexBuffer { get; private set; }
        public Buffer IndexBuffer { get; private set; }

        TransferNode transferNode;
        public TransferNode TransferNode {
            get {
                return transferNode;
            }
            set {
                if (value == null) throw new ArgumentNullException(nameof(TransferNode));
                transferNode = value;
            }
        }

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

            TransferNode = engine.Graphics.TransferNode;
            Apply();
        }

        public Mesh(Engine engine, Stream stream, List<VkVertexInputBindingDescription> bindings, List<VkVertexInputAttributeDescription> attributes) {
            if (engine == null) throw new ArgumentNullException(nameof(engine));

            this.engine = engine;

            ReadStream(stream, bindings, attributes);

            TransferNode = engine.Graphics.TransferNode;
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

        void Free() {
            VertexBuffer?.Dispose();
            IndexBuffer?.Dispose();
            engine.Graphics.Allocator.Free(vertexAllocation);
            engine.Graphics.Allocator.Free(indexAllocation);

            VertexBuffer = null;
            IndexBuffer = null;
            vertexAllocation = new VkaAllocation();
            indexAllocation = new VkaAllocation();
        }

        public void Apply() {
            Free();

            BufferCreateInfo vertexInfo = new BufferCreateInfo();
            vertexInfo.usage = VkBufferUsageFlags.VertexBufferBit | VkBufferUsageFlags.TransferDstBit;
            vertexInfo.size = (ulong)VertexData.Size;
            vertexInfo.sharingMode = VkSharingMode.Exclusive;
            
            VertexBuffer = new Buffer(engine.Graphics.Device, vertexInfo);

            vertexAllocation = engine.Graphics.Allocator.Alloc(VertexBuffer.Requirements, VkMemoryPropertyFlags.DeviceLocalBit);
            VertexBuffer.Bind(vertexAllocation.memory, vertexAllocation.offset);

            GCHandle handle = GCHandle.Alloc(VertexData.InternalData, GCHandleType.Pinned);
            TransferNode.Transfer(handle.AddrOfPinnedObject(), (uint)IndexData.Size, VertexBuffer);
            handle.Free();

            if (IndexData != null) {
                BufferCreateInfo indexInfo = new BufferCreateInfo();
                indexInfo.usage = VkBufferUsageFlags.IndexBufferBit | VkBufferUsageFlags.TransferDstBit;
                indexInfo.size = (ulong)IndexData.Size;
                indexInfo.sharingMode = VkSharingMode.Exclusive;

                IndexBuffer = new Buffer(engine.Graphics.Device, indexInfo);

                indexAllocation = engine.Graphics.Allocator.Alloc(IndexBuffer.Requirements, VkMemoryPropertyFlags.DeviceLocalBit);
                IndexBuffer.Bind(indexAllocation.memory, indexAllocation.offset);

                if (IndexData.IndexType == VkIndexType.Uint16) {
                    TransferNode.Transfer(IndexData.Data16, IndexBuffer);
                }
                else {
                    TransferNode.Transfer(IndexData.Data32, IndexBuffer);
                }
            }
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposing) {
            if (disposed) return;

            Free();

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
