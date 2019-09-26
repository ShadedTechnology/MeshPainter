using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;

public class EditorMeshPainter : EditorWindow {

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

    [MenuItem("Tools/MeshPainter")]
    public static void ShowWindow()
    {
        GetWindow<EditorMeshPainter>("MeshPainter");
    }
    
    float m_dotSize = 0.05f;
    MeshFilter m_meshFilter;
    List<Color> m_colors;
    Dictionary<Vector3, List<uint>> m_realVerts;
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
            foreach (var vert in m_realVerts)
            {
                Vector3 pos = m_meshFilter.transform.localToWorldMatrix.MultiplyPoint3x4(vert.Key);
                if (vert.Value.Count <= 0) continue;
                Color col = m_colors[(int)vert.Value[0]];
                Handles.color = new Color(col.r, col.g, col.b, Mathf.Max(minVisualAlpa, col.a));
                if (Handles.Button(pos, Quaternion.identity, m_dotSize, m_dotSize, Handles.DotHandleCap))
                {
                    foreach(int i in vert.Value)
                    {
                        m_colors[i] = m_colorMask.FilterColor(m_colors[i], m_paintColor);
                    }
                    m_currentMesh.SetColors(m_colors);
                    m_meshFilter.mesh = m_currentMesh;
                }
            }
            Handles.color = Color.white;
        }
    }

    private void ColorAllVertices()
    {
        if (m_colors.Count != m_currentMesh.vertexCount)
        {
            m_colors = new List<Color>(m_currentMesh.vertexCount);
            for (int i = 0; i < m_currentMesh.vertexCount; ++i)
            {
                m_colors.Add(m_paintColor);
            }
        }
        else
        {
            for (int i = 0; i < m_currentMesh.vertexCount; ++i)
            {
                m_colors[i] = m_colorMask.FilterColor(m_colors[i], m_paintColor);
            }
        }
        m_currentMesh.SetColors(m_colors);
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
            if(!IsMeshSet())
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
        if(CheckIfNoObjectSelectedAndShowInfo())
        {
            return;
        }

        HandleEnableMeshEditButton();

        if(IsMeshSet())
        {
            ShowEditMeshGUI();
        }
    }

    private bool IsMeshSet()
    {
        return (null != m_realVerts && m_currentMesh && m_meshFilter);
    }

    private void SetMesh()
    {
        GameObject currentObject = Selection.activeGameObject;
        if (currentObject && currentObject.GetComponent<MeshFilter>())
        {
            m_meshFilter = currentObject.GetComponent<MeshFilter>();
            string name = m_meshFilter.sharedMesh.name;
            Mesh meshCopy = Mesh.Instantiate(m_meshFilter.sharedMesh) as Mesh;
            meshCopy.name = ((m_meshFilter.sharedMesh.name.Substring(0,3).CompareTo("mp_")==0)?"":"mp_") + name;
            m_currentMesh = meshCopy;
            m_colors = new List<Color>(m_currentMesh.vertexCount);
            m_currentMesh.GetColors(m_colors);
            m_realVerts = new Dictionary<Vector3, List<uint>>();
            for (uint i = 0; i < m_currentMesh.vertexCount; ++i)
            {
                if (i >= m_colors.Count)
                {
                    m_colors.Add(Color.white);
                }
                Vector3 currentVec = m_currentMesh.vertices[i];
                if (m_realVerts.ContainsKey(currentVec))
                {
                    m_realVerts[currentVec].Add(i);
                }
                else
                {
                    m_realVerts.Add(currentVec, new List<uint>() { i });
                }
            }
        }
        else
        {
            ClearMem();
        }
    }

    private void ClearMem()
    {
        m_realVerts = null;
        m_meshFilter = null;
        m_currentMesh = null;
        m_colors = null;
    }
}
