using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// BeatmapEditorScene 主控制器。
/// 挂载在场景中任意 GameObject 上（建议挂载到 Manager 对象）。
/// 负责连接所有 UI 元素、播放控制、BPM/SV 管理、撤销/重做。
/// </summary>
public class BeatmapEditorController : MonoBehaviour
{
    // ─────────────────────────────────────────────────
    //  内部状态
    // ─────────────────────────────────────────────────

    private AudioSource _audio;
    private bool _isPlaying;
    private int _currentTimeMs;
    private int _playStartTimeMs;   // 点击 Play 时的位置，Stop 返回此处

    private bool _snapEnabled = true;
    private int _beatDivision = 4;  // 拍号分母，范围 1~16
    private float _pixelsPerMs = 0.15f;
    private int _currentDiffIndex = 0;

    // Undo/Redo：每条记录 = (撤销动作, 重做动作)
    private readonly Stack<(Action undo, Action redo)> _undoStack = new();
    private readonly Stack<(Action undo, Action redo)> _redoStack = new();

    // ─────────────────────────────────────────────────
    //  UI 引用（在 Inspector 中手动绑定）
    // ─────────────────────────────────────────────────

    [Header("Top — 工具栏")]
    [SerializeField] private Button _exitBtn;
    [SerializeField] private Button _saveBtn;
    [SerializeField] private Button _saveAsBtn;
    [SerializeField] private Button _playBtn;
    [SerializeField] private Button _stopBtn;
    [SerializeField] private TextMeshProUGUI _bpmDisplay;
    [SerializeField] private TextMeshProUGUI _timeDisplay;

    [Header("Left — 编辑操作")]
    [SerializeField] private Button _undoBtn;
    [SerializeField] private Button _redoBtn;
    [SerializeField] private Button _snapToggle;
    [SerializeField] private TextMeshProUGUI _snapStateText;
    [SerializeField] private Button _beatDivBtn;
    [SerializeField] private TextMeshProUGUI _beatDivDown;
    [SerializeField] private Button _bpmBtn;
    [SerializeField] private Button _svBtn;
    [SerializeField] private TMP_InputField _bpmInput;
    [SerializeField] private TMP_InputField _svInput;
    [SerializeField] private TMP_InputField _diffDescInput;
    [SerializeField] private TMP_InputField _diffValInput;
    [SerializeField] private TMP_InputField _colCountInput;
    [SerializeField] private Button _changeDiffBtn;
    [SerializeField] private Button _newDiffBtn;

    [Header("Right — 谱面集元数据")]
    [SerializeField] private TMP_InputField _titleInput;
    [SerializeField] private TMP_InputField _artistInput;
    [SerializeField] private TMP_InputField _mapWriterInput;
    [SerializeField] private TMP_InputField _illustratorInput;
    [SerializeField] private TMP_InputField _descInput;
    [SerializeField] private Button _selectAudioBtn;
    [SerializeField] private Button _selectCoverBtn;
    [SerializeField] private TMP_InputField _previewStartInput;
    [SerializeField] private TMP_InputField _initialBpmInput;

    // ─────────────────────────────────────────────────
    //  生命周期
    // ─────────────────────────────────────────────────

    private void Awake()
    {
        _audio = GetComponent<AudioSource>();
        if (_audio == null) _audio = gameObject.AddComponent<AudioSource>();
        _audio.playOnAwake = false;

        WireListeners();
    }


    private void Start()
    {
        if (_beatDivDown != null) _beatDivDown.text = _beatDivision.ToString();
        UpdateSnapStateText();
        RefreshFromManager();
    }

    private void Update()
    {
        if (_isPlaying) TickPlayback();
        UpdateDisplays();
    }

    // ─────────────────────────────────────────────────
    //  连接监听器
    // ─────────────────────────────────────────────────

    private void WireListeners()
    {
        // Top
        Click(_exitBtn,   OnExit);
        Click(_saveBtn,   OnSave);
        Click(_saveAsBtn, OnSaveAs);
        Click(_playBtn,   OnPlay);
        Click(_stopBtn,   OnStop);

        // Left – 操作
        Click(_undoBtn, OnUndo);
        Click(_redoBtn, OnRedo);
        Click(_beatDivBtn, CycleBeatDivision);
        Click(_bpmBtn, OnAddBpmKeyframe);
        Click(_svBtn,  OnAddSvKeyframe);
        Click(_changeDiffBtn, OnChangeDifficulty);
        Click(_newDiffBtn,    OnNewDifficulty);

        Click(_snapToggle, () =>
        {
            _snapEnabled = !_snapEnabled;
            UpdateSnapStateText();
        });

        // Left – 难度字段（失焦时应用）
        EndEdit(_diffDescInput, v => Do(
            () => BeatmapEditorManager.Instance?.SetDifficultyDescription(v),
            () => BeatmapEditorManager.Instance?.SetDifficultyDescription(
                BeatmapEditorManager.Instance?.CurrentMap?.DifficultyDescription ?? "")));
        EndEdit(_diffValInput, v =>
        {
            if (!float.TryParse(v, out float d)) return;
            d = (float)Math.Round(d, 1);
            float old = BeatmapEditorManager.Instance?.CurrentMap?.Difficulty ?? d;
            Do(() => BeatmapEditorManager.Instance?.SetDifficulty(d),
               () => BeatmapEditorManager.Instance?.SetDifficulty(old));
        });
        EndEdit(_colCountInput, v =>
        {
            if (!int.TryParse(v, out int c)) return;
            int old = BeatmapEditorManager.Instance?.CurrentMap?.ColumnCount ?? c;
            Do(() => { BeatmapEditorManager.Instance?.SetColumnCount(c); },
               () => { BeatmapEditorManager.Instance?.SetColumnCount(old); });
        });
        EndEdit(_initialBpmInput, v =>
        {
            if (!float.TryParse(v, out float bpm) || bpm <= 0) return;
            var map = BeatmapEditorManager.Instance?.CurrentMap;
            float old = map?.BPMTimePoint?.Count > 0 ? map.BPMTimePoint[0].BPM : bpm;
            Do(() => BeatmapEditorManager.Instance?.SetBPMAt(0, bpm),
               () => BeatmapEditorManager.Instance?.SetBPMAt(0, old));
        });

        // Right – 谱面集字段
        EndEdit(_titleInput,       v => BeatmapEditorManager.Instance?.SetTitle(v));
        EndEdit(_artistInput,      v => BeatmapEditorManager.Instance?.SetArtist(v));
        EndEdit(_mapWriterInput,   v => BeatmapEditorManager.Instance?.SetMapWriter(v));
        EndEdit(_illustratorInput, v => BeatmapEditorManager.Instance?.SetIllustrator(v));
        EndEdit(_descInput,        v => BeatmapEditorManager.Instance?.SetDescription(v));
        EndEdit(_previewStartInput, v =>
        {
            if (int.TryParse(v, out int ms)) BeatmapEditorManager.Instance?.SetPreview(ms, 10000);
        });

        // Right – 文件选择
        Click(_selectAudioBtn, OnSelectAudio);
        Click(_selectCoverBtn, OnSelectCover);
    }

    // ─────────────────────────────────────────────────
    //  播放控制
    // ─────────────────────────────────────────────────

    private void OnPlay()
    {
        var clip = BeatmapEditorManager.Instance?.CurrentSet?.AudioClip;
        if (clip == null) { Debug.LogWarning("[Editor] 未加载音频"); return; }

        if (!_isPlaying)
        {
            _playStartTimeMs = _currentTimeMs;
            _audio.clip = clip;
            _audio.time = _currentTimeMs / 1000f;
            _audio.Play();
            _isPlaying = true;
            SetBtnText(_playBtn, "⏸");
        }
        else
        {
            _audio.Pause();
            _isPlaying = false;
            SetBtnText(_playBtn, "▶");
        }
    }

    private void OnStop()
    {
        _audio.Stop();
        _isPlaying = false;
        _currentTimeMs = _playStartTimeMs;
        SetBtnText(_playBtn, "▶");
    }

    private void TickPlayback()
    {
        if (!_audio.isPlaying) { _isPlaying = false; SetBtnText(_playBtn, "▶"); return; }
        _currentTimeMs = Mathf.RoundToInt(_audio.time * 1000f);
    }

    // ─────────────────────────────────────────────────
    //  显示更新
    // ─────────────────────────────────────────────────

    private void UpdateDisplays()
    {
        if (_timeDisplay != null)
            _timeDisplay.text = MsToString(_currentTimeMs);

        if (_bpmDisplay != null)
            _bpmDisplay.text = $"{GetBpmAt(_currentTimeMs):F1} BPM";
    }


    private void UpdateSnapStateText()
    {
        if (_snapStateText != null) _snapStateText.text = _snapEnabled ? "ON" : "OFF";
    }

    // ─────────────────────────────────────────────────
    //  文件操作
    // ─────────────────────────────────────────────────

    private void OnExit() => SceneManager.LoadScene("SongSelectScene");

    private void OnSave() => BeatmapEditorManager.Instance?.SaveAll();

    private void OnSaveAs()
    {
#if UNITY_EDITOR
        string folder = UnityEditor.EditorUtility.OpenFolderPanel("另存为……", "", "");
        if (string.IsNullOrEmpty(folder)) return;
        // TODO: 将谱面集目录完整复制到所选文件夹
        Debug.Log($"[Editor] 另存为：{folder}（功能待实现）");
#endif
    }

    private void OnSelectAudio()
    {
#if UNITY_EDITOR
        string path = UnityEditor.EditorUtility.OpenFilePanel("选择音频", "", "mp3,ogg,wav");
        if (string.IsNullOrEmpty(path)) return;
        BeatmapEditorManager.Instance?.SetAudioPath(path);
        StartCoroutine(CoLoadAudio(path));
#endif
    }

    private void OnSelectCover()
    {
#if UNITY_EDITOR
        string path = UnityEditor.EditorUtility.OpenFilePanel("选择封面图片", "", "png,jpg,jpeg");
        if (string.IsNullOrEmpty(path)) return;
        BeatmapEditorManager.Instance?.SetCoverPath(path);
        StartCoroutine(CoLoadCover(path));
#endif
    }

    private IEnumerator CoLoadAudio(string path)
    {
        string uri = "file:///" + path.Replace('\\', '/');
        using var req = UnityWebRequestMultimedia.GetAudioClip(uri, AudioType.UNKNOWN);
        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            var clip = DownloadHandlerAudioClip.GetContent(req);
            var set = BeatmapEditorManager.Instance?.CurrentSet;
            if (set != null) set.AudioClip = clip;

            int totalMs = Mathf.RoundToInt(clip.length * 1000f);
            if (BeatmapEditorManager.Instance?.CurrentMap != null)
                BeatmapEditorManager.Instance.SetTotalLength(totalMs);


            Debug.Log($"[Editor] 音频加载成功：{clip.name}，时长 {MsToString(totalMs)}");
        }
        else Debug.LogError($"[Editor] 音频加载失败：{req.error}");
    }

    private IEnumerator CoLoadCover(string path)
    {
        string uri = "file:///" + path.Replace('\\', '/');
        using var req = UnityWebRequestTexture.GetTexture(uri);
        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            var tex = DownloadHandlerTexture.GetContent(req);
            var set = BeatmapEditorManager.Instance?.CurrentSet;
            if (set != null) { set.CoverTexture = tex; Debug.Log("[Editor] 封面加载成功"); }
        }
        else Debug.LogError($"[Editor] 封面加载失败：{req.error}");
    }

    // ─────────────────────────────────────────────────
    //  Undo / Redo
    // ─────────────────────────────────────────────────

    /// <summary>执行一个可撤销的操作。</summary>
    private void Do(Action execute, Action undo, Action redo = null)
    {
        try { execute(); }
        catch (Exception e) { Debug.LogError($"[Editor] 操作失败：{e.Message}"); return; }
        _undoStack.Push((undo, redo ?? execute));
        _redoStack.Clear();
    }

    private void OnUndo()
    {
        if (_undoStack.Count == 0) return;
        var (undo, redo) = _undoStack.Pop();
        try { undo(); } catch (Exception e) { Debug.LogError($"[Editor] 撤销失败：{e.Message}"); return; }
        _redoStack.Push((undo, redo));

    }

    private void OnRedo()
    {
        if (_redoStack.Count == 0) return;
        var (undo, redo) = _redoStack.Pop();
        try { redo(); } catch (Exception e) { Debug.LogError($"[Editor] 重做失败：{e.Message}"); return; }
        _undoStack.Push((undo, redo));

    }

    // ─────────────────────────────────────────────────
    //  BPM / SV 关键帧
    // ─────────────────────────────────────────────────

    private void OnAddBpmKeyframe()
    {
        var m = BeatmapEditorManager.Instance;
        if (m?.CurrentMap == null) { Debug.LogWarning("[Editor] 请先加载难度"); return; }
        if (!float.TryParse(_bpmInput?.text, out float bpm) || bpm <= 0)
        { Debug.LogWarning("[Editor] 请在 BPMInput 输入有效 BPM 值"); return; }

        int t = _currentTimeMs;
        var existing = m.CurrentMap.BPMTimePoint.Find(p => p.Time == t);
        float oldBpm = existing?.BPM ?? -1f;
        bool wasNew = existing == null;

        Do(
            () => m.SetBPMAt(t, bpm),
            () => { if (wasNew) m.RemoveBPMAt(t); else if (oldBpm > 0) m.SetBPMAt(t, oldBpm); }
        );

    }

    private void OnAddSvKeyframe()
    {
        var m = BeatmapEditorManager.Instance;
        if (m?.CurrentMap == null) { Debug.LogWarning("[Editor] 请先加载难度"); return; }
        if (!float.TryParse(_svInput?.text, out float sv))
        { Debug.LogWarning("[Editor] 请在 SVInput 输入有效 SV 值"); return; }

        int t = _currentTimeMs;
        var existing = m.CurrentMap.SVTimePoint.Find(p => p.Time == t);
        float oldSv = existing?.SV ?? float.NaN;
        bool wasNew = existing == null;

        Do(
            () => m.SetSVAt(t, sv),
            () => { if (wasNew) m.RemoveSVAt(t); else if (!float.IsNaN(oldSv)) m.SetSVAt(t, oldSv); }
        );

    }

    // ─────────────────────────────────────────────────
    //  难度管理
    // ─────────────────────────────────────────────────

    private void OnChangeDifficulty()
    {
        var m = BeatmapEditorManager.Instance;
        var maps = m?.CurrentSet?.Beatmaps;
        if (maps == null || maps.Count == 0) return;

        _currentDiffIndex = (_currentDiffIndex + 1) % maps.Count;
        m.SelectMap(maps[_currentDiffIndex].Id);
        PopulateDifficultyFields();

    }

    private void OnNewDifficulty()
    {
        var m = BeatmapEditorManager.Instance;
        if (m?.CurrentSet == null) return;

        string desc = _diffDescInput?.text;
        if (string.IsNullOrWhiteSpace(desc)) desc = "Normal";

        float diff = 1f;
        if (_diffValInput != null) float.TryParse(_diffValInput.text, out diff);

        int cols = 4;
        if (_colCountInput != null) int.TryParse(_colCountInput.text, out cols);

        float initBpm = 120f;
        if (_initialBpmInput != null) float.TryParse(_initialBpmInput.text, out initBpm);
        if (initBpm <= 0) initBpm = 120f;

        m.NewMap(desc, (float)Math.Round(diff, 1), Mathf.Max(1, cols), initBpm);
        _currentDiffIndex = m.CurrentSet.Beatmaps.Count - 1;
        PopulateDifficultyFields();

    }

    // ─────────────────────────────────────────────────
    //  节拍细分
    // ─────────────────────────────────────────────────

    private void CycleBeatDivision()
    {
        _beatDivision = _beatDivision % 16 + 1;
        if (_beatDivDown != null) _beatDivDown.text = _beatDivision.ToString();
    }

    /// <summary>将毫秒时间吸附到最近的节拍细分点。</summary>
    public int SnapTime(int timeMs)
    {
        if (!_snapEnabled) return timeMs;
        float bpm = GetBpmAt(timeMs);
        if (bpm <= 0) return timeMs;
        float msSub = 60000f / bpm / _beatDivision;
        if (msSub < 1f) return timeMs;
        return Mathf.RoundToInt(timeMs / msSub) * Mathf.RoundToInt(msSub);
    }

    // ─────────────────────────────────────────────────
    //  全量刷新
    // ─────────────────────────────────────────────────

    /// <summary>读取 Manager 当前数据，刷新所有 UI 显示（加载谱面后调用）。</summary>
    public void RefreshFromManager()
    {
        PopulateSetFields();
        PopulateDifficultyFields();
    }

    // ─────────────────────────────────────────────────
    //  字段填充
    // ─────────────────────────────────────────────────

    private void PopulateSetFields()
    {
        var s = BeatmapEditorManager.Instance?.CurrentSet;
        if (s == null) return;
        SetField(_titleInput,        s.Title);
        SetField(_artistInput,       s.Artist);
        SetField(_mapWriterInput,    s.MapWriter);
        SetField(_illustratorInput,  s.Illustrator);
        SetField(_descInput,         s.Description);
        SetField(_previewStartInput, s.PreviewStartTime.ToString());
    }

    private void PopulateDifficultyFields()
    {
        var map = BeatmapEditorManager.Instance?.CurrentMap;
        if (map == null) return;
        SetField(_diffDescInput, map.DifficultyDescription);
        SetField(_diffValInput,  map.Difficulty.ToString("F1"));
        SetField(_colCountInput, map.ColumnCount.ToString());
        float initBpm = map.BPMTimePoint?.Count > 0 ? map.BPMTimePoint[0].BPM : 120f;
        SetField(_initialBpmInput, initBpm.ToString("F1"));
    }

    // ─────────────────────────────────────────────────
    //  工具方法
    // ─────────────────────────────────────────────────

    public int TotalLengthMs()
    {
        var set = BeatmapEditorManager.Instance?.CurrentSet;
        if (set?.AudioClip != null) return Mathf.RoundToInt(set.AudioClip.length * 1000f);
        var map = BeatmapEditorManager.Instance?.CurrentMap;
        if (map != null && map.TotalLength > 0) return map.TotalLength;
        return 60000;
    }

    private float GetBpmAt(int timeMs)
    {
        var map = BeatmapEditorManager.Instance?.CurrentMap;
        if (map?.BPMTimePoint == null || map.BPMTimePoint.Count == 0) return 120f;
        float bpm = map.BPMTimePoint[0].BPM;
        foreach (var p in map.BPMTimePoint)
        {
            if (p.Time <= timeMs) bpm = p.BPM;
            else break;
        }
        return bpm;
    }

    private static string MsToString(int ms)
    {
        ms = Mathf.Max(0, ms);
        int m = ms / 60000;
        int s = (ms % 60000) / 1000;
        int r = ms % 1000;
        return $"{m:00}:{s:00}.{r:000}";
    }


    private static void SetBtnText(Button btn, string text)
    {
        var t = btn?.GetComponentInChildren<TextMeshProUGUI>();
        if (t != null) t.text = text;
    }


    private static void Click(Button btn, UnityEngine.Events.UnityAction cb)
        { if (btn != null) btn.onClick.AddListener(cb); }

    private static void EndEdit(TMP_InputField field, UnityEngine.Events.UnityAction<string> cb)
        { if (field != null) field.onEndEdit.AddListener(cb); }

    private static void SetField(TMP_InputField field, string value)
        { if (field != null) field.SetTextWithoutNotify(value ?? ""); }
}
