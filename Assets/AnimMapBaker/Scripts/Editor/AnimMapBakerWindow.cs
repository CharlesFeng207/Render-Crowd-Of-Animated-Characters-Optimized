/*
 * Created by jiadong chen
 * https://jiadong-chen.medium.com/
 */
using UnityEngine;
using UnityEditor;
using System.IO;
using UnityEngine.Rendering;

public class AnimMapBakerWindow : EditorWindow {

    private enum SaveStrategy
    {
        // Only anim map
        AnimMap, 
        // With shader
        Mat, 
        // Prefab with mat
        Prefab 
    }

    #region FIELDS

    private const string BuiltInShader = "chenjd/BuiltIn/AnimMapShader";
    private const string URPShader = "chenjd/URP/AnimMapShader";
    private const string ShadowShader = "chenjd/BuiltIn/AnimMapWithShadowShader";
    private static GameObject _targetGo;
    private static AnimMapBaker _baker;
    private static string _path = "AnimMapBaker";
    private static string _subPath = "SubPath";
    private static SaveStrategy _strategy = SaveStrategy.Prefab;
    private static Shader _animMapShader;
    private static Shader _prevAnimMapShader;
    private static readonly int MainTex = Shader.PropertyToID("_MainTex");
    private static readonly int AnimMap = Shader.PropertyToID("_AnimMap");
    private static readonly int AnimLen = Shader.PropertyToID("_AnimLen");
    private bool _isShadowEnabled = false;
    private static bool _enableTextureCompression;
    private float _samplingRate = 1f;

    #endregion


    #region  METHODS

    [MenuItem("Window/AnimMapBaker")]
    public static void ShowWindow()
    {
        EditorWindow.GetWindow(typeof(AnimMapBakerWindow));
        _baker = new AnimMapBaker();
    }

    private void OnGUI()
    {
        _targetGo = (GameObject)EditorGUILayout.ObjectField(_targetGo, typeof(GameObject), true);
        _subPath = _targetGo == null ? _subPath : _targetGo.name;
        EditorGUILayout.LabelField(string.Format($"Output Path: {Path.Combine(_path, _subPath)}"));
        _path = EditorGUILayout.TextField(_path);
        _subPath = EditorGUILayout.TextField(_subPath);

        _strategy = (SaveStrategy)EditorGUILayout.EnumPopup("Output Type:", _strategy);

        _samplingRate = EditorGUILayout.Slider("Sampling Rate", _samplingRate, 0f, 1f);
        _enableTextureCompression = EditorGUILayout.Toggle("Texture Compression", _enableTextureCompression);
        _isShadowEnabled = EditorGUILayout.Toggle("Enable Shadow", _isShadowEnabled);

        if(_isShadowEnabled)
        {
            var style = new GUIStyle(EditorStyles.label);
            style.normal.textColor = Color.yellow;

            EditorGUILayout.LabelField("Warning: Enabling shadows will cause additional draw calls to draw shadows.", style);

            _prevAnimMapShader = _animMapShader;
            _animMapShader = Shader.Find(ShadowShader);
        }
        else if(_prevAnimMapShader != null)
        {
            _animMapShader = _prevAnimMapShader;
        }

        if (!GUILayout.Button("Bake")) return;

        if(_targetGo == null)
        {
            EditorUtility.DisplayDialog("err", "targetGo is null！", "OK");
            return;
        }

        if (_animMapShader == null)
        {
            var shaderName = GraphicsSettings.renderPipelineAsset != null ? URPShader : BuiltInShader;
            _animMapShader = Shader.Find(shaderName);
        }

        if(_baker == null)
        {
            _baker = new AnimMapBaker();
        }

        _baker.SetSamplingRate(_samplingRate);
        _baker.SetEnableTextureCompression(_enableTextureCompression);
        _baker.SetAnimData(_targetGo);

        var list = _baker.Bake();

        if (list == null) return;
        foreach (var t in list)
        {
            var data = t;
            Save(ref data);
        }
    }

    private void Save(ref BakedData data)
    {
        switch(_strategy)
        {
            case SaveStrategy.AnimMap:
                SaveAsAsset(ref data);
                break;
            case SaveStrategy.Mat:
                SaveAsMat(ref data);
                break;
            case SaveStrategy.Prefab:
                SaveAsPrefab(ref data);
                break;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static Texture2D SaveAsAsset(ref BakedData data)
    {
        var folderPath = CreateFolder();
        var animMap = new Texture2D(data.AnimMapWidth, data.AnimMapHeight, data.TextureFormat, false);
        animMap.LoadRawTextureData(data.RawAnimMap);
        AssetDatabase.CreateAsset(animMap, Path.Combine(folderPath, data.Name + ".asset"));
        return animMap;
    }

    private static Material SaveAsMat(ref BakedData data)
    {
        if(_animMapShader == null)
        {
            EditorUtility.DisplayDialog("err", "shader is null!!", "OK");
            return null;
        }

        if(_targetGo == null || !_targetGo.GetComponentInChildren<SkinnedMeshRenderer>())
        {
            EditorUtility.DisplayDialog("err", "SkinnedMeshRender is null!!", "OK");
            return null;
        }

        var smr = _targetGo.GetComponentInChildren<SkinnedMeshRenderer>();
        var mat = new Material(_animMapShader);
        var animMap = SaveAsAsset(ref data);
        mat.SetTexture(MainTex, smr.sharedMaterial.mainTexture);
        mat.SetTexture(AnimMap, animMap);
        mat.SetFloat(AnimLen, data.AnimLen);

        if (_enableTextureCompression)
        {
            mat.EnableKeyword("TEXTURE_COMPRESSION");
            mat.SetVector("_PosRegionStart", data.Bounds.min);
            mat.SetVector("_PosRegionEnd", data.Bounds.max);
        }

        var folderPath = CreateFolder();
        AssetDatabase.CreateAsset(mat, Path.Combine(folderPath, $"{data.Name}.mat"));

        return mat;
    }

    private static void SaveAsPrefab(ref BakedData data)
    {
        var mat = SaveAsMat(ref data);

        if(mat == null)
        {
            EditorUtility.DisplayDialog("err", "mat is null!!", "OK");
            return;
        }

        var go = new GameObject();
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        go.AddComponent<MeshFilter>().sharedMesh = _targetGo.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh;

        var folderPath = CreateFolder();
        PrefabUtility.SaveAsPrefabAsset(go, Path.Combine(folderPath, $"{data.Name}.prefab")
            .Replace("\\", "/"));

        // Clean temp GameObject
        DestroyImmediate(go);
    }

    private static string CreateFolder()
    {
        var folderPath = Path.Combine("Assets/" + _path,  _subPath);
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            AssetDatabase.CreateFolder("Assets/" + _path, _subPath);
        }
        return folderPath;
    }

    #endregion


}
