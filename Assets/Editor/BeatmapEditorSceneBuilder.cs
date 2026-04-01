#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// 一键构建 BeatmapEditorScene 的 Center 面板层级。
/// 菜单：Tools / Tizaria / Build Editor Center Panel
/// </summary>
public static class BeatmapEditorSceneBuilder
{
    [MenuItem("Tools/Tizaria/Build Editor Center Panel")]
    public static void BuildCenterPanel()
    {
        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null) { Debug.LogError("[Builder] 找不到 Canvas"); return; }

        // ── 读取现有面板尺寸 ──
        float leftW = GetPanelWidth(canvas.transform, "Left", 245f);
        float rightW = GetPanelWidth(canvas.transform, "Right", 359f);
        float topH = GetPanelHeight(canvas.transform, "Top", 73.5f);

        // ── 删除旧 Center（如有）──
        var old = canvas.transform.Find("Center");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        // ══════════════════════════════════════════
        //  Center 根面板
        // ══════════════════════════════════════════
        var center = MakeUI("Center", canvas.transform);
        {
            var rt = center.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 1);
            rt.offsetMin = new Vector2(leftW, 0);
            rt.offsetMax = new Vector2(-rightW, -topH);

            var img = center.AddComponent<Image>();
            img.color = new Color(0.09f, 0.09f, 0.11f, 1f);
            img.raycastTarget = false;
        }

        // ══════════════════════════════════════════
        //  MainScrollView
        // ══════════════════════════════════════════
        var scrollGO = MakeUI("MainScrollView", center.transform);
        ScrollRect scroll;
        {
            Stretch(scrollGO.GetComponent<RectTransform>(), 0, 0, 0, 0);
            // 为 ScrollRect 挂背景（可选）
            var img = scrollGO.AddComponent<Image>();
            img.color = Color.clear;
            img.raycastTarget = true; // 接收滚轮事件

            scroll = scrollGO.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.scrollSensitivity = 40f;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = false;
        }

        // ── Viewport ──
        var viewport = MakeUI("Viewport", scrollGO.transform);
        {
            Stretch(viewport.GetComponent<RectTransform>(), 0, 0, 0, 0);
            var img = viewport.AddComponent<Image>();
            img.color = Color.clear;
            var mask = viewport.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            scroll.viewport = viewport.GetComponent<RectTransform>();
        }

        // ── Content（HorizontalLayoutGroup，竖向高度动态） ──
        var content = MakeUI("Content", viewport.transform);
        {
            var rt = content.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0, 5000f); // 由运行时脚本设置实际高度

            var hlg = content.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.UpperLeft;
            hlg.childForceExpandHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.spacing = 0;
            hlg.padding = new RectOffset(0, 0, 0, 0);

            scroll.content = rt;
        }

        // ══════════════════════════════════════════
        //  Content 内各竖向条带
        // ══════════════════════════════════════════

        // ── 1. 时间刻度尺 (TimeRulerView) ──
        var rulerGO = MakeUI("TimeRulerView", content.transform);
        {
            SetLE(rulerGO, preferredW: 40f);
            rulerGO.AddComponent<RawImage>().color = Color.white;
            rulerGO.AddComponent<TimeRulerRenderer>();
        }

        // ── 2. 波形条带 (WaveformView) ──
        var waveGO = MakeUI("WaveformView", content.transform);
        {
            SetLE(waveGO, preferredW: 50f);
            waveGO.AddComponent<RawImage>().color = Color.white;
            var wr = waveGO.AddComponent<WaveformRenderer>();
            wr.vertical = true;
            wr.amplitudeAxisResolution = 50;
        }

        // ── 3. BPM 曲线面板 ──
        var bpmPanel = MakeUI("BpmCurvePanel", content.transform);
        {
            SetLE(bpmPanel, preferredW: 90f);
            bpmPanel.AddComponent<Image>().color = new Color(0.07f, 0.07f, 0.10f);
        }
        {
            var bpmCurve = MakeUI("BpmCurveView", bpmPanel.transform);
            Stretch(bpmCurve.GetComponent<RectTransform>());
            bpmCurve.AddComponent<RawImage>().color = Color.white;
            bpmCurve.AddComponent<BpmCurveRenderer>();

            var bpmKF = MakeUI("BpmKeyframeContainer", bpmPanel.transform);
            Stretch(bpmKF.GetComponent<RectTransform>());
            var kfImg = bpmKF.AddComponent<Image>();
            kfImg.color = Color.clear;
            kfImg.raycastTarget = false;
        }

        // ── 4. SV 曲线面板 ──
        var svPanel = MakeUI("SvCurvePanel", content.transform);
        {
            SetLE(svPanel, preferredW: 90f);
            svPanel.AddComponent<Image>().color = new Color(0.07f, 0.07f, 0.10f);
        }
        {
            var svCurve = MakeUI("SvCurveView", svPanel.transform);
            Stretch(svCurve.GetComponent<RectTransform>());
            svCurve.AddComponent<RawImage>().color = Color.white;
            svCurve.AddComponent<SvCurveRenderer>();

            var svKF = MakeUI("SvKeyframeContainer", svPanel.transform);
            Stretch(svKF.GetComponent<RectTransform>());
            var kfImg = svKF.AddComponent<Image>();
            kfImg.color = Color.clear;
            kfImg.raycastTarget = false;
        }

        // ── 5. 音符网格面板 (NoteGridPanel) ──
        var noteGrid = MakeUI("NoteGridPanel", content.transform);
        {
            SetLE(noteGrid, flexW: 1f);
            noteGrid.AddComponent<Image>().color = new Color(0.07f, 0.07f, 0.09f);
        }
        {
            var beatGrid = MakeUI("BeatGridView", noteGrid.transform);
            Stretch(beatGrid.GetComponent<RectTransform>());
            beatGrid.AddComponent<RawImage>().color = Color.white;
            beatGrid.AddComponent<BeatGridRenderer>();

            var notes = MakeUI("NotesContainer", noteGrid.transform);
            Stretch(notes.GetComponent<RectTransform>());
            var nImg = notes.AddComponent<Image>();
            nImg.color = Color.clear;
            nImg.raycastTarget = true; // 接收音符放置点击
        }

        // ══════════════════════════════════════════
        //  Playhead（固定水平线，覆盖在 ScrollView 上）
        // ══════════════════════════════════════════
        var playhead = MakeUI("Playhead", center.transform);
        {
            var rt = playhead.GetComponent<RectTransform>();
            // 固定在面板高度 30% 处
            rt.anchorMin = new Vector2(0f, 0.3f);
            rt.anchorMax = new Vector2(1f, 0.3f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(0f, 2f);
            rt.anchoredPosition = Vector2.zero;

            var img = playhead.AddComponent<Image>();
            img.color = new Color(1f, 0.92f, 0.2f, 0.9f);
            img.raycastTarget = false;
        }

        // ── 保存场景 ──
        EditorUtility.SetDirty(canvas.gameObject);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log("[Builder] Center 面板构建完成！层级：" +
                  "Center > MainScrollView > Viewport > Content > " +
                  "[TimeRulerView | WaveformView | BpmCurvePanel | SvCurvePanel | NoteGridPanel]");
    }

    // ─────────────────────────────────────────────────
    //  辅助方法
    // ─────────────────────────────────────────────────

    static GameObject MakeUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    static void Stretch(RectTransform rt, float l = 0, float b = 0, float r = 0, float t = 0)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(l, b);
        rt.offsetMax = new Vector2(-r, -t);
    }

    static void SetLE(GameObject go, float preferredW = -1, float flexW = 0, float flexH = 1)
    {
        var le = go.AddComponent<LayoutElement>();
        if (preferredW >= 0) le.preferredWidth = preferredW;
        le.flexibleWidth = flexW;
        le.flexibleHeight = flexH;
    }

    static float GetPanelWidth(Transform canvasT, string name, float fallback)
    {
        var t = canvasT.Find(name);
        if (t == null) return fallback;
        var rt = t.GetComponent<RectTransform>();
        return rt != null ? rt.rect.width : fallback;
    }

    static float GetPanelHeight(Transform canvasT, string name, float fallback)
    {
        var t = canvasT.Find(name);
        if (t == null) return fallback;
        var rt = t.GetComponent<RectTransform>();
        return rt != null ? rt.rect.height : fallback;
    }
}
#endif
