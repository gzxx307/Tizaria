using System;
using System.Collections.Generic;
using UnityEngine;

public class PlaySession
{
    // 当前谱面
    public BeatmapData CurrentBeatmap  { get; private set; }
    
    // 当前音频时间
    public float CurrentAudioTime { get; set; }
    
    // 实时成绩（每帧刷新）
    
    // 当前分数
    public int CurrentScore { get; set; }
    // 当前连击数
    public int CurrentCombo { get; set; }
    // 最大连击数
    public int MaxCombo { get; set; }
    // 当前排名
    public Rank CurrentRanking { get; set; }
    // 当前acc
    public float CurrentAccuracy { get; set; }
    
    // 状态
    
    // 是否正在进行游戏
    public bool IsPlaying { get; set; }
    // 是否暂停
    public bool IsPaused { get; set; }
    // 是否已结束
    public bool IsCompleted { get; set; }
    
    // 实时判定计数
    
    public int PerfectCount { get; set; }
    public int GreatCount { get; set; }
    public int GoodCount { get; set; }
    public int BadCount { get; set; }
    public int MissCount { get; set; }
    
    // 当前等待判定的音符列表
    public List<Queue<ActiveNote>> WaitingNotes { get; private set; }
    // 当前屏幕中所有可见的音符列表
    public List<ActiveNote> VisibleNotes { get; private set; }
    
    // 当前按键状态
    
    // 是否被按下
    public List<bool> ColumnPressed { get; private set; }
    // 是否持续按住
    public List<bool> ColumnHeld { get; private set; }
    // 是否刚释放
    public List<bool> ColumnReleased { get; private set; }
}
/// <summary>
/// 屏幕中活跃的音符运行时状态
/// </summary>
public class ActiveNote
{
    public NoteData NoteData { get; }
    
    // 列数
    public int Column => NoteData.Column;
    // 是否为长条
    public bool IsHold => NoteData.IsHold;
    
    // 音符运行时状态
    public NoteState State { get; set; } = NoteState.Waiting;
    // 是否正在被按下（对于单音符）或持续按住（对于长条）
    public bool IsHoldHeld { get; set; }
    // 是否已经被判定（无论是成功还是失败）
    public bool WasJudged => State != NoteState.Waiting;
    
    // 音符在屏幕中的位置（Y）
    public float ScreenY { get; set; }
    // 长条尾端的位置（Y）
    public float HoldEndScreenY { get; set; }
    // 判定闪光剩余时间
    public float JudgeFlashTimer { get; set; }
    // 判定结果
    public Judgement LastJudgement  { get; set; }
    
    public ActiveNote(NoteData data) { NoteData = data; }
}

/// <summary>
/// 音符状态枚举
/// </summary>
public enum NoteState
{
    Waiting,      // 等待按键
    HitPerfect,
    HitGreat,
    HitGood,
    HitBad,
    Missed,
    HoldActive,   // 长条正在被按住
    HoldCompleted,
    HoldBroken,   // 长条中途松手
}