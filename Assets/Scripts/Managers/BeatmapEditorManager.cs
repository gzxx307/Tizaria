using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// BeatmapEditorScene 的核心管理器，负责维护当前编辑会话的状态，
/// 并向 UI 层暴露所有谱面创建与编辑操作。
///
/// 职责边界：
///   • 本类只操作内存中的数据模型（BeatmapDataSet / BeatmapData）
///   • 文件读写全部委托给 BeatmapIO
///   • UI 脚本通过 BeatmapEditorManager.Instance 调用各方法
/// </summary>
public class BeatmapEditorManager : MonoBehaviour
{
    public static BeatmapEditorManager Instance { get; private set; }

    // ─────────────────────────────────────────────────
    //  当前编辑会话（只读属性，修改请通过方法）
    // ─────────────────────────────────────────────────

    /// <summary> 当前正在编辑的谱面集 </summary>
    public BeatmapDataSet CurrentSet { get; private set; }

    /// <summary> 当前正在编辑的难度 </summary>
    public BeatmapData CurrentMap { get; private set; }

    /// <summary> 当前编辑会话是否有未保存的修改 </summary>
    public bool IsDirty { get; private set; }

    // ─────────────────────────────────────────────────
    //  生命周期
    // ─────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // ─────────────────────────────────────────────────
    //  谱面集操作
    // ─────────────────────────────────────────────────

    /// <summary>
    /// 创建一个新的空白谱面集并进入编辑状态。
    /// </summary>
    public BeatmapDataSet NewSet(
        string title,
        string artist,
        string mapWriter    = "",
        string illustrator  = "",
        string description  = "")
    {
        var set = new BeatmapDataSet
        {
            Id             = Guid.NewGuid().ToString(),
            Title          = title,
            Artist         = artist,
            MapWriter      = mapWriter,
            Illustrator    = illustrator,
            Description    = description,
            Tags           = new List<string>(),
            Beatmaps       = new List<BeatmapData>(),
            CoverPath      = "",
            AudioPath      = "",
            PreviewStartTime  = 0,
            PreviewDuration   = 10000,
        };

        CurrentSet = set;
        CurrentMap = null;
        IsDirty    = true;

        Debug.Log($"[BeatmapEditorManager] 新建谱面集: {title} (id={set.Id})");
        return set;
    }

    /// <summary>
    /// 从磁盘加载一个现有谱面集（含元数据，不自动加载各难度 Notes）。
    /// </summary>
    /// <returns>加载成功返回 true</returns>
    public bool LoadSet(string setId)
    {
        BeatmapDataSet set = BeatmapIO.LoadSetMeta(setId);
        if (set == null)
        {
            Debug.LogError($"[BeatmapEditorManager] 加载谱面集失败: {setId}");
            return false;
        }

        CurrentSet = set;
        CurrentMap = null;
        IsDirty    = false;

        Debug.Log($"[BeatmapEditorManager] 已加载谱面集: {set.Title}");
        return true;
    }

    /// <summary>
    /// 保存当前谱面集元数据（各难度不含 Notes）。
    /// </summary>
    public void SaveCurrentSet()
    {
        if (CurrentSet == null)
        {
            Debug.LogWarning("[BeatmapEditorManager] 没有正在编辑的谱面集");
            return;
        }
        BeatmapIO.SaveSetMeta(CurrentSet);
        IsDirty = false;
        Debug.Log($"[BeatmapEditorManager] 谱面集已保存: {CurrentSet.Title}");
    }

    /// <summary>
    /// 保存当前谱面集的元数据以及所有难度（含 Notes）。
    /// </summary>
    public void SaveAll()
    {
        if (CurrentSet == null)
        {
            Debug.LogWarning("[BeatmapEditorManager] 没有正在编辑的谱面集");
            return;
        }
        BeatmapIO.SaveAll(CurrentSet);
        IsDirty = false;
        Debug.Log($"[BeatmapEditorManager] 全部数据已保存: {CurrentSet.Title}");
    }

    /// <summary>
    /// 删除当前谱面集（磁盘上的目录与所有文件一并删除）。
    /// 删除后 CurrentSet 置空。
    /// </summary>
    public void DeleteCurrentSet()
    {
        if (CurrentSet == null) return;
        BeatmapIO.DeleteSet(CurrentSet.Id);
        Debug.Log($"[BeatmapEditorManager] 谱面集已删除: {CurrentSet.Title}");
        CurrentSet = null;
        CurrentMap = null;
        IsDirty    = false;
    }

    // ─────────────────────────────────────────────────
    //  谱面集元数据编辑
    // ─────────────────────────────────────────────────

    public void SetTitle(string title)               { RequireSet(); CurrentSet.Title = title;               MarkDirty(); }
    public void SetArtist(string artist)             { RequireSet(); CurrentSet.Artist = artist;             MarkDirty(); }
    public void SetMapWriter(string mapWriter)       { RequireSet(); CurrentSet.MapWriter = mapWriter;       MarkDirty(); }
    public void SetIllustrator(string illustrator)  { RequireSet(); CurrentSet.Illustrator = illustrator;   MarkDirty(); }
    public void SetDescription(string description)  { RequireSet(); CurrentSet.Description = description;   MarkDirty(); }
    public void SetCoverPath(string path)            { RequireSet(); CurrentSet.CoverPath = path;            MarkDirty(); }
    public void SetAudioPath(string path)            { RequireSet(); CurrentSet.AudioPath = path;            MarkDirty(); }

    public void SetPreview(int startMs, int durationMs)
    {
        RequireSet();
        CurrentSet.PreviewStartTime = Mathf.Max(0, startMs);
        CurrentSet.PreviewDuration  = Mathf.Max(0, durationMs);
        MarkDirty();
    }

    public void AddTag(string tag)
    {
        RequireSet();
        if (CurrentSet.Tags == null) CurrentSet.Tags = new List<string>();
        if (!CurrentSet.Tags.Contains(tag)) { CurrentSet.Tags.Add(tag); MarkDirty(); }
    }

    public void RemoveTag(string tag)
    {
        RequireSet();
        if (CurrentSet.Tags != null && CurrentSet.Tags.Remove(tag)) MarkDirty();
    }

    // ─────────────────────────────────────────────────
    //  难度操作
    // ─────────────────────────────────────────────────

    /// <summary>
    /// 在当前谱面集下创建一个新难度并进入编辑状态。
    /// </summary>
    public BeatmapData NewMap(
        string difficultyDescription,
        float  difficulty,
        int    columnCount,
        float  initialBpm  = 120f)
    {
        RequireSet();

        string now = Transformer.DateTimeToString(DateTime.Now);
        var map = new BeatmapData
        {
            Id                    = Guid.NewGuid().ToString(),
            SetId                 = CurrentSet.Id,
            MapWriter             = CurrentSet.MapWriter,
            Version               = "1.0",
            CreationTime          = now,
            LastUpdateTime        = now,
            DifficultyDescription = difficultyDescription,
            Difficulty            = difficulty,
            ColumnCount           = Mathf.Max(1, columnCount),
            MinBPM                = initialBpm,
            MaxBPM                = initialBpm,
            TotalLength           = 0,
            BPMTimePoint          = new List<BPMTimePoint>
            {
                new BPMTimePoint { Time = 0, BPM = initialBpm, Numerator = 4, Denominator = 4 }
            },
            SVTimePoint           = new List<SVTimePoint>(),
            Notes                 = new List<NoteData>(),
            NoteCount             = 0,
        };

        CurrentSet.Beatmaps.Add(map);
        CurrentMap = map;
        MarkDirty();

        Debug.Log($"[BeatmapEditorManager] 新建难度: {difficultyDescription} Lv{difficulty}");
        return map;
    }

    /// <summary>
    /// 切换到当前谱面集中指定 ID 的难度。
    /// 若该难度的 Notes 尚未从磁盘加载，则自动加载。
    /// </summary>
    /// <returns>切换成功返回 true</returns>
    public bool SelectMap(string mapId)
    {
        RequireSet();

        BeatmapData map = CurrentSet.Beatmaps?.Find(m => m.Id == mapId);
        if (map == null)
        {
            Debug.LogWarning($"[BeatmapEditorManager] 未找到难度: {mapId}");
            return false;
        }

        // 如果 Notes 尚未加载（磁盘存在文件），则懒加载
        if (map.Notes == null && BeatmapIO.SetExists(CurrentSet.Id))
        {
            BeatmapData full = BeatmapIO.LoadBeatmapData(CurrentSet.Id, mapId);
            if (full != null) map.Notes = full.Notes;
        }

        if (map.Notes == null) map.Notes = new List<NoteData>();

        CurrentMap = map;
        return true;
    }

    /// <summary>
    /// 保存当前难度（含完整 Notes 列表）到磁盘。
    /// </summary>
    public void SaveCurrentMap()
    {
        RequireMap();
        CurrentMap.LastUpdateTime = Transformer.DateTimeToString(DateTime.Now);
        BeatmapIO.SaveBeatmapData(CurrentMap, CurrentSet.Id);
        Debug.Log($"[BeatmapEditorManager] 难度已保存: {CurrentMap.DifficultyDescription}");
    }

    /// <summary>
    /// 从谱面集中移除当前难度，并删除对应的磁盘文件。
    /// </summary>
    public void RemoveCurrentMap()
    {
        RequireMap();
        CurrentSet.Beatmaps.Remove(CurrentMap);
        BeatmapIO.DeleteBeatmapData(CurrentSet.Id, CurrentMap.Id);
        Debug.Log($"[BeatmapEditorManager] 难度已移除: {CurrentMap.DifficultyDescription}");
        CurrentMap = null;
        MarkDirty();
    }

    // ─────────────────────────────────────────────────
    //  难度元数据编辑
    // ─────────────────────────────────────────────────

    public void SetDifficultyDescription(string desc)
    {
        RequireMap();
        CurrentMap.DifficultyDescription = desc;
        MarkDirty();
    }

    public void SetDifficulty(float difficulty)
    {
        RequireMap();
        CurrentMap.Difficulty = difficulty;
        MarkDirty();
    }

    public void SetColumnCount(int count)
    {
        RequireMap();
        CurrentMap.ColumnCount = Mathf.Max(1, count);
        MarkDirty();
    }

    public void SetTotalLength(int totalMs)
    {
        RequireMap();
        CurrentMap.TotalLength = Mathf.Max(0, totalMs);
        MarkDirty();
    }

    // ─────────────────────────────────────────────────
    //  音符操作
    // ─────────────────────────────────────────────────

    /// <summary>
    /// 在指定列和时间添加一个音符。
    /// endTimeMs &lt; 0 或等于 timeMs 时创建 Tap，否则创建 Hold。
    /// </summary>
    /// <returns>添加后的 NoteData（已分配 ID）</returns>
    public NoteData AddNote(int column, int timeMs, int endTimeMs = -1)
    {
        RequireMap();

        bool isHold = endTimeMs > timeMs;
        var note = new NoteData
        {
            Column  = Mathf.Clamp(column, 0, CurrentMap.ColumnCount - 1),
            Time    = Mathf.Max(0, timeMs),
            EndTime = isHold ? endTimeMs : timeMs,
            Type    = isHold ? NoteType.Hold : NoteType.Tap,
        };

        CurrentMap.Notes.Add(note);
        RebuildNoteIds();
        UpdateBpmRange();
        MarkDirty();

        return note;
    }

    /// <summary>
    /// 删除指定 ID 的音符。
    /// </summary>
    /// <returns>删除成功返回 true</returns>
    public bool RemoveNote(int noteId)
    {
        RequireMap();

        int idx = CurrentMap.Notes.FindIndex(n => n.Id == noteId);
        if (idx < 0)
        {
            Debug.LogWarning($"[BeatmapEditorManager] 未找到音符 ID={noteId}");
            return false;
        }

        CurrentMap.Notes.RemoveAt(idx);
        RebuildNoteIds();
        MarkDirty();
        return true;
    }

    /// <summary>
    /// 修改指定 ID 音符的列与时间信息。
    /// </summary>
    /// <returns>更新成功返回 true</returns>
    public bool UpdateNote(int noteId, int column, int timeMs, int endTimeMs = -1)
    {
        RequireMap();

        NoteData note = CurrentMap.Notes.Find(n => n.Id == noteId);
        if (note == null)
        {
            Debug.LogWarning($"[BeatmapEditorManager] 未找到音符 ID={noteId}");
            return false;
        }

        bool isHold = endTimeMs > timeMs;
        note.Column  = Mathf.Clamp(column, 0, CurrentMap.ColumnCount - 1);
        note.Time    = Mathf.Max(0, timeMs);
        note.EndTime = isHold ? endTimeMs : timeMs;
        note.Type    = isHold ? NoteType.Hold : NoteType.Tap;

        RebuildNoteIds();
        MarkDirty();
        return true;
    }

    /// <summary>
    /// 清空当前难度的所有音符。
    /// </summary>
    public void ClearAllNotes()
    {
        RequireMap();
        CurrentMap.Notes.Clear();
        CurrentMap.NoteCount = 0;
        MarkDirty();
    }

    /// <summary>
    /// 返回指定时间范围内的所有音符（按 Time 排序）。
    /// </summary>
    public List<NoteData> GetNotesInRange(int startMs, int endMs)
    {
        RequireMap();
        return CurrentMap.Notes.FindAll(n => n.Time >= startMs && n.Time <= endMs);
    }

    // ─────────────────────────────────────────────────
    //  BPM 关键帧操作
    // ─────────────────────────────────────────────────

    /// <summary>
    /// 在指定时间设置一个 BPM 关键帧。若该时间已有关键帧则覆盖。
    /// </summary>
    public void SetBPMAt(int timeMs, float bpm, int numerator = 4, int denominator = 4)
    {
        RequireMap();

        var existing = CurrentMap.BPMTimePoint.Find(p => p.Time == timeMs);
        if (existing != null)
        {
            existing.BPM         = bpm;
            existing.Numerator   = numerator;
            existing.Denominator = denominator;
        }
        else
        {
            CurrentMap.BPMTimePoint.Add(new BPMTimePoint
            {
                Time        = timeMs,
                BPM         = bpm,
                Numerator   = numerator,
                Denominator = denominator,
            });
            CurrentMap.BPMTimePoint.Sort((a, b) => a.Time.CompareTo(b.Time));
        }

        UpdateBpmRange();
        MarkDirty();
    }

    /// <summary>
    /// 删除指定时间的 BPM 关键帧。不允许删除 Time==0 的初始关键帧。
    /// </summary>
    /// <returns>删除成功返回 true</returns>
    public bool RemoveBPMAt(int timeMs)
    {
        RequireMap();
        if (timeMs == 0)
        {
            Debug.LogWarning("[BeatmapEditorManager] 不能删除 Time=0 的 BPM 关键帧");
            return false;
        }

        int removed = CurrentMap.BPMTimePoint.RemoveAll(p => p.Time == timeMs);
        if (removed > 0) { UpdateBpmRange(); MarkDirty(); }
        return removed > 0;
    }

    // ─────────────────────────────────────────────────
    //  SV 关键帧操作
    // ─────────────────────────────────────────────────

    /// <summary>
    /// 在指定时间设置一个 SV（滚轮速度）关键帧。若该时间已有关键帧则覆盖。
    /// </summary>
    public void SetSVAt(int timeMs, float sv)
    {
        RequireMap();

        var existing = CurrentMap.SVTimePoint.Find(p => p.Time == timeMs);
        if (existing != null)
        {
            existing.SV = sv;
        }
        else
        {
            CurrentMap.SVTimePoint.Add(new SVTimePoint { Time = timeMs, SV = sv });
            CurrentMap.SVTimePoint.Sort((a, b) => a.Time.CompareTo(b.Time));
        }

        MarkDirty();
    }

    /// <summary>
    /// 删除指定时间的 SV 关键帧。
    /// </summary>
    /// <returns>删除成功返回 true</returns>
    public bool RemoveSVAt(int timeMs)
    {
        RequireMap();
        int removed = CurrentMap.SVTimePoint.RemoveAll(p => p.Time == timeMs);
        if (removed > 0) MarkDirty();
        return removed > 0;
    }

    // ─────────────────────────────────────────────────
    //  内部工具
    // ─────────────────────────────────────────────────

    /// <summary>
    /// 按时间（升序）、再按列（升序）对音符排序，
    /// 然后重新分配连续 ID（0-based）并更新 NoteCount。
    /// </summary>
    private void RebuildNoteIds()
    {
        CurrentMap.Notes.Sort((a, b) =>
            a.Time != b.Time ? a.Time.CompareTo(b.Time) : a.Column.CompareTo(b.Column));

        for (int i = 0; i < CurrentMap.Notes.Count; i++)
            CurrentMap.Notes[i].Id = i;

        CurrentMap.NoteCount = CurrentMap.Notes.Count;
    }

    /// <summary>
    /// 根据 BPMTimePoint 列表重新计算并更新 MinBPM / MaxBPM。
    /// </summary>
    private void UpdateBpmRange()
    {
        if (CurrentMap.BPMTimePoint == null || CurrentMap.BPMTimePoint.Count == 0) return;

        float min = float.MaxValue;
        float max = float.MinValue;
        foreach (var p in CurrentMap.BPMTimePoint)
        {
            if (p.BPM < min) min = p.BPM;
            if (p.BPM > max) max = p.BPM;
        }
        CurrentMap.MinBPM = min;
        CurrentMap.MaxBPM = max;
    }

    private void MarkDirty() => IsDirty = true;

    private void RequireSet()
    {
        if (CurrentSet == null)
            throw new InvalidOperationException("[BeatmapEditorManager] 当前没有打开的谱面集");
    }

    private void RequireMap()
    {
        RequireSet();
        if (CurrentMap == null)
            throw new InvalidOperationException("[BeatmapEditorManager] 当前没有选中的难度");
    }
}
