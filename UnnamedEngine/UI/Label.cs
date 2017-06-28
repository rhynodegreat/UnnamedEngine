using System;
using System.Collections.Generic;
using System.Numerics;

using CSGL;
using CSGL.Graphics;
using CSGL.Vulkan1;

using UnnamedEngine.Core;
using UnnamedEngine.Resources;
using UnnamedEngine.UI.Text;

namespace UnnamedEngine.UI {
    public class Label : UIElement {
        Engine engine;
        bool disposed;

        string text = "";
        bool dirty;
        GlyphCache cache;

        public string Text {
            get {
                return text;
            }
            set {
                if (value == null) throw new ArgumentNullException(nameof(Text));
                
                int oldHash = text.GetHashCode();
                int newHash = value.GetHashCode();

                if (oldHash != newHash && text != value) {
                    dirty = true;
                }

                text = value;
            }
        }

        public Font Font { get; set; }
        public float FontSize { get; set; }
        public float Outline { get; set; }
        public float Thickness { get; set; }
        public Color4 Color { get; set; }
        public Color4 OutlineColor { get; set; }

        internal Mesh Mesh { get; private set; }

        public Label(Engine engine, GlyphCache cache) {
            if (engine == null) throw new ArgumentNullException(nameof(engine));
            if (cache == null) throw new ArgumentNullException(nameof(cache));

            this.engine = engine;
            this.cache = cache;
        }

        internal void Update() {
            if (!dirty) return;
            if (string.IsNullOrEmpty(text)) return;

            if (Mesh == null) {
                VertexData<LabelVertex> vertexData = new VertexData<LabelVertex>(engine);
                Mesh = new Mesh(engine, vertexData, null);
            }

            cache.AddString(Font, text);
            List<LabelVertex> verts = new List<LabelVertex>();
            float height = Font.Height * cache.Scale * FontSize;
            Vector3 pos = new Vector3(0, height, 0);

            foreach (char c in text) {
                Emit(Font, c, verts, FontSize, ref pos);
            }

            ((VertexData<LabelVertex>)Mesh.VertexData).SetData(verts);
            Mesh.Apply();

            dirty = false;
        }

        void Emit(Font font, int codepoint, List<LabelVertex> verts, float scale, ref Vector3 pos) {
            //screen has y axis going down, glyph metric has y axis going up
            var metrics = cache.GetInfo(font, codepoint);
            var offset = metrics.offset * scale;
            var size = metrics.size * scale;
            size.Y *= -1;

            var v1 = new LabelVertex(new Vector3(0, size.Y, 0) + pos + offset, metrics.uvPosition);
            var v2 = new LabelVertex(size + pos + offset, metrics.uvPosition + new Vector3(metrics.size.X, 0, 0));
            var v3 = new LabelVertex(pos + offset, metrics.uvPosition + new Vector3(0, metrics.size.Y, 0));
            var v4 = new LabelVertex(new Vector3(size.X, 0, 0) + pos + offset, metrics.uvPosition + metrics.size);

            verts.Add(v1);
            verts.Add(v2);
            verts.Add(v3);
            verts.Add(v2);
            verts.Add(v4);
            verts.Add(v3);

            pos += new Vector3(metrics.advance * scale, 0, 0);
        }

        public override void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposing) {
            if (disposed) return;

            Mesh.Dispose();

            disposed = true;
        }

        ~Label() {
            Dispose(false);
        }
    }

    public struct LabelVertex {
        public Vector3 position;
        public Vector3 uv;

        public LabelVertex(Vector3 position, Vector3 uv) {
            this.position = position;
            this.uv = uv;
        }

        public static VkVertexInputBindingDescription GetBindingDescription() {
            var result = new VkVertexInputBindingDescription();
            result.binding = 0;
            result.stride = (uint)Interop.SizeOf<LabelVertex>();
            result.inputRate = VkVertexInputRate.Vertex;

            return result;
        }

        public static List<VkVertexInputAttributeDescription> GetAttributeDescriptions() {
            LabelVertex v = new LabelVertex();
            var a = new VkVertexInputAttributeDescription();
            a.binding = 0;
            a.location = 0;
            a.format = VkFormat.R32g32b32Sfloat;
            a.offset = (uint)Interop.Offset(ref v, ref v.position);

            var b = new VkVertexInputAttributeDescription();
            b.binding = 0;
            b.location = 1;
            b.format = VkFormat.R32g32b32Sfloat;
            b.offset = (uint)Interop.Offset(ref v, ref v.uv);

            return new List<VkVertexInputAttributeDescription> { a, b };
        }
    }
}
