using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ShadedTechnology.MeshPainter
{
    class ColorMask
    {
        public bool isR = true;
        public bool isG = true;
        public bool isB = true;
        public bool isA = true;

        public Color FilterColor(Color original, Color newColor)
        {
            return new Color(isR ? newColor.r : original.r,
                             isG ? newColor.g : original.g,
                             isB ? newColor.b : original.b,
                             isA ? newColor.a : original.a);
        }
    }

    class VertexColors : IEnumerable
    {
        private List<Color> colors;
        private Dictionary<Vector3, List<uint>> realVerts;
        public VertexColors(Mesh mesh)
        {
            colors = new List<Color>(mesh.vertexCount);
            mesh.GetColors(colors);
            realVerts = new Dictionary<Vector3, List<uint>>();
            for (uint i = 0; i < mesh.vertexCount; ++i)
            {
                if (i >= colors.Count)
                {
                    colors.Add(Color.white);
                }
                Vector3 currentVec = mesh.vertices[i];
                if (realVerts.ContainsKey(currentVec))
                {
                    realVerts[currentVec].Add(i);
                }
                else
                {
                    realVerts.Add(currentVec, new List<uint>() { i });
                }
            }
        }

        public List<Color> GetColors()
        {
            return colors;
        }

        public IEnumerator GetEnumerator()
        {
            return new VertexColor(this);
        }

        public class VertexColor : IEnumerator
        {
            private VertexColors vertexColors;
            private IEnumerator<KeyValuePair<Vector3, List<uint>>> iterator;

            public VertexColor(VertexColors vertexColors)
            {
                this.vertexColors = vertexColors;
                iterator = vertexColors.realVerts.GetEnumerator();
            }

            public Color GetColor()
            {
                if (iterator.Current.Value.Count <= 0) return Color.white;
                return vertexColors.colors[(int)iterator.Current.Value[0]];
            }

            public void SetColor(Color color)
            {
                foreach (int i in iterator.Current.Value)
                {
                    vertexColors.colors[i] = color;
                }
            }

            public Vector3 GetLocalPosition()
            {
                return iterator.Current.Key;
            }

            public Vector3 GetPosition(Matrix4x4 objectToWorld)
            {
                return objectToWorld.MultiplyPoint3x4(iterator.Current.Key);
            }

            public object Current
            {
                get
                {
                    return this;
                }
            }

            public bool MoveNext()
            {
                return iterator.MoveNext();
            }

            public void Reset()
            {
                iterator.Reset();
            }
        }
    }

    static class VertexColorsWithColorMask
    {
        public static void SetColorWithMask(this VertexColors.VertexColor vertexColor, ColorMask colorMask, Color color)
        {
            vertexColor.SetColor(colorMask.FilterColor(vertexColor.GetColor(), color));
        }
    }
}
