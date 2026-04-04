using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 渲染竖向时间刻度尺：在 RawImage 纹理上绘制刻度线，并创建 TMP 文字标签。
/// Y 轴 = 时间（底部 = time=0，顶部 = time=end）。
/// 大刻度 = 整秒，中刻度 = 0.5 秒，小刻度 = 0.25 秒。
/// </summary>
[RequireComponent(typeof(RawImage))]
public class TimeRulerRenderer : MonoBehaviour
{
    [Header("纹理")]
    public int textureWidth     = 40;
    public int maxTextureHeight = 4096;

    [Header("颜色")]
    public Color backgroundColor = new Color(0.06f, 0.06f, 0.08f, 1f);
    public Color majorTickColor  = new Color(0.80f, 0.80f, 0.85f, 1f);  // 整秒
    public Color midTickColor    = new Color(0.50f, 0.50f, 0.55f, 1f);  // 0.5 秒
    public Color minorTickColor  = new Color(0.25f, 0.25f, 0.30f, 1f);  // 0.25 秒

    [Header("刻度线宽度（占纹理宽度的比例）")]
    [Range(0f, 1f)] public float majorTickLength = 1.0f;
    [Range(0f, 1f)] public float midTickLength   = 0.5f;
    [Range(0f, 1f)] public float minorTickLength = 0.3f;

    private RawImage  _rawImage;
    private Texture2D _texture;
    private Transform _labelContainer;

    private void Awake()
    {
        _rawImage = GetComponent<RawImage>();

        // 标签容器：覆盖在 RawImage 上方，用锚点定位
        var go = new GameObject("__Labels", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        _labelContainer = go.transform;
    }

    private void OnDestroy() { if (_texture != null) Destroy(_texture); }

    // ─────────────────────────────────────────────────
    //  公开 API
    // ─────────────────────────────────────────────────

    public void Render(int totalLengthMs)
    {
        if (totalLengthMs <= 0) return;

        int texH = Mathf.Clamp(totalLengthMs / 10, 64, maxTextureHeight);
        RebuildTexture(textureWidth, texH);

        Color[] pixels = new Color[textureWidth * texH];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = backgroundColor;

        float totalSec = totalLengthMs / 1000f;

        for (float t = 0f; t <= totalSec + 0.001f; t += 0.25f)
        {
            int y = SecToY(t, totalSec, texH);
            bool isMajor = Mathf.Approximately(t % 1f,   0f);
            bool isMid   = !isMajor && Mathf.Approximately(t % 0.5f, 0f);

            Color col;
            float lenRatio;
            if      (isMajor) { col = majorTickColor; lenRatio = majorTickLength; }
            else if (isMid)   { col = midTickColor;   lenRatio = midTickLength;   }
            else              { col = minorTickColor;  lenRatio = minorTickLength; }

            int tickLen = Mathf.RoundToInt(lenRatio * textureWidth);
            int xStart  = textureWidth - tickLen;

            for (int x = xStart; x < textureWidth; x++)
            {
                int idx = y * textureWidth + x;
                if (idx >= 0 && idx < pixels.Length)
                    pixels[idx] = col;
            }
        }

        _texture.SetPixels(pixels);
        _texture.Apply();
        _rawImage.texture = _texture;

        // ── 重建文字标签 ──
        RebuildLabels(totalSec);
    }

    // ─────────────────────────────────────────────────
    //  标签
    // ─────────────────────────────────────────────────

    private void RebuildLabels(float totalSec)
    {
        for (int i = _labelContainer.childCount - 1; i >= 0; i--)
            Destroy(_labelContainer.GetChild(i).gameObject);

        // 根据总时长选择标签间隔（秒）
        int stepSec = 1;
        if      (totalSec > 600) stepSec = 60;
        else if (totalSec > 300) stepSec = 30;
        else if (totalSec > 120) stepSec = 10;
        else if (totalSec > 60)  stepSec =  5;

        for (int t = 0; t <= (int)totalSec; t += stepSec)
        {
            float norm = Mathf.Clamp01((float)t / totalSec);
            SpawnLabel(t, norm);
        }
    }

    private void SpawnLabel(int sec, float normPos)
    {
        var go = new GameObject($"L{sec}", typeof(RectTransform));
        go.transform.SetParent(_labelContainer, false);

        // 用锚点定位：normPos=0 对应底部（time=0），normPos=1 对应顶部
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0f, normPos);
        rt.anchorMax        = new Vector2(1f, normPos);
        rt.pivot            = new Vector2(0f, 0.5f);
        rt.sizeDelta        = new Vector2(0f, 16f);
        rt.anchoredPosition = Vector2.zero;

        var txt = go.AddComponent<TextMeshProUGUI>();
        int m = sec / 60, s = sec % 60;
        txt.text          = $"{m}:{s:00}";
        txt.fontSize      = 10f;
        txt.color         = majorTickColor;
        txt.alignment     = TextAlignmentOptions.Left;
        txt.raycastTarget = false;
        txt.overflowMode  = TextOverflowModes.Overflow;
    }

    // ─────────────────────────────────────────────────
    //  内部工具
    // ─────────────────────────────────────────────────

    // time=0 → y=0（底部），time=end → y=texH-1（顶部）
    private int SecToY(float sec, float totalSec, int texH)
        => Mathf.Clamp(Mathf.RoundToInt(sec / totalSec * texH), 0, texH - 1);

    private void RebuildTexture(int w, int h)
    {
        if (_texture != null && _texture.width == w && _texture.height == h) return;
        if (_texture != null) Destroy(_texture);
        _texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode   = TextureWrapMode.Clamp,
        };
    }
}
