using System;
using System.IO;
using System.Collections.Generic;

using CSGL.Vulkan;

namespace UnnamedEngine.Resources {
    public class IndexData : IDisposable {
        bool disposed;

        public VkIndexType IndexType { get; private set; }

        public object Data { get; private set; }

        public uint[] Data32 {
            get {
                return (uint[])Data;    //we want this getter to fail at the callsite if the wrong type is called
            }
            set {
                if (value == null) throw new ArgumentNullException(nameof(Data32));
                Data = value;
                IndexType = VkIndexType.Uint32;
            }
        }

        public ushort[] Data16 {
            get {
                return (ushort[])Data;  //we want this getter to fail at the callsite if the wrong type is called
            }
            set {
                if (value == null) throw new ArgumentNullException(nameof(Data16));
                Data = value;
                IndexType = VkIndexType.Uint16;
            }
        }

        public IndexData(uint[] indices) {
            Data32 = indices;
        }

        public IndexData(ushort[] indices) {
            Data16 = indices;
        }

        public IndexData(Stream stream) {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            using (var reader = new BinaryReader(stream)) {
                Seek(reader);
                Read(reader);
            }
        }

        void Seek(BinaryReader reader) {
            //check if constructor was called at start of mesh data stream, or at a preset location
            //also accounts for if mesh data stream starts partway into file

            long startPos = reader.BaseStream.Position;

            byte[] header = reader.ReadBytes(5);
            if (!(header[0] == 'M' && header[1] == 'e' && header[2] == 's' && header[3] == 'h' && header[5] == 0)) {
                reader.ReadByte();  //skip version
                reader.ReadByte();
                reader.ReadByte();

                reader.ReadUInt64();    //skip other sections
                reader.ReadUInt64();
                ulong indexOffset = reader.ReadUInt64();

                if (indexOffset == 0) throw new IndexDataException("Mesh does not have an index section");

                reader.BaseStream.Position = startPos + (long)indexOffset;
            }
        }

        void Read(BinaryReader reader) {
            byte indexType = reader.ReadByte();
            uint indexCount = reader.ReadUInt32();
            if ((VertexAttribute)indexType == VertexAttribute.Uint16) {
                Read16(reader, indexCount);
            } else if ((VertexAttribute)indexType == VertexAttribute.Uint32) {
                Read32(reader, indexCount);
            } else {
                throw new IndexDataException(string.Format("Index type of {0} is not supported", (VertexAttribute)indexType));
            }
        }

        void Read32(BinaryReader reader, uint indexCount) {
            uint[] indices = new uint[indexCount];

            for (uint i = 0; i < indexCount; i++) {
                indices[i] = reader.ReadUInt32();
            }

            Data32 = indices;
        }

        void Read16(BinaryReader reader, uint indexCount) {
            ushort[] indices = new ushort[indexCount];

            for (uint i = 0; i < indexCount; i++) {
                indices[i] = reader.ReadUInt16();
            }

            Data16 = indices;
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposing) {
            if (disposed) return;

            disposed = true;
        }

        ~IndexData() {
            Dispose(false);
        }
    }

    public class IndexDataException : Exception {
        public IndexDataException(string message) : base(message) { }
    }
}
