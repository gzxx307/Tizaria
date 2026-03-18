using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "JudgementScoreSO", menuName = "Configs/JudgementScoreSO")]
public class JudgementScoreSO : ScriptableObject
{
    // Perfect 分数基线
    public int PerfectScore;
    // Great 分数基线
    public int GreatScore;
    // Good 分数基线
    public int GoodScore;
    // Bad 分数基线
    public int BadScore;
    // Miss 分数基线
    public int MissScore;
}