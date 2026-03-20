using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 分数基线（比例）
/// </summary>
[CreateAssetMenu(fileName = "NoteScoreSO", menuName = "Configs/NoteScoreSO")]
public class NoteScoreSO : ScriptableObject
{
    // Perfect 分数基线
    public int PerfectScore = 300;
    // Great 分数基线
    public int GreatScore = 200;
    // Good 分数基线
    public int GoodScore = 100;
    // Bad 分数基线
    public int BadScore = 50;
    // Miss 分数基线
    public int MissScore = 0;
}