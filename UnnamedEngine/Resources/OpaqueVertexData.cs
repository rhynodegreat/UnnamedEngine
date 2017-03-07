using System;
using System.IO;
using System.Collections.Generic;

using CSGL.Vulkan;

using UnnamedEngine.Core;

namespace UnnamedEngine.Resources {
    public class OpaqueVertexData : VertexData {
        byte[] data;
        public byte[] Data {
            get {
                return data;
            }
            set {
                if (value == null) throw new ArgumentNullException(nameof(Data));
                data = value;
                InternalData = value;
            }
        }

        public OpaqueVertexData(Engine engine, List<VkVertexInputBindingDescription> bindings, List<VkVertexInputAttributeDescription> attributes, byte[] data) : base(engine, bindings, attributes) {
            if (data == null) throw new ArgumentNullException(nameof(data));

            Data = data;
        }

        public OpaqueVertexData(Engine engine, Stream stream) : base(engine, stream) { }
        public OpaqueVertexData(Engine engine, Stream stream, List<VkVertexInputBindingDescription> bindings, List<VkVertexInputAttributeDescription> attributes) : base(engine, stream, bindings, attributes) { }

        protected override void ReadVertices(BinaryReader reader) {
            uint vertexSize = reader.ReadUInt32();
            if (vertexSize != Bindings[0].stride) throw new VertexDataException(string.Format("Vertex size ({0}) does not match binding stride ({1})", vertexSize, Bindings[0].stride));

            uint vertexCount = reader.ReadUInt32();
            VertexCount = (int)vertexCount;
            Size = (int)(vertexCount * vertexSize);
            Data = reader.ReadBytes(Size);
        }
    }
}
