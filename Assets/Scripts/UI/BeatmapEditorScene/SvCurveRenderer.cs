using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 将 SVTimePoint 列表渲染为竖向阶梯折线图，显示在 RawImage 上。
/// Y 轴 = 时间（顶部 = time=0），X 轴 = SV 值（中心线 = SV 1.0）。
/// </summary>
[RequireComponent(typeof(RawImage))]
public class SvCurveRenderer : MonoBehaviour
{
    [Header("纹理")]
    public int textureWidth = 128;
    public int maxTextureHeight = 4096;

    [Header("SV 显示范围")]
    [Tooltip("X 轴显示的 SV 值范围（以 1.0 为中心）")]
    public float svDisplayHalfRange = 1.5f;

    [Header("颜色")]
    public Color backgroundColor = new Color(0.07f, 0.07f, 0.10f, 1f);
    public Color centerLineColor = new Color(0.20f, 0.35f, 0.40f, 1f);
    public Color curveColor = new Color(0.20f, 0.85f, 0.95f, 1f);
    public Color transitionColor = new Color(0.70f, 0.95f, 1.00f, 1f);

    private RawImage _rawImage;
    private Texture2D _texture;

    private void Awake() => _rawImage = GetComponent<RawImage>();
    private void OnDestroy() { if (_texture != null) Destroy(_texture); }

    // ─────────────────────────────────────────────────
    //  公开 API
    // ─────────────────────────────────────────────────

    /// <summary>
    /// 渲染 SV 曲线。SV=1.0 显示在中心线位置。
    /// </summary>
    /// <param name="points">SVTimePoint 列表（按时间升序）</param>
    /// <param name="totalLengthMs">歌曲总时长（ms）</param>
    public void Render(List<SVTimePoint> points, int totalLengthMs)
    {
        if (points == null || totalLengthMs <= 0) return;

        int texH = Mathf.Clamp(totalLengthMs / 10, 64, maxTextureHeight);
        RebuildTexture(textureWidth, texH);

        Color[] pixels = new Color[textureWidth * texH];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = backgroundColor;

        // SV=1.0 中心线
        int centerX = textureWidth / 2;
        for (int y = 0; y < texH; y++) pixels[y * textureWidth + centerX] = centerLineColor;

        if (points.Count == 0)
        {
            // 没有 SV 关键帧：整段保持 SV=1.0，画中心线即可
            _texture.SetPixels(pixels);
            _texture.Apply();
            _rawImage.texture = _texture;
            return;
        }

        float minSv = 1f - svDisplayHalfRange;
        float maxSv = 1f + svDisplayHalfRange;

        // 在第一个关键帧之前默认 SV=1.0
        float prevSv = 1.0f;
        int prevX = SvToX(prevSv, minSv, maxSv);
        int prevEndY = points.Count > 0 ? TimeToY(points[0].Time, totalLengthMs, texH) : 0;

        // 绘制 time=0 到第一个关键帧之间的段（SV=1.0）
        for (int y = 0; y < prevEndY; y++)
        {
            int idx = y * textureWidth + prevX;
            if (idx >= 0 && idx < pixels.Length) pixels[idx] = curveColor;
        }

        for (int i = 0; i < points.Count; i++)
        {
            int curX = SvToX(points[i].SV, minSv, maxSv);
            int startY = TimeToY(points[i].Time, totalLengthMs, texH);
            int endY = (i + 1 < points.Count)
                ? TimeToY(points[i + 1].Time, totalLengthMs, texH)
                : texH - 1;

            // 垂直过渡线
            int lo = Mathf.Min(prevX, curX);
            int hi = Mathf.Max(prevX, curX);
            for (int x = lo; x <= hi; x++)
            {
                int idx = startY * textureWidth + Mathf.Clamp(x, 0, textureWidth - 1);
                if (idx >= 0 && idx < pixels.Length) pixels[idx] = transitionColor;
            }

            // 水平线段
            for (int y = startY; y <= Mathf.Min(endY, texH - 1); y++)
            {
                int idx = y * textureWidth + curX;
                if (idx >= 0 && idx < pixels.Length) pixels[idx] = curveColor;
            }

            prevX = curX;
        }

        _texture.SetPixels(pixels);
        _texture.Apply();
        _rawImage.texture = _texture;
    }

    // ─────────────────────────────────────────────────
    //  内部工具
    // ─────────────────────────────────────────────────

    private int SvToX(float sv, float minSv, float maxSv)
    {
        float t = (sv - minSv) / (maxSv - minSv);
        return Mathf.Clamp(Mathf.RoundToInt(t * (textureWidth - 3)) + 1, 0, textureWidth - 1);
    }

    private int TimeToY(int timeMs, int totalLengthMs, int texH)
        => Mathf.Clamp(Mathf.RoundToInt((float)timeMs / totalLengthMs * texH), 0, texH - 1);

    private void RebuildTexture(int w, int h)
    {
        if (_texture != null && _texture.width == w && _texture.height == h) return;
        if (_texture != null) Destroy(_texture);
        _texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };
    }
}
