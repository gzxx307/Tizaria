using System;
using System.Collections.Generic;
using UnityEngine;

public static class Transformer
{
    /// <summary>
    /// 将DateTime转换为String用于显示时间
    /// </summary>
    public static string DateTimeToString(DateTime dateTime)
    {
        return dateTime.ToString("yyyy-MM-dd HH:mm:ss");
    }

    /// <summary>
    /// 将string转换为DateTime（虽然一般用不到），解析失败则返回一个DateTime默认构造函数
    /// </summary>
    public static DateTime StringToDateTime(string dateTime)
    {
        try
        {
            return DateTime.ParseExact(dateTime, "yyyy-MM-dd HH:mm:ss",
                System.Globalization.CultureInfo.InvariantCulture);
        }
        catch
        {
            Debug.Log("解析时间出现错误");
            return new DateTime();
        }
    }
    
    /// <summary>
    /// 将acc转换为两位小数带百分号的字符串，例如 "79.90%"。
    /// </summary>
    public static string AccuracyToString(float accuracy)
    {
        return $"{accuracy * 100f:F2}%";
    }

    /// <summary>
    /// 将分数转换为长度为8的字符串，用于显示分数
    /// </summary>
    /// <param name="num"> 整形分数 </param>
    /// <returns> 字符串形式的分数 </returns>
    public static string ScoreToStringScore(int num)
    {
        string str = num.ToString();
        str = (8 - str.Length) * '0' + str;
        return str;
    }

    /// <summary>
    /// 将长度为八的字符串转换为整形分数，如果转换不了则返回0并报错
    /// </summary>
    /// <param name="str"> 字符串形式的分数 </param>
    /// <returns> 整形分数 </returns>
    public static int StringScoreToScore(string str)
    {
        if (int.TryParse(str, out int num))
        {
            return num;
        }
        else
        {
            Debug.LogError("Change string (" + str + ") to int occured an error!");
            return 0;
        }
    }
}