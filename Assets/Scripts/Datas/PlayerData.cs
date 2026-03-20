using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[Serializable]
public class PlayerDataSet
{
    public List<PlayerData> Players;
}

/// <summary>
/// 玩家数据，玩家在整个游戏中的唯一存档。
/// 在开启游戏并登录时进行读取，并在游戏过程中进行修改与保存。
/// </summary>
[Serializable]
public class PlayerData
{
    // 基本信息
    
    // 玩家唯一标识符
    public string Uid;
    // 名称
    public string Name;
    // rks
    public int Ranks;
    // 头像路径
    public string AvatarPath;
    // 账号创建时间
    public string CreationTime;
    // 最后一次登录时间
    public string LastLoginTime;
    
    // 其他数据系统
    
    // 数据统计
    public PlayerStat PlayerStat;
    // 设置数据
    public PlayerSetting PlayerSetting;
}
/// <summary>
/// 玩家统计数据，记录玩家在游戏中的各种统计信息，只增不减
/// </summary>
[Serializable]
public class PlayerStat
{
    // 总游戏时长
    public float TotalPlayTime;
    // 总游戏次数（不算中途退出）
    public int TotalPlayCount;
    // 总谱面个数（需要在每次加载游戏时进行更新）
    public int TotalMapCount;
    // 通关谱面的个数（Ranking >= 'A'）（在玩家完成游戏时更新）
    public int TotalMapCompletedCount;
    // 完成的谱面ID列表（在启动游戏时以及玩家完成游戏时更新，当玩家新添加谱面的时候初始化一个Info）
    public List<PlayerBeatmapInfo> CompletedMapIds;
}

/// <summary>
/// 玩家设置数据
/// </summary>
[Serializable]
public class PlayerSetting
{
    // 操作设置
    
    // 键位
    // 默认 DF - JK
    public List<KeyCode> KeyBindings;
    
    // 谱面设置
    
    // 全局谱面偏移ms
    public float GlobalMapOffset;
    // 玩家偏好谱面下落速度修正（乘算）
    public float MapSpeed;
    
    // 视觉设置
    
    // 轨道宽度（默认为1.0f）
    public float KeySize;
    // 轨道位置
    public float KeyPosition;
    // 轨道皮肤ID
    // TODO: 需要在游戏中添加一个皮肤系统，允许玩家选择不同的轨道皮肤，并且在这里记录玩家选择的皮肤ID
    public string SkinId;
    
    // 音频设置
    
    // 主控轨道
    public float MasterVolume;
    // 音乐轨道
    public float MusicVolume;
    // UI音效轨道
    public float UIVolume;
    // 打击音效轨道
    public float HitVolume;
}

/// <summary>
/// 依附于玩家的谱面信息，需要与整个谱面的统计区分开，但是又要统一数据
/// </summary>
[Serializable]
public class PlayerBeatmapInfo
{
    // 谱面ID（读取时只需要将PlayerMapInfo的ID与MapData的ID匹配）
    public string BeatmapId;
    // 最高分数
    public int HighestScore;
    // 最高评级
    public Rank HighestRank;
    // 最高acc
    public float HighestAccuracy;
    // 最近一次完成时间
    public string LastCompletedTime;
    // 谱面游玩次数
    public int PlayCount;
    
    // 最高连击数
    public int HighestCombo;
    
    // Perfect数
    public int PerfectCount;
    // Great数
    public int GreatCount;
    // Good数
    public int GoodCount;
    // Bad数
    public int BadCount;
    // Miss数
    public int MissCount;
    
}
