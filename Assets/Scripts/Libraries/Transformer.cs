using System;
using System.Collections.Generic;
using UnityEngine;

public static class Transformer
{
    public static string DateTimeToString(DateTime dateTime)
    {
        return dateTime.ToString("yyyy-MM-dd HH:mm:ss");
    }

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
}