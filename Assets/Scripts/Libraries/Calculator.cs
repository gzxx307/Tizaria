using System;
using System.Collections.Generic;
using UnityEngine;

public static class Calculator
{
    public static List<int> GetJudgementScore(int NoteCount, bool IsStrict, JudgementScoreSO so)
    {
        List<int> scoreList = new List<int>(5);
        int baseScore = so.PerfectScore + so.GreatScore + so.GoodScore + so.BadScore + so.MissScore;
        int totalScore = IsStrict ? 10000000 + NoteCount : 10000000;
        
        scoreList.Add(totalScore * so.PerfectScore / baseScore);
        scoreList.Add(totalScore * so.GreatScore / baseScore);
        scoreList.Add(totalScore * so.GoodScore / baseScore);
        scoreList.Add(totalScore * so.BadScore / baseScore);
        scoreList.Add(0);

        return scoreList;
    }
}