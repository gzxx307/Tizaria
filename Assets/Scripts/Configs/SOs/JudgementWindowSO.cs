using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 判定时间窗口
/// </summary>
[CreateAssetMenu(fileName = "JudgementWindowSO", menuName = "Configs/JudgementWindowSO")]
public class JudgementWindowSO : ScriptableObject
{
    // Perfect 判定时间窗口（ms）
    public int PerfectWindow = 22;
    // Great 判定时间窗口（ms）
    public int GreatWindow = 40;
    // Good 判定时间窗口（ms）
    public int GoodWindow = 73;
    // Bad 判定时间窗口（ms）
    public int BadWindow = 103;

}