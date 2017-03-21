using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

using CSGL;
using CSGL.Vulkan;

using UnnamedEngine.Core;

namespace UnnamedEngine.Resources {
    public class VertexData<T> : VertexData where T : struct {
        static List<VkVertexInputBindingDescription> bindings;
        static List<VkVertexInputAttributeDescription> attributes;

        static VertexData() {
            bindings = new List<VkVertexInputBindingDescription> {
                new VkVertexInputBindingDescription {
                    binding = 0,
                    stride = (uint)Interop.SizeOf<T>(),
                    inputRate = VkVertexInputRate.Vertex
                }
            };

            attributes = new List<VkVertexInputAttributeDescription>();

            FieldInfo[] fields = typeof(T).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            int size = 0;

            for (int i = 0; i < fields.Length; i++) {
                int attribSize;
                attributes.Add(new VkVertexInputAttributeDescription {
                    binding = 0,
                    location = (uint)i,
                    format = GetFormat(fields[i].FieldType, i, out attribSize),
                    offset = (uint)size
                });
                size += attribSize;
            }
        }

        public VertexData(Engine engine) : base(engine, bindings, attributes) {

        }

        public VertexData(Engine engine, Stream stream) : base(engine, stream) {
            if (bindings.Count != Bindings.Count || attributes.Count != Attributes.Count) throw new VertexDataException($"Stream data does not match {typeof(T).Name}");

            for (int i = 0; i < bindings.Count; i++) {
                if (bindings[i].stride != Bindings[i].stride
                    || bindings[i].inputRate != Bindings[i].inputRate) {
                    throw new VertexDataException($"Stream data does not match {typeof(T).Name}");
                }
            }

            for (int i = 0; i < attributes.Count; i++) {
                if (attributes[i].format != Attributes[i].format
                    || attributes[i].location != Attributes[i].location
                    || attributes[i].offset != Attributes[i].offset) {
                    throw new VertexDataException($"Stream data does not match {typeof(T).Name}");
                }
            }
        }

        protected override void ReadVertices(BinaryReader reader) {
            uint vertexSize = reader.ReadUInt32();
            if (vertexSize != Bindings[0].stride) throw new VertexDataException(string.Format("Vertex size ({0}) does not match binding stride ({1})", vertexSize, Bindings[0].stride));

            uint vertexCount = reader.ReadUInt32();
            VertexCount = (int)vertexCount;
            Size = (int)(vertexCount * vertexSize);
            byte[] data = reader.ReadBytes(Size);

            T[] array = new T[VertexCount];
            Interop.Copy(data, array);
            InternalData = array;
        }

        public void SetData(T[] array, int count) {
            T[] vertexData = new T[count];
            Interop.Copy(array, vertexData);
            VertexCount = count;
            InternalData = vertexData;
            Size = (int)Interop.SizeOf(vertexData);
        }

        public void SetData(List<T> list) {
            T[] array = Interop.GetInternalArray(list);
            SetData(array, list.Count);
        }

        static VkFormat GetFormat(Type t, int index, out int size) {
            if (t.IsPrimitive) return GetFormat(1, GetAttribute(t), index, out size);   //check if field is just a single primitive

            FieldInfo[] fields = t.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (fields.Length == 0) throw new VertexDataException($"Unsupported vertex format: {t.Name} has no fields ({typeof(T).Name})");

            VertexAttribute attrib = GetAttribute(fields[0].FieldType);
            for (int i = 1; i < fields.Length; i++) {
                VertexAttribute a = GetAttribute(fields[i].FieldType);
                if (a != attrib) throw new VertexDataException($"Unsupported vertex format: all members of each attribute must be the same type (attribute {i} of {typeof(T).Name}");
            }

            return GetFormat(fields.Length, attrib, index, out size);
        }

        static VertexAttribute GetAttribute(Type t) {
            if (t == typeof(byte)) return VertexAttribute.Uint8;
            if (t == typeof(ushort)) return VertexAttribute.Uint16;
            if (t == typeof(uint)) return VertexAttribute.Uint32;
            if (t == typeof(ulong)) return VertexAttribute.Uint64;
            if (t == typeof(sbyte)) return VertexAttribute.Int8;
            if (t == typeof(short)) return VertexAttribute.Int16;
            if (t == typeof(int)) return VertexAttribute.Int32;
            if (t == typeof(long)) return VertexAttribute.Int64;
            if (t == typeof(float)) return VertexAttribute.Float32;
            if (t == typeof(double)) return VertexAttribute.Float64;

            return VertexAttribute.Undefined;
        }
    }
}
