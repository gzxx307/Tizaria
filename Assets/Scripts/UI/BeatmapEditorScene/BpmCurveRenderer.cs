using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 将 BPMTimePoint 列表渲染为竖向线性折线图，显示在 RawImage 上。
/// Y 轴 = 时间（底部 = time=0，顶部 = time=end），X 轴 = BPM 值（左=最低，右=最高）。
/// 相邻关键帧之间用直线连接（线性插值）。
/// </summary>
[RequireComponent(typeof(RawImage))]
public class BpmCurveRenderer : MonoBehaviour
{
    [Header("纹理")]
    public int textureWidth   = 128;
    public int maxTextureHeight = 4096;

    [Header("颜色")]
    public Color backgroundColor = new Color(0.07f, 0.07f, 0.10f, 1f);
    public Color gridLineColor   = new Color(0.15f, 0.15f, 0.20f, 1f);
    public Color curveColor      = new Color(1.00f, 0.60f, 0.15f, 1f);

    private RawImage  _rawImage;
    private Texture2D _texture;

    private void Awake()     => _rawImage = GetComponent<RawImage>();
    private void OnDestroy() { if (_texture != null) Destroy(_texture); }

    // ─────────────────────────────────────────────────
    //  公开 API
    // ─────────────────────────────────────────────────

    /// <summary>渲染 BPM 曲线（线性插值）。</summary>
    public void Render(List<BPMTimePoint> points, int totalLengthMs)
    {
        if (points == null || points.Count == 0 || totalLengthMs <= 0) return;

        int texH = Mathf.Clamp(totalLengthMs / 10, 64, maxTextureHeight);
        RebuildTexture(textureWidth, texH);

        Color[] pixels = new Color[textureWidth * texH];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = backgroundColor;

        // 计算 BPM 值域
        float minBpm = float.MaxValue, maxBpm = float.MinValue;
        foreach (var p in points)
        {
            if (p.BPM < minBpm) minBpm = p.BPM;
            if (p.BPM > maxBpm) maxBpm = p.BPM;
        }
        if (Mathf.Approximately(minBpm, maxBpm)) { minBpm -= 10f; maxBpm += 10f; }

        // 参考中线
        int midX = textureWidth / 2;
        for (int y = 0; y < texH; y++) pixels[y * textureWidth + midX] = gridLineColor;

        // 线性折线：逐段连接相邻关键帧
        for (int i = 0; i < points.Count; i++)
        {
            int x0 = BpmToX(points[i].BPM, minBpm, maxBpm);
            int y0 = TimeToY(points[i].Time, totalLengthMs, texH);

            int x1, y1;
            if (i + 1 < points.Count)
            {
                x1 = BpmToX(points[i + 1].BPM, minBpm, maxBpm);
                y1 = TimeToY(points[i + 1].Time, totalLengthMs, texH);
            }
            else
            {
                x1 = x0;        // 最后段 BPM 保持不变
                y1 = texH - 1;  // 延伸到时间轴顶部（歌曲结尾）
            }

            DrawLine(pixels, textureWidth, texH, x0, y0, x1, y1, curveColor);
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

    // time=0 → y=0（底部），time=end → y=texH-1（顶部）
    private int TimeToY(int timeMs, int totalLengthMs, int texH)
        => Mathf.Clamp(Mathf.RoundToInt((float)timeMs / totalLengthMs * texH), 0, texH - 1);

    /// <summary>Bresenham 直线算法。</summary>
    private void DrawLine(Color[] pixels, int texW, int texH,
                          int x0, int y0, int x1, int y1, Color col)
    {
        int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
        int dy = Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
        int err = (dx > dy ? dx : -dy) / 2;
        int limit = (texW + texH) * 2; // 防无限循环
        for (int iter = 0; iter < limit; iter++)
        {
            if (x0 >= 0 && x0 < texW && y0 >= 0 && y0 < texH)
                pixels[y0 * texW + x0] = col;
            if (x0 == x1 && y0 == y1) break;
            int e2 = err;
            if (e2 > -dx) { err -= dy; x0 += sx; }
            if (e2 <  dy) { err += dx; y0 += sy; }
        }
    }

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
