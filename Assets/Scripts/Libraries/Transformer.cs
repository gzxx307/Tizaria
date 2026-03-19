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
    /// 将 0.0~1.0 的准确率转换为带两位小数的百分比字符串，例如 "98.75%"。
    /// </summary>
    public static string AccuracyToString(float accuracy)
    {
        return $"{accuracy * 100f:F2}%";
    }
}