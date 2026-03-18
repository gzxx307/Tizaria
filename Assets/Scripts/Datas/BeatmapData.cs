using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// 谱面集
/// 一首歌曲的所有谱面数据的集合，包含了不同难度的谱面数据。
/// </summary>
[Serializable]
public class BeatmapDataSet
{
    // 基本信息
    
    // 谱面集的ID
    public string Id;
    // 歌曲名称
    public string Title;
    // 曲绘路径
    public string CoverPath;
    // 音频文件路径
    public string AudioPath;
    // 艺术家
    public string Artist;
    // 画师
    public string Illustrator;
    // 谱面作者
    public string MapWriter;
    // 歌曲Tag（用于分类显示与搜索）
    public List<string> Tags;
    
    // 预览歌曲起始时间（ms）
    public int PreviewStartTime;
    // 预览歌曲持续时间（ms）
    public int PreviewDuration;
    
    // 谱面数据列表
    public List<BeatmapData> Beatmaps;
    
    // 本地缓存
    
    // 曲绘
    [NonSerialized]
    public Texture2D CoverTexture;
    // 音频
    [NonSerialized]
    public AudioClip AudioClip;
    
    // 描述性文本
    public string Description;
}

[Serializable]
public class BeatmapData
{
    // 基本信息
    
    // ID
    public string Id;
    // 所属的集合
    public string SetId;
    // 该难度作者（可重载集合作者）
    public string MapWriter;
    // 谱面版本号
    public string Version;
    // 谱面创建时间
    public string CreationTime;
    // 最近一次更新时间
    public string LastUpdateTime;
    
    // 谱面信息
    
    // 难度描述
    public string DifficultyDescription;
    // 难度（一位小数）
    public float Difficulty;
    
    // 谱面参数
    
    // 最小BPM（若BPM不变则最大BPM与最小BPM相等）
    public float MinBPM;
    // 最大BPM（若BPM不变则最大BPM与最小BPM相等）
    public float MaxBPM;
    // 歌曲总长度（ms）
    public int TotalLength;
    
    // 关键帧
    
    // BPM变化关键帧
    public List<BPMTimePoint> BPMTimePoint;
    // SV变化关键帧
    public List<SVTimePoint> SVTimePoint;
    
    // 音符列表
    public List<NoteData> Notes;
    // 物量
    public int NoteCount;
    
    // 缓存
    
    // 按列分组的音符列表，方便游戏过程中进行判定
    [NonSerialized]
    public List<List<NoteData>> NotesByColumn;
}


[Serializable]
public class BPMTimePoint
{
    // 时间（ms）
    public int Time;
    // BPM值
    public float BPM;
    // 拍号分子（默认 4）
    public int Numerator;
    // 拍号分母（默认 4）
    public int Denominator;
}

[Serializable]
public class SVTimePoint
{
    // 时间（ms）
    public int Time;
    // SV值
    public float SV;
}

[Serializable]
public class NoteData
{
    // 唯一序号，按照时间排序，若时间相同则最左侧的ID较小
    public int Id;
    // 所在列
    public int Column;
    // 按下该音符的判定时间（ms）
    public int Time;
    // 长条结束时间（ms），如果是单音符则与Time相同
    public int EndTime;
    // 音符类型
    public NoteType Type;

    // 一些属性
    
    // 是否为长条音符
    public bool IsHold => Type == NoteType.Hold;
    // 长条音符持续时间，如果该音符不是长条音符则为0
    public float HoldTIme => IsHold ? EndTime - Time : 0f;
}
