using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class PlaySession
{
    // 当前谱面
    public BeatmapData CurrentBeatmap  { get; private set; }
    
    // 当前音频时间
    public int CurrentAudioTime { get; set; }
    
    // 实时成绩（每帧刷新）
    
    // 当前分数
    public int CurrentScore      { get; set; }
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
    // 当前游戏进度下是否全连
    public bool IsFullCombo => MissCount == 0 && BadCount == 0;
    // 当前游戏进度下是否全P
    public bool IsFullPerfect => GreatCount == 0 && GoodCount == 0 && BadCount == 0 && MissCount == 0;
    // 是否是严判模式
    public bool IsStrict { get; set; }
    
    // 实时判定计数（断长条算Miss）
    
    public int PerfectCount { get; set; }
    public int GreatCount { get; set; }
    public int GoodCount { get; set; }
    public int BadCount { get; set; }
    public int MissCount { get; set; }
    
    // 当前等待判定的音符列表
    public List<Queue<ActiveNote>> WaitingNotes { get; private set; }
    // 当前屏幕中所有可见的音符列表
    public List<ActiveNote> VisibleNotes { get; private set; }
    
    // 当前按键状态（使用列表保存，分别表示每个按键的状态）
    
    // 是否被按下
    public List<bool> ColumnPressed { get; private set; }
    // 是否持续按住
    public List<bool> ColumnHeld { get; private set; }
    // 是否刚释放
    public List<bool> ColumnReleased { get; private set; }
    
    // 每个判定等级对应的单音符得分
    private List<int> _noteScores;
    
    // 函数

    // 构造
    public PlaySession(BeatmapData beatmap, bool isStrict)
    {
        CurrentBeatmap = beatmap;
        IsStrict = isStrict;

        // 初始化数值
        
        CurrentScore = 0;
        CurrentCombo = 0;
        MaxCombo = 0;
        CurrentRanking = Rank.F;
        CurrentAccuracy = 0f;
        
        IsPlaying = false;
        IsPaused = false;
        IsCompleted = false;
        
        PerfectCount = 0;
        GreatCount = 0;
        GoodCount = 0;
        BadCount = 0;
        MissCount = 0;
        
        // 初始化容器
        
        WaitingNotes = new List<Queue<ActiveNote>>(beatmap.ColumnCount);
        VisibleNotes = new List<ActiveNote>(beatmap.ColumnCount);
        
        ColumnPressed = new List<bool>(beatmap.ColumnCount);
        ColumnHeld = new List<bool>(beatmap.ColumnCount);
        ColumnReleased = new List<bool>(beatmap.ColumnCount);

        for (int i = 0; i < beatmap.ColumnCount; i++)
        {
            WaitingNotes.Add(new Queue<ActiveNote>());
            
            ColumnPressed.Add(false);
            ColumnHeld.Add(false);
            ColumnReleased.Add(false);
        }
        
        // 根据Beatmap初始化其他数值
        
        _noteScores = Calculator.GetNoteScores(beatmap.NoteCount, IsStrict);
        
    }

    /// <summary>
    /// 根据输入产生的打击判定更新游戏会话状态
    /// </summary>
    /// <param name="judgement"> 判定结果 </param>
    public void AddJudgement(Judgement judgement)
    {
        // 更新计数
        switch (judgement)
        {
            case Judgement.Perfect:
                PerfectCount++;
                break;
            case Judgement.Great:
                GreatCount++;
                break;
            case Judgement.Good:
                GoodCount++;
                break;
            case Judgement.Bad:
                BadCount++;
                break;
            case Judgement.Miss:
                MissCount++;
                break;
            default:
                MissCount++;
                break;
        }
        
        // 更新分数
        AddScore(judgement);
        
        // 更新连击
        if (judgement == Judgement.Bad || judgement == Judgement.Miss)
        {
            ClearCombo();
        }
        else
        {
            CurrentCombo++;
        }
        
        // 更新acc
        CurrentAccuracy = Calculator.CalculateAccuracy(PerfectCount, GreatCount, GoodCount, BadCount, MissCount);
    }
    
    // 公开控制函数

    // 游戏结束事件
    public void Complete()
    {
        IsPlaying = false;
        IsCompleted = true;
    }
    
    // 查询与计算函数

    public float GetProgress()
    {
        return Mathf.Clamp01(CurrentAudioTime / (float)CurrentBeatmap.TotalLength);
    }
    
    // 列表操作
    
    /// <summary>
    /// 判定指定列的等待队列中，是否存在在给定时间窗口内的音符头。（即是否存在除产生miss外的可判定音符）
    /// </summary>
    /// <param name="column">列索引</param>
    /// <param name="audioTimeMs">当前音频时间（ms）</param>
    public bool HasNoteInWindow(int column, float audioTimeMs)
    {
        if (column < 0 || column >= CurrentBeatmap.ColumnCount) return false;
        var queue = WaitingNotes[column];
        if (queue.Count == 0) return false;
        float delta = Mathf.Abs(queue.Peek().NoteData.Time - audioTimeMs);
        return delta <= ConfigBus.Instance.JudgementWindow.BadWindow;
    }
    
    // 从指定列的等待队列中取出队首音符（用于执行判定）。
    public bool TryDequeueNote(int column, out ActiveNote note)
    {
        note = null;
        if (column < 0 || column >= CurrentBeatmap.ColumnCount) return false;
        var queue = WaitingNotes[column];
        if (queue.Count == 0) return false;
        note = queue.Dequeue();
        return true;
    }
    
    // 将音符加入可见列表。
    public void AddVisibleNote(ActiveNote note)
    {
        if (note != null && !VisibleNotes.Contains(note))
            VisibleNotes.Add(note);
    }
 
    // 将音符从可见列表中移除。
    public bool RemoveVisibleNote(ActiveNote note)
    {
        return VisibleNotes.Remove(note);
    }
    
    // 按键状态更新函数
    
    // 每一帧更新一列的按键状态
    public void UpdateColumnInput(int column, bool isDown)
    {
        bool wasHeld = ColumnHeld[column];
        ColumnHeld[column] = isDown;
        ColumnPressed[column] = isDown && !wasHeld;
        ColumnReleased[column] = !isDown && wasHeld;
    }
    
    // 每帧结束后清除 Pressed / Released 瞬态标记。
    public void ClearFrameInputFlags()
    {
        for (int i = 0; i < CurrentBeatmap.ColumnCount; i++)
        {
            ColumnPressed[i] = false;
            ColumnReleased[i] = false;
        }
    }

    // 工具函数
    
    // 清空连击数
    private void ClearCombo()
    {
        CurrentCombo = 0;
    }
    // 更新分数
    private void AddScore(Judgement judgement)
    {
        CurrentScore += _noteScores[(int)judgement];
    }
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

    // 更新该音符的判定结果，并更新对应的状态
    public void GetJudgement(Judgement judgement)
    {
        LastJudgement = judgement;
        
        // 获取到对应的状态
        switch (judgement)
        {
            case Judgement.Perfect:
                State = IsHold ? NoteState.HoldActive : NoteState.HitPerfect;
                break;
            case Judgement.Great:
                State = IsHold ? NoteState.HoldActive : NoteState.HitGreat;
                break;
            case Judgement.Good:
                State = IsHold ? NoteState.HoldActive : NoteState.HitGood;
                break;
            case Judgement.Bad:
                State = NoteState.HitBad;
                break;
            case Judgement.Miss:
                State = NoteState.Missed;
                break;
            default:
                State = NoteState.Missed;
                break;
        }
    }
    
    // 完成长条
    public void CompleteHold()
    {
        IsHoldHeld = false;
        State = NoteState.HoldCompleted;
    }
    // 长条断开
    public void BreakHold()
    {
        IsHoldHeld = false;
        State = NoteState.HoldBroken;
    }
}

/// <summary>
/// 音符状态枚举
/// </summary>
public enum NoteState
{
    Waiting,
    HitPerfect,
    HitGreat,
    HitGood,
    HitBad,
    Missed,
    HoldActive,
    HoldCompleted,
    HoldBroken,
}