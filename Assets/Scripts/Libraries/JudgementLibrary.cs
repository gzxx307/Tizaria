using System;
using System.Collections.Generic;
using UnityEngine;

public static class JudgementLibrary
{
    /// <summary>
    /// 根据配置表计算等级
    /// </summary>
    /// <param name="Score"> 分数 </param>
    /// <param name="NoteCount"> 物量 </param>
    /// <param name="IsStrict"> 是否为严判 </param>
    /// <param name="IsStrictT"> 是否为严判理论值（严判 T），用于结算界面特殊标记 </param>
    /// <returns> 评级 </returns>
    public static Rank GetRank(int Score, int NoteCount, bool IsStrict, out bool IsStrictT)
    {
        RankingRuleSO RankingRuleSo = ConfigBus.Instance.RankingRule;
        
        IsStrictT = false;

        if (IsStrict && Score == RankingRuleSo.TScore + NoteCount)
        {
            IsStrictT = true;
            return Rank.T;
        }
        if (Score == RankingRuleSo.TScore) return Rank.T;
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
    /// <returns> 打击结果 </returns>
    public static Judgement Evaluate(float DeltaMs)
    {
        JudgementWindowSO judgementWindowSo = ConfigBus.Instance.JudgementWindow;
        
        float abs = Mathf.Abs(DeltaMs);
        if (abs <= judgementWindowSo.PerfectWindow) return Judgement.Perfect;
        if (abs <= judgementWindowSo.GreatWindow) return Judgement.Great;
        if (abs <= judgementWindowSo.GoodWindow) return Judgement.Good;
        if (abs <= judgementWindowSo.BadWindow) return Judgement.Bad;
        // 如果超出判定时间范围则自动判定，并判定为Miss
        return Judgement.Miss;
    }

    public static bool IsMissed(float deltaMis)
    {
        JudgementWindowSO judgementWindowSo = ConfigBus.Instance.JudgementWindow;
        
        return deltaMis > judgementWindowSo.BadWindow;
    }
}