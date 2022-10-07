/*
 * Created by jiadong chen
 * https://jiadong-chen.medium.com/
 * 用来烘焙动作贴图。烘焙对象使用Animation组件，并且在导入时设置Rig为Legacy
 */
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;
using System.IO;

/// <summary>
/// 保存需要烘焙的动画的相关数据
/// </summary>
public struct AnimData
{
    #region FIELDS

    private readonly List<AnimationState> _animClips;
    private string _name;
    private int _vertexCount;

    private  Animation _animation;
    private SkinnedMeshRenderer _skin;

    public List<AnimationState> AnimationClips => _animClips;
    public string Name => _name;
    public int VertexCount => _vertexCount;

    #endregion

    public AnimData(Animation anim, SkinnedMeshRenderer smr, string goName)
    {
        _animClips = new List<AnimationState>(anim.Cast<AnimationState>());
        _animation = anim;
        _skin = smr;
        _name = goName;

        _vertexCount = smr.sharedMesh.vertexCount;
    }

    #region METHODS

    public void AnimationPlay(string animName)
    {
        _animation.Play(animName);
    }

    public void SampleAnimAndBakeMesh(ref Mesh m)
    {
        SampleAnim();
        BakeMesh(ref m);
    }

    private void SampleAnim()
    {
        if (_animation == null)
        {
            Debug.LogError("animation is null!!");
            return;
        }

        _animation.Sample();
    }

    private void BakeMesh(ref Mesh m)
    {
        if (_skin == null)
        {
            Debug.LogError("skin is null!!");
            return;
        }

        _skin.BakeMesh(m);
    }


    #endregion

}

/// <summary>
/// 烘焙后的数据
/// </summary>
public struct BakedData
{
    #region FIELDS

    private readonly string _name;
    private readonly float _animLen;
    private readonly byte[] _rawAnimMap;
    private readonly int _animMapWidth;
    private readonly int _animMapHeight;
    private readonly TextureFormat _textureFormat;
    private Bounds _bounds;

    #endregion

    public BakedData(string name, float animLen, Texture2D animMap, TextureFormat textureFormat)
    {
        _name = name;
        _animLen = animLen;
        _animMapHeight = animMap.height;
        _animMapWidth = animMap.width;
        _rawAnimMap = animMap.GetRawTextureData();
        _textureFormat = textureFormat;
        _bounds = default;
    }

    public void SetBounds(Bounds bounds)
    {
        _bounds = bounds;
    }

    public int AnimMapWidth => _animMapWidth;

    public string Name => _name;

    public float AnimLen => _animLen;

    public byte[] RawAnimMap => _rawAnimMap;

    public int AnimMapHeight => _animMapHeight;

    public TextureFormat TextureFormat => _textureFormat;

    public Bounds Bounds => _bounds;
}

/// <summary>
/// 剔除多余顶点
/// </summary>
public class VertexFilter
{
    private readonly List<int> _indexMap;
    private readonly int _vertexCount;

    public int VertexCount => _vertexCount;

    public VertexFilter(Mesh mesh)
    {
        _indexMap = new List<int>(mesh.vertexCount);

        var vertexMap = new Dictionary<Vector3, int>();
        var vertices = mesh.vertices;
        for (int i = 0; i < vertices.Length; i++)
        {
            if (vertexMap.TryGetValue(vertices[i], out var index))
            {
                _indexMap.Add(index);
            }
            else
            {
                _indexMap.Add(i);
                vertexMap.Add(vertices[i], i);
                _vertexCount++;
            }
        }
    }

    public Vector3[] GetVertices(Vector3[] vertices)
    {
        var indexSet = new HashSet<int>(_indexMap);
        var list = new List<Vector3>(indexSet.Count);
        for (var i = 0; i < vertices.Length; i++)
        {
            if(indexSet.Contains(i))
                list.Add(vertices[i]);
        }

        return list.ToArray();
    }

    public void WriteToUV(Mesh mesh)
    {
        var indexSet = new HashSet<int>(_indexMap);
        var indexList = indexSet.ToList();

        var uv = new Vector2[mesh.vertexCount];
        for (var i = 0; i < uv.Length; i++)
        {
            var newIndex = indexList.IndexOf(_indexMap[i]);
            uv[i].x = newIndex;
        }

        mesh.uv3 = uv;
    }
}

/// <summary>
/// 烘焙器
/// </summary>
public class AnimMapBaker{

    #region FIELDS

    private AnimData? _animData = null;
    private Mesh _bakedMesh;
    private readonly List<Vector3> _vertices = new List<Vector3>();
    private readonly List<BakedData> _bakedDataList = new List<BakedData>();
    private float _samplingRate = 1f;
    private bool _enableTextureCompression;
    private bool _enableVertexCulling;
    private bool _enablePotTexture;
    private Bounds _bounds;
    private VertexFilter _vertexFilter;
    private bool _bakedMeshSaved;

    #endregion

    #region METHODS


    public Mesh SaveAndGetBakedMesh(string path)
    {
        if (_bakedMeshSaved) return _bakedMesh;
        _vertexFilter.WriteToUV(_bakedMesh);
        AssetDatabase.CreateAsset(_bakedMesh, path);
        _bakedMeshSaved = true;

        return _bakedMesh;
    }

    public void SetEnablePotTexture(bool enablePotTexture)
    {
        _enablePotTexture = enablePotTexture;
    }

    public void SetEnableVertexCulling(bool enableVertexCulling)
    {
        _enableVertexCulling = enableVertexCulling;
    }

    public void SetEnableTextureCompression(bool enableTextureCompress)
    {
        _enableTextureCompression = enableTextureCompress;
    }
    
    public void SetSamplingRate(float samplingRate)
    {
        _samplingRate = samplingRate;
    }

    public void SetAnimData(GameObject go)
    {
        if(go == null)
        {
            Debug.LogError("go is null!!");
            return;
        }

        var anim = go.GetComponent<Animation>();
        var smr = go.GetComponentInChildren<SkinnedMeshRenderer>();

        if(anim == null || smr == null)
        {
            Debug.LogError("anim or smr is null!!");
            return;
        }
        _bakedMesh = new Mesh();
        _bakedMeshSaved = false;
        _animData = new AnimData(anim, smr, go.name);
        _vertexFilter = new VertexFilter(smr.sharedMesh);
    }

    public List<BakedData> Bake()
    {
        if(_animData == null)
        {
            Debug.LogError("bake data is null!!");
            return _bakedDataList;
        }


        //每一个动作都生成一个动作图
        foreach (var t in _animData.Value.AnimationClips)
        {
            if(!t.clip.legacy)
            {
                Debug.LogError(string.Format($"{t.clip.name} is not legacy!!"));
                continue;
            }
            BakePerAnimClip(t);
        }

        return _bakedDataList;
    }

    private void BakePerAnimClip(AnimationState curAnim)
    {
        var curClipFrame = 0;
        float sampleTime = 0;
        float perFrameTime = 0;

        // Keyframe interpolation
        var originFrameCount = curAnim.clip.frameRate * curAnim.length;
        curClipFrame = (int)(originFrameCount * _samplingRate);
        Debug.Log($"Keyframe interpolation: {curAnim.clip.name} {originFrameCount}->{curClipFrame}");

        if (_enablePotTexture) curClipFrame = Mathf.ClosestPowerOfTwo(curClipFrame);


        perFrameTime = curAnim.length / curClipFrame;

        var textureFormat = _enableTextureCompression ? TextureFormat.RGBA32 : TextureFormat.RGBAHalf;

        var mapWidth = _enableVertexCulling ? _vertexFilter.VertexCount : _animData.Value.VertexCount;
        if (_enablePotTexture) mapWidth = Mathf.NextPowerOfTwo(mapWidth);

        Debug.Log($"BakePerAnimClip {curAnim.clip.name} {mapWidth}x{curClipFrame}");

        var animMap = new Texture2D(mapWidth, curClipFrame, textureFormat, true);
        animMap.name = string.Format($"{_animData.Value.Name}_{curAnim.name}.animMap");
        _animData.Value.AnimationPlay(curAnim.name);

        var allVertices = new List<Vector3>();
        _bounds = new Bounds();
        for (var i = 0; i < curClipFrame; i++)
        {
            curAnim.time = sampleTime;

            _animData.Value.SampleAnimAndBakeMesh(ref _bakedMesh);

            CalculateBounds();

            var vertices = _bakedMesh.vertices;

            if (_enableVertexCulling)
                vertices = _vertexFilter.GetVertices(vertices);

            var tempList = vertices.ToList();
            while (tempList.Count < mapWidth) tempList.Add(Vector3.zero);
            allVertices.AddRange(tempList);

            sampleTime += perFrameTime;
        }

        Color[] pixels;

        // Animation texture compression.
        if (_enableTextureCompression)
        {
            pixels = allVertices.Select(v =>
            {
                var color = new Color
                {
                    r = Mathf.InverseLerp(_bounds.min.x, _bounds.max.x, v.x),
                    g = Mathf.InverseLerp(_bounds.min.y, _bounds.max.y, v.y),
                    b = Mathf.InverseLerp(_bounds.min.z, _bounds.max.z, v.z)
                };
                return color;
            }).ToArray();
        }
        else
        {
            pixels = allVertices.Select(x => new Color(x.x, x.y, x.z)).ToArray();
        }

        animMap.SetPixels(0,0, mapWidth, curClipFrame, pixels);
        animMap.Apply();

        var data = new BakedData(animMap.name, curAnim.clip.length, animMap, textureFormat);
        data.SetBounds(_bounds);
        _bakedDataList.Add(data);
    }

    private void CalculateBounds()
    {
        var vertices = _bakedMesh.vertices;
        for(var i = 0; i < _bakedMesh.vertexCount; i++)
        {
            _bounds.Encapsulate(vertices[i]);
        }
    }

    #endregion

}
