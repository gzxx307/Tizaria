using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
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

    // 关键帧标记模板（运行时生成）
    private GameObject _markerTemplate;

    // ─────────────────────────────────────────────────
    //  UI 引用
    // ─────────────────────────────────────────────────

    // Top
    private Button _exitBtn, _saveBtn, _saveAsBtn, _playBtn, _stopBtn;
    private Text _bpmDisplay, _timeDisplay;

    // Left
    private Button _undoBtn, _redoBtn;
    private Toggle _snapToggle;
    private Text _snapStateText;
    private Button _beatDivBtn, _beatDivUp, _beatDivDown;
    private Text _beatDivLabel;
    private Button _bpmBtn, _svBtn;
    private InputField _bpmInput, _svInput;
    private InputField _diffDescInput, _diffValInput, _colCountInput;
    private Button _changeDiffBtn, _newDiffBtn;

    // Right
    private InputField _titleInput, _artistInput, _mapWriterInput;
    private InputField _illustratorInput, _descInput;
    private Button _selectAudioBtn, _selectCoverBtn;
    private InputField _previewStartInput, _initialBpmInput;

    // Center
    private ScrollRect _scrollRect;
    private RectTransform _contentRT;
    private TimeRulerRenderer _timeRulerRenderer;
    private WaveformRenderer _waveformRenderer;
    private BpmCurveRenderer _bpmCurveRenderer;
    private SvCurveRenderer _svCurveRenderer;
    private BeatGridRenderer _beatGridRenderer;
    private RectTransform _bpmKFContainer, _svKFContainer;

    // ─────────────────────────────────────────────────
    //  生命周期
    // ─────────────────────────────────────────────────

    private void Awake()
    {
        _audio = GetComponent<AudioSource>();
        if (_audio == null) _audio = gameObject.AddComponent<AudioSource>();
        _audio.playOnAwake = false;

        FindUIReferences();
        WireListeners();
    }

    private void Start()
    {
        BuildMarkerTemplate();
        UpdateBeatDivLabel();
        UpdateSnapStateText();
        RefreshFromManager();
    }

    private void Update()
    {
        if (_isPlaying) TickPlayback();
        UpdateDisplays();
        HandleZoomScroll();
    }

    // ─────────────────────────────────────────────────
    //  查找 UI 引用
    // ─────────────────────────────────────────────────

    private void FindUIReferences()
    {
        var canvas = FindObjectOfType<Canvas>().transform;

        // Top
        _exitBtn        = FindComp<Button>(canvas, "Top/ExitButton");
        _saveBtn        = FindComp<Button>(canvas, "Top/SaveButton");
        _saveAsBtn      = FindComp<Button>(canvas, "Top/SaveAsButton");
        _playBtn        = FindComp<Button>(canvas, "Top/PlayButton");
        _stopBtn        = FindComp<Button>(canvas, "Top/StopButton");
        _bpmDisplay     = FindComp<Text>(canvas, "Top/BPMDisplay");
        _timeDisplay    = FindComp<Text>(canvas, "Top/TimeDisplay");

        // Left
        _undoBtn        = FindComp<Button>(canvas, "Left/UndoButton");
        _redoBtn        = FindComp<Button>(canvas, "Left/RedoButton");
        _snapToggle     = FindComp<Toggle>(canvas, "Left/SnapToggle");
        _snapStateText  = FindComp<Text>(canvas, "Left/SnapToggle/SnapToggleStateText");
        _beatDivBtn     = FindComp<Button>(canvas, "Left/BeatDivisionButton");
        _beatDivUp      = FindComp<Button>(canvas, "Left/BeatDivisionButton/BeatDivisionButtonUp");
        _beatDivDown    = FindComp<Button>(canvas, "Left/BeatDivisionButton/BeatDivisionButtonDown");
        _beatDivLabel   = FindComp<Text>(canvas, "Left/BeatDivisionButton/BeatDivisionButtonDivide");
        _bpmBtn         = FindComp<Button>(canvas, "Left/BPMButton");
        _svBtn          = FindComp<Button>(canvas, "Left/SVButton");
        _bpmInput       = FindComp<InputField>(canvas, "Left/BPMButton/BPMInput");
        _svInput        = FindComp<InputField>(canvas, "Left/SVButton/SVInput");
        _diffDescInput  = FindComp<InputField>(canvas, "Left/DifficultyDescriptionInput");
        _diffValInput   = FindComp<InputField>(canvas, "Left/DifficultyInput");
        _colCountInput  = FindComp<InputField>(canvas, "Left/ColumnCountInput");
        _changeDiffBtn  = FindComp<Button>(canvas, "Left/ChangeDifficultyButton");
        _newDiffBtn     = FindComp<Button>(canvas, "Left/NewDifficultyButton");

        // Right
        _titleInput       = FindComp<InputField>(canvas, "Right/TitleInput");
        _artistInput      = FindComp<InputField>(canvas, "Right/ArtistInput");
        _mapWriterInput   = FindComp<InputField>(canvas, "Right/MapWriterInput");
        _illustratorInput = FindComp<InputField>(canvas, "Right/IllustratorInput");
        _descInput        = FindComp<InputField>(canvas, "Right/DescriptionInput");
        _selectAudioBtn   = FindComp<Button>(canvas, "Right/SelectAudioButton");
        _selectCoverBtn   = FindComp<Button>(canvas, "Right/CoverAudioButton");
        _previewStartInput = FindComp<InputField>(canvas, "Right/PreviewStartInput");
        _initialBpmInput  = FindComp<InputField>(canvas, "Right/InitialBPMInput");

        // Center
        _scrollRect        = FindComp<ScrollRect>(canvas, "Center/MainScrollView");
        _contentRT         = FindComp<RectTransform>(canvas, "Center/MainScrollView/Viewport/Content");
        _timeRulerRenderer = FindComp<TimeRulerRenderer>(canvas, "Center/MainScrollView/Viewport/Content/TimeRulerView");
        _waveformRenderer  = FindComp<WaveformRenderer>(canvas, "Center/MainScrollView/Viewport/Content/WaveformView");
        _bpmCurveRenderer  = FindComp<BpmCurveRenderer>(canvas, "Center/MainScrollView/Viewport/Content/BpmCurvePanel/BpmCurveView");
        _svCurveRenderer   = FindComp<SvCurveRenderer>(canvas, "Center/MainScrollView/Viewport/Content/SvCurvePanel/SvCurveView");
        _beatGridRenderer  = FindComp<BeatGridRenderer>(canvas, "Center/MainScrollView/Viewport/Content/NoteGridPanel/BeatGridView");
        _bpmKFContainer    = FindComp<RectTransform>(canvas, "Center/MainScrollView/Viewport/Content/BpmCurvePanel/BpmKeyframeContainer");
        _svKFContainer     = FindComp<RectTransform>(canvas, "Center/MainScrollView/Viewport/Content/SvCurvePanel/SvKeyframeContainer");
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
        Click(_beatDivBtn,  () => AdjustBeatDivision(+1));
        Click(_beatDivUp,   () => AdjustBeatDivision(+1));
        Click(_beatDivDown, () => AdjustBeatDivision(-1));
        Click(_bpmBtn, OnAddBpmKeyframe);
        Click(_svBtn,  OnAddSvKeyframe);
        Click(_changeDiffBtn, OnChangeDifficulty);
        Click(_newDiffBtn,    OnNewDifficulty);

        if (_snapToggle != null)
            _snapToggle.onValueChanged.AddListener(v =>
            {
                _snapEnabled = v;
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
            Do(() => { BeatmapEditorManager.Instance?.SetColumnCount(c); RefreshAll(); },
               () => { BeatmapEditorManager.Instance?.SetColumnCount(old); RefreshAll(); });
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
        ScrollToTime(_currentTimeMs);
    }

    private void TickPlayback()
    {
        if (!_audio.isPlaying) { _isPlaying = false; SetBtnText(_playBtn, "▶"); return; }
        _currentTimeMs = Mathf.RoundToInt(_audio.time * 1000f);
        ScrollToTime(_currentTimeMs);
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

    private void UpdateBeatDivLabel()
    {
        if (_beatDivLabel != null) _beatDivLabel.text = $"1/{_beatDivision}";
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

            RefreshAll();
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
        RefreshAll();
    }

    private void OnRedo()
    {
        if (_redoStack.Count == 0) return;
        var (undo, redo) = _redoStack.Pop();
        try { redo(); } catch (Exception e) { Debug.LogError($"[Editor] 重做失败：{e.Message}"); return; }
        _undoStack.Push((undo, redo));
        RefreshAll();
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
        RefreshAll();
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
        RefreshAll();
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
        RefreshAll();
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
        RefreshAll();
    }

    // ─────────────────────────────────────────────────
    //  节拍细分
    // ─────────────────────────────────────────────────

    private void AdjustBeatDivision(int delta)
    {
        _beatDivision = Mathf.Clamp(_beatDivision + delta, 1, 16);
        UpdateBeatDivLabel();
        if (_beatGridRenderer != null)
        {
            _beatGridRenderer.beatDivision = _beatDivision;
            RefreshRenderers();
        }
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
    //  时间轴滚动与缩放
    // ─────────────────────────────────────────────────

    private void HandleZoomScroll()
    {
        if (!Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.RightControl)) return;
        float delta = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(delta) < 0.001f) return;

        int pivot = ViewCenterTimeMs();
        _pixelsPerMs = Mathf.Clamp(_pixelsPerMs * (delta > 0 ? 1.25f : 0.8f), 0.01f, 3f);
        UpdateContentHeight();
        RefreshKeyframeMarkers(); // 位置随缩放变化
        ScrollToTime(pivot);
    }

    /// <summary>滚动时间轴使 timeMs 对准 Playhead（视口 30% 高度处）。</summary>
    public void ScrollToTime(int timeMs)
    {
        if (_scrollRect == null || _contentRT == null) return;

        Canvas.ForceUpdateCanvases();
        float contentH  = _contentRT.rect.height;
        float viewportH = _scrollRect.viewport.rect.height;
        float maxScroll = Mathf.Max(0f, contentH - viewportH);
        if (maxScroll <= 0f) return;

        // Playhead 在视口底部 30% 处（从底部算）
        float targetY = timeMs * _pixelsPerMs - viewportH * 0.3f;
        float norm = 1f - Mathf.Clamp01(targetY / maxScroll);
        _scrollRect.verticalNormalizedPosition = norm;
    }

    /// <summary>返回当前视口中央对应的时间（ms）。</summary>
    private int ViewCenterTimeMs()
    {
        if (_scrollRect == null || _contentRT == null) return _currentTimeMs;
        float contentH  = _contentRT.rect.height;
        float viewportH = _scrollRect.viewport.rect.height;
        float maxScroll = Mathf.Max(0f, contentH - viewportH);
        float scrollY   = (1f - _scrollRect.verticalNormalizedPosition) * maxScroll;
        return Mathf.RoundToInt((scrollY + viewportH * 0.5f) / _pixelsPerMs);
    }

    // ─────────────────────────────────────────────────
    //  全量刷新
    // ─────────────────────────────────────────────────

    /// <summary>读取 Manager 当前数据，刷新所有 UI 显示（加载谱面后调用）。</summary>
    public void RefreshFromManager()
    {
        PopulateSetFields();
        PopulateDifficultyFields();
        RefreshAll();
    }

    private void RefreshAll()
    {
        UpdateContentHeight();
        RefreshRenderers();
        RefreshKeyframeMarkers();
    }

    private void UpdateContentHeight()
    {
        if (_contentRT == null) return;
        float h = Mathf.Max(TotalLengthMs() * _pixelsPerMs, 500f);
        _contentRT.sizeDelta = new Vector2(0f, h);
    }

    private void RefreshRenderers()
    {
        var m = BeatmapEditorManager.Instance;
        int totalMs = TotalLengthMs();

        _timeRulerRenderer?.Render(totalMs);

        if (m?.CurrentSet?.AudioClip != null)
            _waveformRenderer?.RenderFull(m.CurrentSet.AudioClip);

        if (m?.CurrentMap != null)
        {
            _bpmCurveRenderer?.Render(m.CurrentMap.BPMTimePoint, totalMs);
            _svCurveRenderer?.Render(m.CurrentMap.SVTimePoint, totalMs);
            _beatGridRenderer?.Render(m.CurrentMap.BPMTimePoint, totalMs, m.CurrentMap.ColumnCount);
        }
    }

    private void RefreshKeyframeMarkers()
    {
        var m = BeatmapEditorManager.Instance;
        if (m?.CurrentMap == null) return;

        ClearChildren(_bpmKFContainer);
        ClearChildren(_svKFContainer);

        int totalMs = TotalLengthMs();
        if (totalMs <= 0) return;

        foreach (var p in m.CurrentMap.BPMTimePoint)
            SpawnMarker(_bpmKFContainer, KeyframeMarker.MarkerType.BPM, p.Time, p.BPM);

        foreach (var p in m.CurrentMap.SVTimePoint)
            SpawnMarker(_svKFContainer, KeyframeMarker.MarkerType.SV, p.Time, p.SV);
    }

    private void SpawnMarker(RectTransform container, KeyframeMarker.MarkerType type, int timeMs, float value)
    {
        if (_markerTemplate == null || container == null) return;

        var go = Instantiate(_markerTemplate, container);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(0f, 20f);
        rt.anchoredPosition = new Vector2(0f, -(timeMs * _pixelsPerMs));

        var marker = go.GetComponent<KeyframeMarker>();
        marker.Init(type, timeMs, value);

        // 颜色区分：BPM = 橙，SV = 青
        var img = go.GetComponent<Image>();
        if (img != null)
            img.color = type == KeyframeMarker.MarkerType.BPM
                ? new Color(1.00f, 0.50f, 0.10f, 0.80f)
                : new Color(0.10f, 0.80f, 0.90f, 0.80f);

        go.SetActive(true);
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

    private int TotalLengthMs()
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

    private void BuildMarkerTemplate()
    {
        _markerTemplate = new GameObject("_MarkerTemplate", typeof(RectTransform));
        _markerTemplate.SetActive(false);
        DontDestroyOnLoad(_markerTemplate);

        _markerTemplate.AddComponent<Image>();
        _markerTemplate.AddComponent<Button>();
        _markerTemplate.AddComponent<KeyframeMarker>();

        // 标签
        var label = new GameObject("Label", typeof(RectTransform));
        label.transform.SetParent(_markerTemplate.transform, false);
        var lrt = label.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
        lrt.offsetMin = new Vector2(3f, 0f); lrt.offsetMax = Vector2.zero;
        var txt = label.AddComponent<Text>();
        txt.fontSize  = 11;
        txt.color     = Color.white;
        txt.alignment = TextAnchor.MiddleLeft;
        txt.raycastTarget = false;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private static void ClearChildren(RectTransform rt)
    {
        if (rt == null) return;
        for (int i = rt.childCount - 1; i >= 0; i--)
            Destroy(rt.GetChild(i).gameObject);
    }

    private static void SetBtnText(Button btn, string text)
    {
        var t = btn?.GetComponentInChildren<Text>();
        if (t != null) t.text = text;
    }

    private static T FindComp<T>(Transform root, string path) where T : Component
    {
        var t = root.Find(path);
        if (t == null) { Debug.LogWarning($"[Editor] 路径未找到：{path}"); return null; }
        var c = t.GetComponent<T>();
        if (c == null) Debug.LogWarning($"[Editor] 组件 {typeof(T).Name} 未找到：{path}");
        return c;
    }

    private static void Click(Button btn, UnityEngine.Events.UnityAction cb)
        { if (btn != null) btn.onClick.AddListener(cb); }

    private static void EndEdit(InputField field, UnityEngine.Events.UnityAction<string> cb)
        { if (field != null) field.onEndEdit.AddListener(cb); }

    private static void SetField(InputField field, string value)
        { if (field != null) field.SetTextWithoutNotify(value ?? ""); }
}
