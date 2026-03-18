using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "JudgementTimeSO", menuName = "Configs/JudgementTimeSO")]
public class JudgementTimeSO : ScriptableObject
{
    // Perfect 判定时间窗口（ms）
    public int PerfectWindow;
    // Great 判定时间窗口（ms）
    public int GreatWindow;
    // Good 判定时间窗口（ms）
    public int GoodWindow;
    // Bad 判定时间窗口（ms）
    public int BadWindow;
    // Miss 判定时间窗口（ms），超过这个时间就算 Miss
    public int MissWindow;
}