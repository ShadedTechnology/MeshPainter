using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;

namespace ShadedTechnology.MeshPainter
{
    using VertexColor = VertexColors.VertexColor;

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
            private IEnumerator<KeyValuePair<Vector3, List<uint>>> current;

            public VertexColor(VertexColors vertexColors)
            {
                this.vertexColors = vertexColors;
                current = vertexColors.realVerts.GetEnumerator();
            }

            public Color GetColor()
            {
                if (current.Current.Value.Count <= 0) return Color.white;
                return vertexColors.colors[(int)current.Current.Value[0]];
            }

            public void SetColor(Color color)
            {
                foreach (int i in current.Current.Value)
                {
                    vertexColors.colors[i] = color;
                }
            }

            public Vector3 GetPosition(Matrix4x4 objectToWorld)
            {
                return objectToWorld.MultiplyPoint3x4(current.Current.Key);
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
                return current.MoveNext();
            }

            public void Reset()
            {
                current.Reset();
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

    public class EditorMeshPainter : EditorWindow
    {
        [MenuItem("Tools/MeshPainter")]
        public static void ShowWindow()
        {
            GetWindow<EditorMeshPainter>("MeshPainter");
        }

        float m_dotSize = 0.05f;
        MeshFilter m_meshFilter;
        VertexColors m_vertexColors;
        Mesh m_currentMesh;
        ColorMask m_colorMask = new ColorMask();
        Color m_paintColor;

        const float minVisualAlpa = 0.3f;

        private void SaveMesh()
        {
            string path = EditorUtility.SaveFilePanelInProject("Choose Location for Mesh to save", m_currentMesh.name, "asset", "Save your mesh");
            AssetDatabase.CreateAsset(m_currentMesh, path);
            AssetDatabase.Refresh();
            m_meshFilter.mesh = AssetDatabase.LoadAssetAtPath(path, typeof(Mesh)) as Mesh;
        }

        // Window has been selected
        void OnFocus()
        {
            // Remove delegate listener if it has previously
            // been assigned.
            SceneView.onSceneGUIDelegate -= this.OnSceneGUI;
            // Add (or re-add) the delegate.
            SceneView.onSceneGUIDelegate += this.OnSceneGUI;

        }

        void OnDestroy()
        {
            // When the window is destroyed, remove the delegate
            // so that it will no longer do any drawing.
            SceneView.onSceneGUIDelegate -= this.OnSceneGUI;
            ClearMem();
        }

        void OnSceneGUI(SceneView sceneView)
        {
            if (IsMeshSet())
            {
                foreach (VertexColor vertexColor in m_vertexColors)
                {
                    Vector3 pos = vertexColor.GetPosition(m_meshFilter.transform.localToWorldMatrix);
                    Color col = vertexColor.GetColor();
                    Handles.color = new Color(col.r, col.g, col.b, Mathf.Max(minVisualAlpa, col.a));
                    if (Handles.Button(pos, Quaternion.identity, m_dotSize, m_dotSize, Handles.DotHandleCap))
                    {
                        vertexColor.SetColorWithMask(m_colorMask, m_paintColor);
                        m_currentMesh.SetColors(m_vertexColors.GetColors());
                        m_meshFilter.mesh = m_currentMesh;
                    }
                }
                Handles.color = Color.white;
            }
        }

        private void ColorAllVertices()
        {
            foreach(VertexColor vertexColor in m_vertexColors)
            {
                vertexColor.SetColorWithMask(m_colorMask, m_paintColor);
            }

            m_currentMesh.SetColors(m_vertexColors.GetColors());
            m_meshFilter.mesh = m_currentMesh;
        }

        bool CheckIfNoObjectSelectedAndShowInfo()
        {
            if (IsMeshSet() || IsGameObjectWithMeshSelected()) return false;
            EditorGUILayout.HelpBox("Select object with mesh you want to edit", MessageType.Info);
            return true;
        }

        bool IsGameObjectWithMeshSelected()
        {
            GameObject obj = Selection.activeGameObject;
            if (obj == null) return false;
            MeshFilter filter = obj.GetComponent<MeshFilter>();
            if (filter == null) return false;
            return (filter.sharedMesh != null);
        }

        void HandleEnableMeshEditButton()
        {
            if (GUILayout.Button(IsMeshSet() ? "Stop editing mesh" : "Edit mesh"))
            {
                if (!IsMeshSet())
                {
                    SetMesh();
                }
                else
                {
                    ClearMem();
                }
            }
        }

        void ShowEditMeshGUI()
        {
            EditorGUILayout.Space();

            EditorGUIUtility.labelWidth = 64;
            m_dotSize = EditorGUILayout.Slider("Dot size", m_dotSize, 0.001f, 0.2f);
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Color Mask");
            EditorGUILayout.BeginHorizontal();
            EditorGUIUtility.labelWidth = 16;
            m_colorMask.isR = EditorGUILayout.Toggle("R", m_colorMask.isR);
            m_colorMask.isG = EditorGUILayout.Toggle("G", m_colorMask.isG);
            m_colorMask.isB = EditorGUILayout.Toggle("B", m_colorMask.isB);
            m_colorMask.isA = EditorGUILayout.Toggle("A", m_colorMask.isA);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();

            EditorGUIUtility.labelWidth = 128;
            m_paintColor = EditorGUILayout.ColorField("Paint vertex color", m_paintColor);
            if (IsMeshSet() && GUILayout.Button("Paint all"))
            {
                ColorAllVertices();
            }
            if (IsMeshSet() && GUILayout.Button("Save mesh"))
            {
                SaveMesh();
            }
        }

        private void OnGUI()
        {
            if (CheckIfNoObjectSelectedAndShowInfo())
            {
                return;
            }

            HandleEnableMeshEditButton();

            if (IsMeshSet())
            {
                ShowEditMeshGUI();
            }
        }

        private bool IsMeshSet()
        {
            return (null != m_vertexColors && m_currentMesh && m_meshFilter);
        }

        private void SetMesh()
        {
            GameObject currentObject = Selection.activeGameObject;
            if (currentObject && currentObject.GetComponent<MeshFilter>())
            {
                m_meshFilter = currentObject.GetComponent<MeshFilter>();
                string name = m_meshFilter.sharedMesh.name;
                Mesh meshCopy = Mesh.Instantiate(m_meshFilter.sharedMesh) as Mesh;
                meshCopy.name = ((m_meshFilter.sharedMesh.name.Substring(0, 3).CompareTo("mp_") == 0) ? "" : "mp_") + name;
                m_currentMesh = meshCopy;
                m_vertexColors = new VertexColors(m_currentMesh);
            }
            else
            {
                ClearMem();
            }
        }

        private void ClearMem()
        {
            m_vertexColors = null;
            m_meshFilter = null;
            m_currentMesh = null;
        }
    }
}
