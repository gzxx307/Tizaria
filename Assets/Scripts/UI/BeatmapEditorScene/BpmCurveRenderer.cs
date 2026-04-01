using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 将 BPMTimePoint 列表渲染为竖向阶梯折线图，显示在 RawImage 上。
/// Y 轴 = 时间（顶部 = time=0），X 轴 = BPM 值（左=最低，右=最高）。
/// </summary>
[RequireComponent(typeof(RawImage))]
public class BpmCurveRenderer : MonoBehaviour
{
    [Header("纹理")]
    public int textureWidth = 128;
    public int maxTextureHeight = 4096;

    [Header("颜色")]
    public Color backgroundColor = new Color(0.07f, 0.07f, 0.10f, 1f);
    public Color gridLineColor = new Color(0.15f, 0.15f, 0.20f, 1f);
    public Color curveColor = new Color(1.00f, 0.60f, 0.15f, 1f);
    public Color transitionColor = new Color(1.00f, 0.85f, 0.40f, 1f);

    private RawImage _rawImage;
    private Texture2D _texture;

    private void Awake() => _rawImage = GetComponent<RawImage>();
    private void OnDestroy() { if (_texture != null) Destroy(_texture); }

    // ─────────────────────────────────────────────────
    //  公开 API
    // ─────────────────────────────────────────────────

    /// <summary>
    /// 渲染 BPM 曲线。
    /// </summary>
    /// <param name="points">BPMTimePoint 列表（已按时间升序排序）</param>
    /// <param name="totalLengthMs">歌曲总时长（ms），决定纹理 Y 轴范围</param>
    public void Render(List<BPMTimePoint> points, int totalLengthMs)
    {
        if (points == null || points.Count == 0 || totalLengthMs <= 0) return;

        int texH = Mathf.Clamp(totalLengthMs / 10, 64, maxTextureHeight);
        RebuildTexture(textureWidth, texH);

        Color[] pixels = new Color[textureWidth * texH];
        FillBackground(pixels, texH, backgroundColor);

        // 计算 BPM 值域
        float minBpm = float.MaxValue, maxBpm = float.MinValue;
        foreach (var p in points) { if (p.BPM < minBpm) minBpm = p.BPM; if (p.BPM > maxBpm) maxBpm = p.BPM; }
        if (Mathf.Approximately(minBpm, maxBpm)) { minBpm -= 10f; maxBpm += 10f; }

        // 绘制参考网格（中线）
        int midX = textureWidth / 2;
        for (int y = 0; y < texH; y++) pixels[y * textureWidth + midX] = gridLineColor;

        // 绘制阶梯曲线
        int prevX = BpmToX(points[0].BPM, minBpm, maxBpm);

        for (int i = 0; i < points.Count; i++)
        {
            int curX = BpmToX(points[i].BPM, minBpm, maxBpm);
            int startY = TimeToY(points[i].Time, totalLengthMs, texH);
            int endY = (i + 1 < points.Count)
                ? TimeToY(points[i + 1].Time, totalLengthMs, texH)
                : texH - 1;

            // 垂直过渡线（BPM 跳变）
            if (i > 0)
            {
                int lo = Mathf.Min(prevX, curX);
                int hi = Mathf.Max(prevX, curX);
                for (int x = lo; x <= hi; x++)
                {
                    int idx = startY * textureWidth + Mathf.Clamp(x, 0, textureWidth - 1);
                    if (idx >= 0 && idx < pixels.Length) pixels[idx] = transitionColor;
                }
            }

            // 水平线段（当前 BPM 持续范围）
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

    private int BpmToX(float bpm, float minBpm, float maxBpm)
    {
        float t = (bpm - minBpm) / (maxBpm - minBpm);
        return Mathf.Clamp(Mathf.RoundToInt(t * (textureWidth - 3)) + 1, 0, textureWidth - 1);
    }

    private int TimeToY(int timeMs, int totalLengthMs, int texH)
        => Mathf.Clamp(Mathf.RoundToInt((float)timeMs / totalLengthMs * texH), 0, texH - 1);

    private void FillBackground(Color[] pixels, int texH, Color col)
    {
        for (int i = 0; i < pixels.Length; i++) pixels[i] = col;
    }

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
