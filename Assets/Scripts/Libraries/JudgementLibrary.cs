using System;
using System.Collections.Generic;
using UnityEngine;

public class JudgementLibrary
{
    public JudgementLibrary Instance = new JudgementLibrary();

    [SerializeField] private RankingRuleSO _rankingRuleSo;
    [SerializeField] private JudgementTimeSO _judgementTimeSo;

    /// <summary>
    /// 根据配置表计算等级
    /// <param name="Score"> 分数 </param>
    /// <param name="NoteCount"> 物量 </param>
    /// <param name="IsFullCombo"> 是否为全连 </param>
    /// <param name="IsStrict"> 是否为严判 </param>
    /// <returns> 评级 </returns>
    /// </summary>
    public Rank GetRank(int Score, int NoteCount, bool IsFullCombo, bool IsStrict)
    {
        // 如果不是严判，则不会到达理论值，直接将 NoteCount 置零以满足 T 级条件
        if (!IsStrict) NoteCount = 0; 
        if (Score == _rankingRuleSo.TScore + NoteCount) return Rank.T;
        if (Score >= _rankingRuleSo.SScore) return Rank.S;
        if (Score >= _rankingRuleSo.AScore) return Rank.A;
        if (Score >= _rankingRuleSo.BScore) return Rank.B;
        if (Score >= _rankingRuleSo.CScore) return Rank.C;
        return Rank.F;
    }

    /// <summary>
    /// 根据打击偏移计算打击结果
    /// </summary>
    /// <param name="DeltaMs"> 时间差（ms）</param>
    /// <returns> 打击结果 </returns>
    public Judgement Evaluate(float DeltaMs)
    {
        float abs = Mathf.Abs(DeltaMs);
        if (abs <= _judgementTimeSo.PerfectWindow) return Judgement.Perfect;
        if (abs <= _judgementTimeSo.GreatWindow) return Judgement.Great;
        if (abs <= _judgementTimeSo.GoodWindow) return Judgement.Good;
        if (abs <= _judgementTimeSo.BadWindow) return Judgement.Bad;
        // 如果超出判定时间范围则自动判定，并判定为Miss
        return Judgement.Miss;
    }
}