using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

using CSGL;
using CSGL.Vulkan;

namespace UnnamedEngine.Resources {
    public class Mesh : IDisposable {
        bool disposed;

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

        public Mesh(VertexData vertexData, IndexData indexData) {
            if (vertexData == null) throw new ArgumentNullException(nameof(vertexData));

            VertexData = vertexData;
            IndexData = indexData;
        }

        public Mesh(Stream stream) {
            ReadStream(stream, null, null);
        }

        public Mesh(Stream stream, List<VkVertexInputBindingDescription> bindings, List<VkVertexInputAttributeDescription> attributes) {
            ReadStream(stream, bindings, attributes);
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
                    VertexData = new VertexData(stream, bindings, attributes);
                } else {
                    VertexData = new VertexData(stream);
                }

                if (indexOffset != 0) {
                    IndexData = new IndexData(stream);
                }
            }
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposing) {
            if (disposed) return;

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
