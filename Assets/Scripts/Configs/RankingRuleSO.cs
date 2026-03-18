using System;
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// 排名规则配置，定义了不同排名等级对应的分数阈值。
/// </summary>
[CreateAssetMenu(fileName = "RankingRuleSO", menuName = "Configs/RankingRuleSO")]
public class RankingRuleSO : ScriptableObject
{
    // T
    public int TScore;
    // S
    public int SScore;
    // A
    public int AScore;
    // B
    public int BScore;
    // C
    public int CScore;
    // F
    public int FScore;
}