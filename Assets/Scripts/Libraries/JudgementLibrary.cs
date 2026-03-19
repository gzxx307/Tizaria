using System;
using System.Collections.Generic;
using UnityEngine;

public static class JudgementLibrary
{
    /// <summary>
    /// 根据配置表计算等级
    /// <param name="Score"> 分数 </param>
    /// <param name="NoteCount"> 物量 </param>
    /// <param name="IsFullCombo"> 是否为全连 </param>
    /// <param name="IsStrict"> 是否为严判 </param>
    /// <param name="RankingRuleSo"> 结算等级配置SO </param>
    /// <returns> 评级 </returns>
    /// </summary>
    public static Rank GetRank(int Score, int NoteCount, bool IsFullCombo, bool IsStrict, RankingRuleSO RankingRuleSo)
    {
        // 如果不是严判，则不会到达理论值，直接将 NoteCount 置零以满足 T 级条件
        if (!IsStrict) NoteCount = 0; 
        if (Score == RankingRuleSo.TScore + NoteCount) return Rank.T;
        if (Score >= RankingRuleSo.SScore) return Rank.S;
        if (Score >= RankingRuleSo.AScore) return Rank.A;
        if (Score >= RankingRuleSo.BScore) return Rank.B;
        if (Score >= RankingRuleSo.CScore) return Rank.C;
        return Rank.F;
    }

    /// <summary>
    /// 根据打击偏移计算打击结果
    /// </summary>
    /// <param name="DeltaMs"> 时间差（ms）</param>
    /// <param name="JudgementTimeSo"> 打击判定范围配置SO </param>
    /// <returns> 打击结果 </returns>
    public static Judgement Evaluate(float DeltaMs, JudgementTimeSO JudgementTimeSo)
    {
        float abs = Mathf.Abs(DeltaMs);
        if (abs <= JudgementTimeSo.PerfectWindow) return Judgement.Perfect;
        if (abs <= JudgementTimeSo.GreatWindow) return Judgement.Great;
        if (abs <= JudgementTimeSo.GoodWindow) return Judgement.Good;
        if (abs <= JudgementTimeSo.BadWindow) return Judgement.Bad;
        // 如果超出判定时间范围则自动判定，并判定为Miss
        return Judgement.Miss;
    }
}