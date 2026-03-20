using System;
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// 结算等级门槛
/// </summary>
[CreateAssetMenu(fileName = "RankingRuleSO", menuName = "Configs/RankingRuleSO")]
public class RankingRuleSO : ScriptableObject
{
    // T (AP)
    public int TScore = 10000000;
    // S
    public int SScore = 9500000;
    // A
    public int AScore = 9000000;
    // B
    public int BScore = 8000000;
    // C
    public int CScore = 7000000;
    // F
    public int FScore = 0;
}