using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 谱面文件的读写工具类（纯静态，无副作用）。
///
/// 磁盘结构：
///   {persistentDataPath}/beatmaps/
///     {setId}/
///       beatmapset.json   ← BeatmapDataSet 元数据（各难度不含 Notes）
///       {mapId}.json      ← 单个 BeatmapData（含完整 Notes 列表）
///       cover.*           ← 曲绘（路径写在 CoverPath 字段里）
///       audio.*           ← 音频（路径写在 AudioPath 字段里）
/// </summary>
public static class BeatmapIO
{
    // ─────────────────────────────────────────────────
    //  路径常量
    // ─────────────────────────────────────────────────

    private const string BeatmapsFolderName = "beatmaps";
    private const string SetMetaFileName    = "beatmapset.json";

    /// <summary> 所有谱面集的根目录 </summary>
    public static string BeatmapsRoot
        => Path.Combine(Application.persistentDataPath, BeatmapsFolderName);

    /// <summary> 指定谱面集的目录 </summary>
    public static string GetSetDirectory(string setId)
        => Path.Combine(BeatmapsRoot, setId);

    /// <summary> 谱面集元数据文件路径 </summary>
    public static string GetSetMetaPath(string setId)
        => Path.Combine(GetSetDirectory(setId), SetMetaFileName);

    /// <summary> 单个难度数据文件路径 </summary>
    public static string GetMapPath(string setId, string mapId)
        => Path.Combine(GetSetDirectory(setId), $"{mapId}.json");

    // ─────────────────────────────────────────────────
    //  保存
    // ─────────────────────────────────────────────────

    /// <summary>
    /// 保存谱面集元数据。
    /// Beatmaps 列表中各难度的 Notes 会被临时清空以避免冗余写入，
    /// 难度的其余字段（难度值、BPM、列数等）保留在元数据文件中。
    /// </summary>
    public static void SaveSetMeta(BeatmapDataSet set)
    {
        if (set == null || string.IsNullOrEmpty(set.Id))
        {
            Debug.LogError("[BeatmapIO] SaveSetMeta: 谱面集或 ID 为空");
            return;
        }

        Directory.CreateDirectory(GetSetDirectory(set.Id));

        // 临时清空各难度的 Notes，避免元数据文件体积过大
        var savedNotes = new List<List<NoteData>>();
        if (set.Beatmaps != null)
        {
            foreach (var map in set.Beatmaps)
            {
                savedNotes.Add(map.Notes);
                map.Notes = new List<NoteData>();
            }
        }

        try
        {
            File.WriteAllText(GetSetMetaPath(set.Id), JsonUtility.ToJson(set, true));
        }
        catch (Exception e)
        {
            Debug.LogError($"[BeatmapIO] 保存谱面集元数据失败: {e.Message}");
        }

        // 恢复 Notes
        if (set.Beatmaps != null)
        {
            for (int i = 0; i < set.Beatmaps.Count; i++)
                set.Beatmaps[i].Notes = savedNotes[i];
        }
    }

    /// <summary>
    /// 保存单个难度的完整数据（含 Notes 列表）。
    /// </summary>
    public static void SaveBeatmapData(BeatmapData map, string setId)
    {
        if (map == null || string.IsNullOrEmpty(map.Id) || string.IsNullOrEmpty(setId))
        {
            Debug.LogError("[BeatmapIO] SaveBeatmapData: 参数不完整");
            return;
        }

        Directory.CreateDirectory(GetSetDirectory(setId));

        try
        {
            File.WriteAllText(GetMapPath(setId, map.Id), JsonUtility.ToJson(map, true));
        }
        catch (Exception e)
        {
            Debug.LogError($"[BeatmapIO] 保存难度 {map.Id} 失败: {e.Message}");
        }
    }

    /// <summary>
    /// 一次性保存谱面集元数据及其下所有难度。
    /// </summary>
    public static void SaveAll(BeatmapDataSet set)
    {
        if (set == null) return;
        SaveSetMeta(set);
        if (set.Beatmaps == null) return;
        foreach (var map in set.Beatmaps)
            SaveBeatmapData(map, set.Id);
    }

    // ─────────────────────────────────────────────────
    //  加载
    // ─────────────────────────────────────────────────

    /// <summary>
    /// 加载单个谱面集的元数据（各难度不含 Notes）。
    /// </summary>
    public static BeatmapDataSet LoadSetMeta(string setId)
    {
        string path = GetSetMetaPath(setId);
        if (!File.Exists(path))
        {
            Debug.LogWarning($"[BeatmapIO] 找不到谱面集元数据: {path}");
            return null;
        }

        try
        {
            return JsonUtility.FromJson<BeatmapDataSet>(File.ReadAllText(path));
        }
        catch (Exception e)
        {
            Debug.LogError($"[BeatmapIO] 加载谱面集 {setId} 失败: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// 加载单个难度的完整数据（含 Notes）。
    /// </summary>
    public static BeatmapData LoadBeatmapData(string setId, string mapId)
    {
        string path = GetMapPath(setId, mapId);
        if (!File.Exists(path))
        {
            Debug.LogWarning($"[BeatmapIO] 找不到难度数据: {path}");
            return null;
        }

        try
        {
            return JsonUtility.FromJson<BeatmapData>(File.ReadAllText(path));
        }
        catch (Exception e)
        {
            Debug.LogError($"[BeatmapIO] 加载难度 {mapId} 失败: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// 扫描 beatmaps 目录，加载所有谱面集的元数据（不含 Notes）。
    /// 用于初始化谱面列表显示。
    /// </summary>
    public static List<BeatmapDataSet> LoadAllSetMetas()
    {
        var result = new List<BeatmapDataSet>();
        if (!Directory.Exists(BeatmapsRoot)) return result;

        foreach (string dir in Directory.GetDirectories(BeatmapsRoot))
        {
            string setId = Path.GetFileName(dir);
            BeatmapDataSet set = LoadSetMeta(setId);
            if (set != null) result.Add(set);
        }

        return result;
    }

    /// <summary>
    /// 将指定谱面集目录下的所有难度文件（.json，排除元数据）
    /// 加载并追加到 set.Beatmaps 中。
    /// </summary>
    public static void LoadAllMapsIntoSet(BeatmapDataSet set)
    {
        if (set == null || string.IsNullOrEmpty(set.Id)) return;

        if (set.Beatmaps == null)
            set.Beatmaps = new List<BeatmapData>();

        // 记录已有 ID 防止重复
        var existingIds = new HashSet<string>();
        foreach (var m in set.Beatmaps)
            existingIds.Add(m.Id);

        string dir = GetSetDirectory(set.Id);
        if (!Directory.Exists(dir)) return;

        foreach (string file in Directory.GetFiles(dir, "*.json"))
        {
            if (Path.GetFileName(file) == SetMetaFileName) continue;

            string mapId = Path.GetFileNameWithoutExtension(file);
            if (existingIds.Contains(mapId)) continue;

            BeatmapData map = LoadBeatmapData(set.Id, mapId);
            if (map != null) set.Beatmaps.Add(map);
        }
    }

    // ─────────────────────────────────────────────────
    //  删除
    // ─────────────────────────────────────────────────

    /// <summary> 删除整个谱面集目录（含所有文件） </summary>
    public static void DeleteSet(string setId)
    {
        string dir = GetSetDirectory(setId);
        if (Directory.Exists(dir))
            Directory.Delete(dir, recursive: true);
    }

    /// <summary> 删除单个难度文件 </summary>
    public static void DeleteBeatmapData(string setId, string mapId)
    {
        string path = GetMapPath(setId, mapId);
        if (File.Exists(path))
            File.Delete(path);
    }

    // ─────────────────────────────────────────────────
    //  工具
    // ─────────────────────────────────────────────────

    /// <summary> 返回磁盘上所有谱面集的 ID 列表 </summary>
    public static List<string> GetAllSetIds()
    {
        var ids = new List<string>();
        if (!Directory.Exists(BeatmapsRoot)) return ids;

        foreach (string dir in Directory.GetDirectories(BeatmapsRoot))
            ids.Add(Path.GetFileName(dir));

        return ids;
    }

    /// <summary> 指定 ID 的谱面集目录是否存在 </summary>
    public static bool SetExists(string setId)
        => Directory.Exists(GetSetDirectory(setId));
}
