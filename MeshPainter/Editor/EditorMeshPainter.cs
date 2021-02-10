using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;

namespace ShadedTechnology.MeshPainter
{
    using VertexColor = VertexColors.VertexColor;

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

        const float MIN_VISUAL_ALPHA = 0.3f;
        const float MIN_DOT_SIZE = 0.001f;
        const float MAX_DOT_SIZE = 0.2f;

        private void SaveMesh()
        {
            string path = EditorUtility.SaveFilePanelInProject("Choose Location for Mesh to save", m_currentMesh.name, "asset", "Save your mesh");
            AssetDatabase.CreateAsset(m_currentMesh, path);
            AssetDatabase.Refresh();
            m_meshFilter.mesh = AssetDatabase.LoadAssetAtPath(path, typeof(Mesh)) as Mesh;
        }

        // Window has been selected
        private void OnFocus()
        {
            // Remove delegate listener if it has previously
            // been assigned.
            SceneView.onSceneGUIDelegate -= this.OnSceneGUI;
            // Add (or re-add) the delegate.
            SceneView.onSceneGUIDelegate += this.OnSceneGUI;

        }

        private void OnDestroy()
        {
            // When the window is destroyed, remove the delegate
            // so that it will no longer do any drawing.
            SceneView.onSceneGUIDelegate -= this.OnSceneGUI;
            ClearMem();
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (IsMeshSet())
            {
                HandleSceneVertexPainting();
            }
        }

        private void ApplyVertexColorsToMesh()
        {
            m_currentMesh.SetColors(m_vertexColors.GetColors());
            m_meshFilter.mesh = m_currentMesh;
        }

        private void HandleSceneVertexPainting()
        {
            bool hasChanged = false;
            Color handlesColor = Handles.color;
            foreach (VertexColor vertexColor in m_vertexColors)
            {
                Vector3 pos = vertexColor.GetPosition(m_meshFilter.transform.localToWorldMatrix);
                Color col = vertexColor.GetColor();
                Handles.color = new Color(col.r, col.g, col.b, Mathf.Lerp(MIN_VISUAL_ALPHA, 1, col.a));
                if (Handles.Button(pos, Quaternion.identity, m_dotSize, m_dotSize, Handles.DotHandleCap))
                {
                    vertexColor.SetColorWithMask(m_colorMask, m_paintColor);
                    hasChanged = true;
                }
            }
            if (hasChanged) ApplyVertexColorsToMesh();
            Handles.color = handlesColor;
        }

        private void ColorAllVertices()
        {
            foreach(VertexColor vertexColor in m_vertexColors)
            {
                vertexColor.SetColorWithMask(m_colorMask, m_paintColor);
            }
            ApplyVertexColorsToMesh();
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
                Mesh mesh;
                if (AssetDatabase.Contains(m_meshFilter.sharedMesh))
                {
                    mesh = Mesh.Instantiate(m_meshFilter.sharedMesh) as Mesh;
                    mesh.name = ((m_meshFilter.sharedMesh.name.Length >= 3 &&
                        (m_meshFilter.sharedMesh.name.Substring(0, 3).CompareTo("mp_") == 0)) ? "" : "mp_") + name;
                }
                else
                {
                    mesh = m_meshFilter.sharedMesh;
                }
                m_currentMesh = mesh;
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

        private bool CheckIfNoObjectSelectedAndShowInfo()
        {
            if (IsMeshSet() || IsGameObjectWithMeshSelected()) return false;
            EditorGUILayout.HelpBox("Select object with mesh you want to edit", MessageType.Info);
            return true;
        }

        private bool IsGameObjectWithMeshSelected()
        {
            GameObject obj = Selection.activeGameObject;
            if (obj == null) return false;
            MeshFilter filter = obj.GetComponent<MeshFilter>();
            if (filter == null) return false;
            return (filter.sharedMesh != null);
        }

        private void HandleEnableMeshEditButton()
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

        private void ShowDotSizeSliderGUI()
        {
            EditorGUIUtility.labelWidth = 64;
            m_dotSize = EditorGUILayout.Slider("Dot size", m_dotSize, MIN_DOT_SIZE, MAX_DOT_SIZE);
        }

        private void ShowColorMaskGUI()
        {
            EditorGUILayout.LabelField("Color Mask");
            EditorGUILayout.BeginHorizontal();
            EditorGUIUtility.labelWidth = 16;
            m_colorMask.isR = EditorGUILayout.Toggle("R", m_colorMask.isR);
            m_colorMask.isG = EditorGUILayout.Toggle("G", m_colorMask.isG);
            m_colorMask.isB = EditorGUILayout.Toggle("B", m_colorMask.isB);
            m_colorMask.isA = EditorGUILayout.Toggle("A", m_colorMask.isA);
            EditorGUILayout.EndHorizontal();
        }

        private void ShowPaintColorGUI()
        {
            EditorGUIUtility.labelWidth = 128;
            m_paintColor = EditorGUILayout.ColorField("Paint vertex color", m_paintColor);
        }

        private void ShowPaintAllButtonGUI()
        {
            if (IsMeshSet() && GUILayout.Button("Paint all"))
            {
                ColorAllVertices();
            }
        }

        private void ShowSaveMeshButtonGUI()
        {
            if (IsMeshSet() && GUILayout.Button("Save mesh"))
            {
                SaveMesh();
            }
        }

        private void ShowEditMeshGUI()
        {
            float labelWidth = EditorGUIUtility.labelWidth;
            EditorGUILayout.Space();
            ShowDotSizeSliderGUI();
            EditorGUILayout.Space();
            ShowColorMaskGUI();
            EditorGUILayout.Space();
            ShowPaintColorGUI();
            ShowPaintAllButtonGUI();
            ShowSaveMeshButtonGUI();
            EditorGUIUtility.labelWidth = labelWidth;
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
    }
}
