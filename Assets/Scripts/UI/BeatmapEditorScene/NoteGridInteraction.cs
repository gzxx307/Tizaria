using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 处理谱面网格上的音符交互（挂载在 NoteGridPanel 的透明输入层上）：
/// - 左键单击  → 添加 Tap
/// - 左键拖动（空白处）→ 添加 Hold（从按下到松开位置）
/// - 左键拖动（已有音符上）→ 移动音符
/// - 右键单击  → 删除音符
/// - 鼠标悬停  → 显示半透明预放置预览
///
/// 组件要求：
/// 1. 挂载在 NoteGridPanel 的最后一个子 GameObject 上，该子物体需有 Image 组件。
/// 2. 该子 GameObject 的 RectTransform 须撑满 NoteGridPanel（anchorMin=0,0 anchorMax=1,1）。
/// 3. 在 Inspector 中绑定 _controller（BeatmapEditorController）和 _notesContainer。
/// </summary>
[RequireComponent(typeof(Image))]
public class NoteGridInteraction : MonoBehaviour,
    IPointerDownHandler, IPointerUpHandler,
    IPointerMoveHandler, IPointerExitHandler
{
    [Header("引用")]
    [SerializeField] private BeatmapEditorController _controller;
    [SerializeField] private RectTransform           _notesContainer;

    [Header("交互参数")]
    [Tooltip("拖动判定阈值（屏幕像素）")]
    [SerializeField] private float _dragThresholdPx = 8f;

    [Header("音符颜色")]
    [SerializeField] private Color _tapColor   = new Color(0.35f, 0.75f, 1.00f, 1.0f);
    [SerializeField] private Color _holdColor  = new Color(0.25f, 0.55f, 0.90f, 0.9f);
    [SerializeField] private Color _ghostColor = new Color(0.35f, 0.75f, 1.00f, 0.35f);

    // ─── 内部状态 ───

    private RectTransform _gridRT;
    private Camera        _uiCamera;   // ScreenSpaceOverlay 时为 null

    private GameObject    _ghostGO;
    private RectTransform _ghostRT;

    private bool          _pointerDown;
    private bool          _isDragging;
    private bool          _dragOnExisting;   // 拖动的是已有音符
    private Vector2       _pointerDownScreenPos;

    private int           _downTimeMs;
    private int           _downColumn;
    private NoteData      _dragTarget;       // 被拖动的音符
    private int           _dragOrigTime;
    private int           _dragOrigColumn;

    // noteId → 视觉 GameObject
    private readonly Dictionary<int, GameObject> _noteGOs = new();

    // ─────────────────────────────────────────────────
    //  初始化
    // ─────────────────────────────────────────────────

    private void Awake()
    {
        _gridRT = GetComponent<RectTransform>();

        // 透明输入层
        var img = GetComponent<Image>();
        img.color = Color.clear;
        img.raycastTarget = true;

        // 查找 UI Camera
        var canvas = GetComponentInParent<Canvas>();
        _uiCamera = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            ? canvas.worldCamera
            : null;

        BuildGhostNote();
    }

    private void BuildGhostNote()
    {
        _ghostGO = new GameObject("__GhostNote", typeof(RectTransform));
        _ghostGO.transform.SetParent(_notesContainer, false);

        _ghostRT = _ghostGO.GetComponent<RectTransform>();
        var img = _ghostGO.AddComponent<Image>();
        img.color = _ghostColor;
        img.raycastTarget = false;

        _ghostGO.SetActive(false);
    }

    // ─────────────────────────────────────────────────
    //  刷新音符视图（由控制器在数据更改后调用）
    // ─────────────────────────────────────────────────

    public void RefreshNotes()
    {
        foreach (var kv in _noteGOs)
            if (kv.Value != null) Destroy(kv.Value);
        _noteGOs.Clear();

        var m = BeatmapEditorManager.Instance?.CurrentMap;
        if (m?.Notes == null) return;

        foreach (var note in m.Notes)
            _noteGOs[note.Id] = CreateNoteVisual(note);

        // Ghost 始终保持在最顶层
        if (_ghostGO != null) _ghostGO.transform.SetAsLastSibling();
    }

    // ─────────────────────────────────────────────────
    //  音符视觉
    // ─────────────────────────────────────────────────

    private GameObject CreateNoteVisual(NoteData note)
    {
        var go = new GameObject($"N{note.Id}", typeof(RectTransform));
        go.transform.SetParent(_notesContainer, false);

        var img = go.AddComponent<Image>();
        img.color = note.IsHold ? _holdColor : _tapColor;
        img.raycastTarget = false;

        SetNoteRT(go.GetComponent<RectTransform>(), note.Column, note.Time, note.IsHold ? note.EndTime - note.Time : 0);
        return go;
    }

    /// <summary>
    /// 定位一个音符的 RectTransform（新坐标系：time=0 在底部）。
    /// </summary>
    private void SetNoteRT(RectTransform rt, int col, int timeMs, int durationMs)
    {
        var m    = BeatmapEditorManager.Instance?.CurrentMap;
        int cols = Mathf.Max(1, m?.ColumnCount ?? 4);
        float ppm = _controller.PixelsPerMs;

        float colFrac    = (float)col / cols;
        float colFracEnd = (float)(col + 1) / cols;
        float noteH      = durationMs > 0 ? Mathf.Max(4f, durationMs * ppm) : 8f;

        rt.anchorMin        = new Vector2(colFrac, 0f);
        rt.anchorMax        = new Vector2(colFracEnd, 0f);
        rt.pivot            = new Vector2(0.5f, 0f);
        rt.sizeDelta        = new Vector2(-4f, noteH);  // -4 水平内缩
        rt.anchoredPosition = new Vector2(0f, timeMs * ppm);
    }

    // ─────────────────────────────────────────────────
    //  坐标转换：屏幕 → 时间 / 轨道列
    // ─────────────────────────────────────────────────

    private bool ScreenToGrid(Vector2 screenPos, out int timeMs, out int col)
    {
        timeMs = 0; col = 0;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _gridRT, screenPos, _uiCamera, out Vector2 local))
            return false;

        Rect r  = _gridRT.rect;
        float nx = Mathf.Clamp01((local.x - r.xMin) / r.width);
        float ny = Mathf.Clamp01((local.y - r.yMin) / r.height);  // 0=底部(time=0)

        var m    = BeatmapEditorManager.Instance?.CurrentMap;
        int cols = Mathf.Max(1, m?.ColumnCount ?? 4);
        col = Mathf.Clamp(Mathf.FloorToInt(nx * cols), 0, cols - 1);

        int totalMs = _controller.TotalLengthMs();
        timeMs = _controller.SnapTime(Mathf.RoundToInt(ny * totalMs));
        return true;
    }

    /// <summary>查找在 (timeMs, col) 附近的音符（容差 = 10 像素对应的时间）。</summary>
    private NoteData FindNoteAt(int timeMs, int col)
    {
        var m = BeatmapEditorManager.Instance?.CurrentMap;
        if (m?.Notes == null) return null;

        float ppm = Mathf.Max(0.001f, _controller.PixelsPerMs);
        int tol   = Mathf.Max(50, Mathf.RoundToInt(10f / ppm));

        return m.Notes.Find(n =>
            n.Column == col &&
            n.Time <= timeMs + tol &&
            (n.IsHold ? n.EndTime : n.Time) >= timeMs - tol);
    }

    // ─────────────────────────────────────────────────
    //  指针事件
    // ─────────────────────────────────────────────────

    public void OnPointerDown(PointerEventData e)
    {
        if (!ScreenToGrid(e.position, out int timeMs, out int col)) return;

        // ── 右键：删除 ──
        if (e.button == PointerEventData.InputButton.Right)
        {
            var target = FindNoteAt(timeMs, col);
            if (target != null) DeleteNote(target);
            return;
        }

        // ── 左键：开始交互 ──
        _pointerDown          = true;
        _isDragging           = false;
        _dragOnExisting       = false;
        _pointerDownScreenPos = e.position;
        _downTimeMs           = timeMs;
        _downColumn           = col;

        _dragTarget = FindNoteAt(timeMs, col);
        if (_dragTarget != null)
        {
            _dragOrigTime   = _dragTarget.Time;
            _dragOrigColumn = _dragTarget.Column;
            _dragOnExisting = true;
        }

        UpdateGhost(timeMs, col, false);
    }

    public void OnPointerMove(PointerEventData e)
    {
        if (!ScreenToGrid(e.position, out int timeMs, out int col)) return;

        if (_pointerDown)
        {
            float moved = (e.position - _pointerDownScreenPos).magnitude;
            if (!_isDragging && moved > _dragThresholdPx)
                _isDragging = true;

            if (_isDragging)
            {
                if (_dragOnExisting && _dragTarget != null)
                {
                    // 预览移动
                    if (_noteGOs.TryGetValue(_dragTarget.Id, out var go) && go != null)
                        SetNoteRT(go.GetComponent<RectTransform>(), col, timeMs,
                                  _dragTarget.IsHold ? _dragTarget.EndTime - _dragTarget.Time : 0);
                    _ghostGO.SetActive(false);
                }
                else
                {
                    // 拖拽空白处 → Hold 预览
                    int startMs = Mathf.Min(_downTimeMs, timeMs);
                    int endMs   = Mathf.Max(_downTimeMs, timeMs);
                    UpdateGhost(startMs, _downColumn, true, endMs - startMs);
                }
                return;
            }
        }

        // 悬停预览（未按下）
        if (!_pointerDown)
            UpdateGhost(timeMs, col, false);
    }

    public void OnPointerUp(PointerEventData e)
    {
        if (!_pointerDown || e.button != PointerEventData.InputButton.Left)
        {
            _pointerDown = false;
            return;
        }
        _pointerDown = false;

        if (!ScreenToGrid(e.position, out int upTimeMs, out int upCol)) return;

        if (_isDragging)
        {
            if (_dragOnExisting && _dragTarget != null)
                FinalizeDrag(_dragTarget, upCol, upTimeMs);
            else
                PlaceHold(_downColumn, _downTimeMs, upTimeMs);
        }
        else
        {
            PlaceTap(_downColumn, _downTimeMs);
        }

        _isDragging     = false;
        _dragTarget     = null;
        _dragOnExisting = false;

        UpdateGhost(upTimeMs, upCol, false);
    }

    public void OnPointerExit(PointerEventData e)
    {
        if (!_pointerDown)
            _ghostGO.SetActive(false);
    }

    // ─────────────────────────────────────────────────
    //  音符操作
    // ─────────────────────────────────────────────────

    private void PlaceTap(int col, int timeMs)
    {
        var m = BeatmapEditorManager.Instance;
        if (m?.CurrentMap == null) return;
        var note = m.AddNote(col, timeMs);
        RefreshNotes();  // ID 可能因排序重建而变化，全量刷新
    }

    private void PlaceHold(int col, int startMs, int endMs)
    {
        int s = Mathf.Min(startMs, endMs);
        int e = Mathf.Max(startMs, endMs);
        if (s == e) { PlaceTap(col, s); return; }
        var m = BeatmapEditorManager.Instance;
        if (m?.CurrentMap == null) return;
        m.AddNote(col, s, e);
        RefreshNotes();
    }

    private void DeleteNote(NoteData note)
    {
        if (_noteGOs.TryGetValue(note.Id, out var go) && go != null) Destroy(go);
        _noteGOs.Remove(note.Id);
        BeatmapEditorManager.Instance?.RemoveNote(note.Id);
        // RemoveNote 会 RebuildNoteIds，所以全量刷新
        RefreshNotes();
    }

    private void FinalizeDrag(NoteData note, int col, int timeMs)
    {
        var m = BeatmapEditorManager.Instance;
        if (m?.CurrentMap == null) { RefreshNotes(); return; }

        int newEnd = note.IsHold ? timeMs + (note.EndTime - note.Time) : timeMs;
        m.UpdateNote(note.Id, col, timeMs, newEnd);
        RefreshNotes();
    }

    // ─────────────────────────────────────────────────
    //  Ghost（预放置预览）
    // ─────────────────────────────────────────────────

    private void UpdateGhost(int timeMs, int col, bool isHold, int durationMs = 0)
    {
        SetNoteRT(_ghostRT, col, timeMs, isHold ? durationMs : 0);
        _ghostGO.SetActive(true);
        _ghostGO.transform.SetAsLastSibling();
    }
}
