using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;

public class EditorMeshPainter : EditorWindow {

    [MenuItem("Tools/MeshPainter")]
    public static void ShowWindow()
    {
        GetWindow<EditorMeshPainter>("MeshPainter");
    }

    bool enabledTool;
    float dotSize = 0.05f;
    GameObject _currentObject;
    MeshFilter _mf;
    List<Color> _colors;
    Dictionary<Vector3, List<uint>> _realVerts;
    Mesh _currentMesh;
    bool _onlyAlpha;
    Color _paintColor;

    private void SaveMesh()
    {
        if (!AssetDatabase.IsValidFolder("Assets/MeshPainter/Models/"))
        {
            AssetDatabase.CreateFolder("Assets/", "MeshPainter/Models/");
        }
        int i = 0;
        string name;
        do
        {
            name = _currentMesh.name + ((i != 0) ? " " + i: "");
            if(!File.Exists(Application.dataPath + "/MeshPainter/Models/" + name + ".asset"))
            {
                break;
            }
        }
        while (i++ < 100);
        AssetDatabase.CreateAsset(_currentMesh, "Assets/MeshPainter/Models/" + name + ".asset");
        AssetDatabase.Refresh();
        _mf.mesh = AssetDatabase.LoadAssetAtPath("Assets/MeshPainter/Models/" + name + ".asset", typeof(Mesh)) as Mesh;
    }

    // Window has been selected
    void OnFocus()
    {
        // Remove delegate listener if it has previously
        // been assigned.
        SceneView.onSceneGUIDelegate -= this.OnSceneGUI;
        // Add (or re-add) the delegate.
        SceneView.onSceneGUIDelegate += this.OnSceneGUI;
        SelectObject();

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
        if (!enabledTool) return;

        if(isSetMesh())
        {
            foreach (var vert in _realVerts)
            {
                Vector3 pos = _currentObject.transform.localToWorldMatrix.MultiplyPoint3x4(vert.Key);
                if (Handles.Button(pos, Quaternion.identity, dotSize, dotSize, Handles.DotHandleCap))
                {
                    foreach(int i in vert.Value)
                    {
                        if (_onlyAlpha)
                        {
                            _colors[i] = new Color(_colors[i].r, _colors[i].g, _colors[i].b, _paintColor.a);
                        }
                        else
                        {
                            _colors[i] = _paintColor;
                        }
                    }
                    _currentMesh.SetColors(_colors);
                    _mf.mesh = _currentMesh;
                }
            }
        }

        Handles.BeginGUI();
        // Do your drawing here using GUI.
        Handles.EndGUI();
    }

    private void OnInspectorUpdate()
    {
        //Handles.Button(Camera.main.transform.position, Quaternion.identity, 10, 20, Handles.ConeHandleCap);
    }

    private void OnGUI()
    {
        if(GUILayout.Button(enabledTool?"Disable Tool":"Enable Tool"))
        {
            enabledTool = !enabledTool;
        }
        if (enabledTool)
        {
            dotSize = EditorGUILayout.Slider("Dot size", dotSize, 0.001f, 0.2f);
            _onlyAlpha = EditorGUILayout.Toggle("Paint only alpha", _onlyAlpha);
            _paintColor = EditorGUILayout.ColorField("Paint vertex color", _paintColor);
            if (isSetMesh() && GUILayout.Button("Paint all"))
            {
                if (_colors.Count != _currentMesh.vertexCount)
                {
                    _colors = new List<Color>(_currentMesh.vertexCount);
                    for (int i = 0; i < _currentMesh.vertexCount; ++i)
                    {
                        _colors.Add(_paintColor);
                    }
                }
                else
                {
                    for (int i = 0; i < _currentMesh.vertexCount; ++i)
                    {
                        _colors[i] = _paintColor;
                    }
                }
                _currentMesh.SetColors(_colors);
                _mf.mesh = _currentMesh;
            }
            if (isSetMesh() && GUILayout.Button("Save mesh"))
            {
                SaveMesh();
            }
        }
    }

    private bool isSetMesh()
    {
        return (null != _realVerts && _currentObject && _currentMesh && _mf);
    }

    private void SetMesh()
    {
        _currentObject = Selection.activeGameObject;
        if (_currentObject && _currentObject.GetComponent<MeshFilter>())
        {
            _mf = _currentObject.GetComponent<MeshFilter>();
            string name = _mf.sharedMesh.name;
            Mesh meshCopy = Mesh.Instantiate(_mf.sharedMesh) as Mesh;
            meshCopy.name = ((_mf.sharedMesh.name.Substring(0,3).CompareTo("mp_")==0)?"":"mp_") + name;
            _currentMesh = meshCopy;
            _colors = new List<Color>(_currentMesh.vertexCount);
            _currentMesh.GetColors(_colors);
            _realVerts = new Dictionary<Vector3, List<uint>>();
            for (uint i = 0; i < _currentMesh.vertexCount; ++i)
            {
                if (i >= _colors.Count)
                {
                    _colors.Add(Color.white);
                }
                Vector3 currentVec = _currentMesh.vertices[i];
                if (_realVerts.ContainsKey(currentVec))
                {
                    _realVerts[currentVec].Add(i);
                }
                else
                {
                    _realVerts.Add(currentVec, new List<uint>() { i });
                }
            }
        }
        else
        {
            _realVerts = null;
            _mf = null;
            _currentMesh = null;
            _colors = null;
        }
    }

    private void ClearMem()
    {
        _realVerts = null;
        _currentObject = null;
        _mf = null;
        _currentMesh = null;
        _colors = null;
    }

    private void SelectObject()
    {
        if (enabledTool)
        {
            SetMesh();
        }
        else
        {
            ClearMem();
        }
    }

    private void OnSelectionChange()
    {
        SelectObject();
    }
}