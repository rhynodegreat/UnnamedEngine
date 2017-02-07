using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

using CSGL;
using CSGL.Vulkan;

namespace UnnamedEngine.Resources {
    public enum VertexAttribute {
        Undefined = 0,
        Uint8 = 1,
        Uint16 = 2,
        Uint32 = 3,
        Uint64 = 4,
        Int8 = 5,
        Int16 = 6,
        Int32 = 7,
        Int64 = 8,
        Float32 = 9,
        Float64 = 10,
    }

    public abstract class VertexData {
        bool disposed;

        List<VkVertexInputBindingDescription> bindings;
        public List<VkVertexInputBindingDescription> Bindings {
            get {
                return bindings;
            }
            set {
                if (value == null) throw new ArgumentNullException(nameof(Bindings));
                bindings = value;
            }
        }

        List<VkVertexInputAttributeDescription> attributes;
        public List<VkVertexInputAttributeDescription> Attributes {
            get {
                return attributes;
            }
            set {
                if (value == null) throw new ArgumentNullException(nameof(Attributes));
                attributes = value;
            }
        }

        internal protected object InternalData { get; set; }

        public int VertexCount { get; protected set; }
        public int Size { get; protected set; }

        protected VertexData(List<VkVertexInputBindingDescription> bindings, List<VkVertexInputAttributeDescription> attributes) {
            if (bindings == null) throw new ArgumentNullException(nameof(bindings));
            if (attributes == null) throw new ArgumentNullException(nameof(attributes));

            Bindings = bindings;
            Attributes = Attributes;
        }

        protected VertexData(Stream stream) {
            using (var reader = new BinaryReader(stream, Encoding.UTF8, true)) {
                Bindings = new List<VkVertexInputBindingDescription>();
                Attributes = new List<VkVertexInputAttributeDescription>();
                ReadAttributes(reader);
                ReadVertices(reader);
            }
        }

        protected VertexData(Stream stream, List<VkVertexInputBindingDescription> bindings, List<VkVertexInputAttributeDescription> attributes) : this(bindings, attributes) {
            using (var reader = new BinaryReader(stream, Encoding.UTF8, true)) {
                ReadVertices(reader);
            }
        }

        protected void ReadAttributes(BinaryReader reader) {
            byte formatLength = reader.ReadByte();
            if (formatLength > 0) {
                reader.ReadBytes(formatLength); //skip format
            }

            byte attributeCount = reader.ReadByte();

            int totalOffset = 0;

            for (int i = 0; i < attributeCount; i++) {
                byte attributeNameLength = reader.ReadByte();
                if (attributeNameLength > 0) {
                    reader.ReadBytes(attributeNameLength);  //skip attribute name
                }

                byte componentCount = reader.ReadByte();
                byte attributeType = reader.ReadByte();

                int size;
                VkFormat format = GetFormat(componentCount, (VertexAttribute)attributeType, i, out size);

                attributes.Add(new VkVertexInputAttributeDescription {
                    binding = 0,
                    format = format,
                    location = (uint)i,
                    offset = (uint)totalOffset
                });

                totalOffset += size;
            }

            bindings.Add(new VkVertexInputBindingDescription {
                binding = 0,
                inputRate = VkVertexInputRate.Vertex,
                stride = (uint)totalOffset
            });
        }

        protected abstract void ReadVertices(BinaryReader reader);

        VkFormat GetFormat(int components, VertexAttribute type, int index, out int size) {
            switch (type) {
                case VertexAttribute.Uint8:
                    size = components;
                    if (components == 1) {
                        return VkFormat.R8Uint;
                    } else if (components == 2) {
                        return VkFormat.R8g8Uint;
                    } else if (components == 3) {
                        return VkFormat.R8g8b8Uint;
                    } else if (components == 4) {
                        return VkFormat.R8g8b8a8Uint;
                    }
                    break;
                case VertexAttribute.Uint16:
                    size = components * 2;
                    if (components == 1) {
                        return VkFormat.R16Uint;
                    } else if (components == 2) {
                        return VkFormat.R16g16Uint;
                    } else if (components == 3) {
                        return VkFormat.R16g16b16Uint;
                    } else if (components == 4) {
                        return VkFormat.R16g16b16a16Uint;
                    }
                    break;
                case VertexAttribute.Uint32:
                    size = components * 4;
                    if (components == 1) {
                        return VkFormat.R32Uint;
                    } else if (components == 2) {
                        return VkFormat.R32g32Uint;
                    } else if (components == 3) {
                        return VkFormat.R32g32b32Uint;
                    } else if (components == 4) {
                        return VkFormat.R32g32b32a32Uint;
                    }
                    break;
                case VertexAttribute.Uint64:
                    size = components * 8;
                    if (components == 1) {
                        return VkFormat.R64Uint;
                    } else if (components == 2) {
                        return VkFormat.R64g64Uint;
                    } else if (components == 3) {
                        return VkFormat.R64g64b64Uint;
                    } else if (components == 4) {
                        return VkFormat.R64g64b64a64Uint;
                    }
                    break;
                case VertexAttribute.Int8:
                    size = components;
                    if (components == 1) {
                        return VkFormat.R8Sint;
                    } else if (components == 2) {
                        return VkFormat.R8g8Sint;
                    } else if (components == 3) {
                        return VkFormat.R8g8b8Uint;
                    } else if (components == 4) {
                        return VkFormat.R8g8b8a8Uint;
                    }
                    break;
                case VertexAttribute.Int16:
                    size = components * 2;
                    if (components == 1) {
                        return VkFormat.R16Sint;
                    } else if (components == 2) {
                        return VkFormat.R16g16Sint;
                    } else if (components == 3) {
                        return VkFormat.R16g16b16Sint;
                    } else if (components == 4) {
                        return VkFormat.R16g16b16a16Sint;
                    }
                    break;
                case VertexAttribute.Int32:
                    size = components * 4;
                    if (components == 1) {
                        return VkFormat.R32Sint;
                    } else if (components == 2) {
                        return VkFormat.R32g32Sint;
                    } else if (components == 3) {
                        return VkFormat.R32g32b32Sint;
                    } else if (components == 4) {
                        return VkFormat.R32g32b32a32Sint;
                    }
                    break;
                case VertexAttribute.Int64:
                    size = components * 8;
                    if (components == 1) {
                        return VkFormat.R64Sint;
                    } else if (components == 2) {
                        return VkFormat.R64g64Sint;
                    } else if (components == 3) {
                        return VkFormat.R64g64b64Sint;
                    } else if (components == 4) {
                        return VkFormat.R64g64b64a64Sint;
                    }
                    break;
                case VertexAttribute.Float32:
                    size = components * 4;
                    if (components == 1) {
                        return VkFormat.R32Sfloat;
                    } else if (components == 2) {
                        return VkFormat.R32g32Sfloat;
                    } else if (components == 3) {
                        return VkFormat.R32g32b32Sfloat;
                    } else if (components == 4) {
                        return VkFormat.R32g32b32a32Sfloat;
                    }
                    break;
                case VertexAttribute.Float64:
                    size = components * 8;
                    if (components == 1) {
                        return VkFormat.R64Sfloat;
                    } else if (components == 2) {
                        return VkFormat.R64g64Sfloat;
                    } else if (components == 3) {
                        return VkFormat.R64g64b64Sfloat;
                    } else if (components == 4) {
                        return VkFormat.R64g64b64a64Sfloat;
                    }
                    break;
            }
            throw new VertexDataException(string.Format("Invalid attribute type of {0} at attribute {1}", type, index));          
        }
    }

    public class VertexDataException : Exception {
        public VertexDataException(string message) : base(message) { }
    }
}
