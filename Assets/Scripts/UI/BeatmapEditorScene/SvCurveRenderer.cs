using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 将 SVTimePoint 列表渲染为竖向线性折线图，显示在 RawImage 上。
/// Y 轴 = 时间（底部 = time=0，顶部 = time=end），X 轴 = SV 值（中心线 = SV 1.0）。
/// 相邻关键帧之间用直线连接（线性插值）。
/// </summary>
[RequireComponent(typeof(RawImage))]
public class SvCurveRenderer : MonoBehaviour
{
    [Header("纹理")]
    public int textureWidth     = 128;
    public int maxTextureHeight = 4096;

    [Header("SV 显示范围")]
    [Tooltip("X 轴显示的 SV 值范围（以 1.0 为中心）")]
    public float svDisplayHalfRange = 1.5f;

    [Header("颜色")]
    public Color backgroundColor = new Color(0.07f, 0.07f, 0.10f, 1f);
    public Color centerLineColor = new Color(0.20f, 0.35f, 0.40f, 1f);
    public Color curveColor      = new Color(0.20f, 0.85f, 0.95f, 1f);

    private RawImage  _rawImage;
    private Texture2D _texture;

    private void Awake()     => _rawImage = GetComponent<RawImage>();
    private void OnDestroy() { if (_texture != null) Destroy(_texture); }

    // ─────────────────────────────────────────────────
    //  公开 API
    // ─────────────────────────────────────────────────

    /// <summary>渲染 SV 曲线（线性插值）。SV=1.0 显示在中心线位置。</summary>
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

        float minSv = 1f - svDisplayHalfRange;
        float maxSv = 1f + svDisplayHalfRange;

        if (points.Count == 0)
        {
            // 没有关键帧：整段 SV=1.0，只画中心竖线即可
            _texture.SetPixels(pixels);
            _texture.Apply();
            _rawImage.texture = _texture;
            return;
        }

        // 第一个关键帧前：SV=1.0，从 y=0（底部）画到第一关键帧
        int firstX = SvToX(1.0f, minSv, maxSv);
        int firstY = TimeToY(points[0].Time, totalLengthMs, texH);
        DrawLine(pixels, textureWidth, texH, firstX, 0, firstX, firstY, curveColor);

        // 逐段线性插值
        for (int i = 0; i < points.Count; i++)
        {
            int x0 = SvToX(points[i].SV, minSv, maxSv);
            int y0 = TimeToY(points[i].Time, totalLengthMs, texH);

            int x1, y1;
            if (i + 1 < points.Count)
            {
                x1 = SvToX(points[i + 1].SV, minSv, maxSv);
                y1 = TimeToY(points[i + 1].Time, totalLengthMs, texH);
            }
            else
            {
                x1 = x0;        // 最后段 SV 保持不变
                y1 = texH - 1;
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

    private int SvToX(float sv, float minSv, float maxSv)
    {
        float t = (sv - minSv) / (maxSv - minSv);
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
        int limit = (texW + texH) * 2;
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
