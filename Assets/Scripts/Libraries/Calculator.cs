using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 分数与准确率计算工具（纯静态，无副作用）。
/// </summary>
public static class Calculator
{
    /// <summary>
    /// 计算每个判定等级对应的「单音符得分」列表，顺序对应 <see cref="Judgement"/> 枚举。
    /// <para>总分公式：严判 = 10,000,000 + NoteCount；普通 = 10,000,000</para>
    /// <para>单音符得分 = 总分 × 权重 / 权重之和 / NoteCount</para>
    /// </summary>
    /// <param name="noteCount">谱面物量</param>
    /// <param name="isStrict">是否严判</param>
    /// <returns>长度为 5 的列表 [Perfect, Great, Good, Bad, Miss]</returns>
    public static List<int> GetNoteScores(int noteCount, bool isStrict)
    {
        NoteScoreSO so = ConfigBus.Instance.NoteScore;
        
        int totalScore = isStrict ? 10_000_000 + noteCount : 10_000_000;
        int baseWeight = so.PerfectScore + so.GreatScore + so.GoodScore + so.BadScore + so.MissScore;

        // Miss 固定为 0 分，其余按权重线性分配
        return new List<int>(5)
        {
            totalScore * so.PerfectScore / baseWeight / noteCount,
            totalScore * so.GreatScore / baseWeight / noteCount,
            totalScore * so.GoodScore / baseWeight / noteCount,
            totalScore * so.BadScore / baseWeight / noteCount,
            0,
        };
    }

    /// <summary>
    /// 获取指定判定等级的单音符得分。
    /// </summary>
    public static int GetNoteScore(Judgement judgement, int noteCount, bool isStrict)
    {
        var scores = GetNoteScores(noteCount, isStrict);
        return scores[(int)judgement];
    }

    /// <summary>
    /// 返回当前谱面在指定判定配置下的理论最高分（全P时的满分）。
    /// </summary>
    public static int GetMaxScore(int noteCount, bool isStrict)
    {
        return isStrict ? 10_000_000 + noteCount : 10_000_000;
    }

    /// <summary>
    /// 根据各判定计数计算准确率（0.0 ~ 1.0）。
    /// 权重：Perfect=100%，Great=75%，Good=50%，Bad=25%，Miss=0%
    /// </summary>
    public static float CalculateAccuracy(int perfect, int great, int good, int bad, int miss)
    {
        int total = perfect + great + good + bad + miss;
        if (total == 0) return 1f;

        float weighted = perfect * 1.00f + great * 0.75f + good * 0.50f + bad * 0.25f;

        return weighted / total;
    }


}