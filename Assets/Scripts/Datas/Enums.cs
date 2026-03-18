using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 结算等级
/// </summary>
public enum Rank
{
    T = 0,
    S = 1,
    A = 2,
    B = 3,
    C = 4,
    F = 5,
}

/// <summary>
/// 音符类型
/// </summary>
public enum NoteType
{
    Tap = 0,
    Hold = 1,
}

/// <summary>
/// 判定等级
/// </summary>
public enum Judgement
{
    Perfect = 0,
    Great = 1,
    Good = 2,
    Bad = 3,
    Miss = 4,
}