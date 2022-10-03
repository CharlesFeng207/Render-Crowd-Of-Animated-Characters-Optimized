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

    private int _vertexCount;
    private int _mapWidth;
    private readonly List<AnimationState> _animClips;
    private string _name;

    private  Animation _animation;
    private SkinnedMeshRenderer _skin;

    public List<AnimationState> AnimationClips => _animClips;
    public int MapWidth => _mapWidth;
    public string Name => _name;

    #endregion

    public AnimData(Animation anim, SkinnedMeshRenderer smr, string goName)
    {
        _vertexCount = smr.sharedMesh.vertexCount;
        _mapWidth = Mathf.NextPowerOfTwo(_vertexCount);
        _animClips = new List<AnimationState>(anim.Cast<AnimationState>());
        _animation = anim;
        _skin = smr;
        _name = goName;
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
    private readonly Bounds _bounds;

    #endregion

    public BakedData(string name, float animLen, Texture2D animMap, TextureFormat textureFormat, Bounds bounds)
    {
        _name = name;
        _animLen = animLen;
        _animMapHeight = animMap.height;
        _animMapWidth = animMap.width;
        _rawAnimMap = animMap.GetRawTextureData();
        _textureFormat = textureFormat;
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
    private Bounds _bounds;

    #endregion

    #region METHODS

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
        _animData = new AnimData(anim, smr, go.name);
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
        curClipFrame = Mathf.ClosestPowerOfTwo((int)(originFrameCount * _samplingRate));

        Debug.Log($"BakePerAnimClip {curAnim.clip.name} vertexCount:{_bakedMesh.vertexCount} frameCount:{originFrameCount} samepleFrameCount:{curClipFrame}");

        perFrameTime = curAnim.length / curClipFrame;

        var textureFormat = _enableTextureCompression ? TextureFormat.RGBA32 : TextureFormat.RGBAHalf;
        var animMap = new Texture2D(_animData.Value.MapWidth, curClipFrame, textureFormat, true);
        animMap.name = string.Format($"{_animData.Value.Name}_{curAnim.name}.animMap");
        _animData.Value.AnimationPlay(curAnim.name);

        for (var i = 0; i < curClipFrame; i++)
        {
            curAnim.time = sampleTime;

            _animData.Value.SampleAnimAndBakeMesh(ref _bakedMesh);

            var vertices = _bakedMesh.vertices;

            if (_enableTextureCompression)
                CalculateBounds();

            for(var j = 0; j < _bakedMesh.vertexCount; j++)
            {
                var vertex = vertices[j];

                // Animation texture compression.
                if (_enableTextureCompression)
                {
                    vertex.x = Mathf.InverseLerp(_bounds.min.x, _bounds.max.x, vertex.x);
                    vertex.y = Mathf.InverseLerp(_bounds.min.y, _bounds.max.y, vertex.y);
                    vertex.z = Mathf.InverseLerp(_bounds.min.z, _bounds.max.z, vertex.z);
                }

                animMap.SetPixel(j, i, new Color(vertex.x, vertex.y, vertex.z));
            }

            sampleTime += perFrameTime;
        }
        animMap.Apply();

        var data = new BakedData(animMap.name, curAnim.clip.length, animMap, textureFormat, _bounds);
        _bakedDataList.Add(data);
    }

    private void CalculateBounds()
    {
        _bounds = new Bounds();
        var vertices = _bakedMesh.vertices;
        for(var i = 0; i < _bakedMesh.vertexCount; i++)
        {
            _bounds.Encapsulate(vertices[i]);
        }
    }

    #endregion

}
