using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 渲染音符网格背景：横向节拍线 + 纵向轨道分隔线。
///
/// Y 轴 = 时间（底部 = time=0，顶部 = time=totalLength）。
/// 渲染顺序：细分线 → 拍线 → 小节线 → 列线（后绘制的更优先）。
/// 使用 double 精度计算时间，避免浮点数累积误差。
/// </summary>
[RequireComponent(typeof(RawImage))]
public class BeatGridRenderer : MonoBehaviour
{
    [Header("纹理")]
    public int maxTextureHeight = 4096;

    [Header("节拍分割")]
    [Tooltip("每拍细分数，例如 4 = 1/4 音符，16 = 1/16 音符")]
    public int beatDivision = 4;

    [Header("颜色")]
    public Color backgroundColor = new Color(0.07f, 0.07f, 0.09f, 1f);
    public Color measureLineColor = new Color(0.55f, 0.55f, 0.65f, 1f);
    public Color beatLineColor    = new Color(0.30f, 0.30f, 0.38f, 1f);
    public Color subBeatLineColor = new Color(0.14f, 0.14f, 0.18f, 1f);
    public Color columnLineColor  = new Color(0.22f, 0.22f, 0.28f, 1f);

    private RawImage  _rawImage;
    private Texture2D _texture;

    private void Awake()     => _rawImage = GetComponent<RawImage>();
    private void OnDestroy() { if (_texture != null) Destroy(_texture); }

    // ─────────────────────────────────────────────────
    //  公开 API
    // ─────────────────────────────────────────────────

    public void Render(List<BPMTimePoint> bpmPoints, int totalLengthMs, int columnCount)
    {
        if (bpmPoints == null || bpmPoints.Count == 0 || totalLengthMs <= 0 || columnCount < 1) return;

        columnCount = Mathf.Max(1, columnCount);
        int texW = Mathf.Max(columnCount * 8, 64);
        int texH = Mathf.Clamp(totalLengthMs / 10, 64, maxTextureHeight);

        // 纹理每像素对应的时间（ms）
        double msPerPx = (double)totalLengthMs / texH;

        RebuildTexture(texW, texH);

        Color[] pixels = new Color[texW * texH];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = backgroundColor;

        // ── 3 个 pass，优先级从低到高：细分线 → 拍线 → 小节线 ──
        // 后 pass 会覆盖前 pass，确保更重要的线始终可见
        for (int pass = 0; pass < 3; pass++)
        {
            for (int seg = 0; seg < bpmPoints.Count; seg++)
            {
                double bpm = bpmPoints[seg].BPM;
                if (bpm <= 0) continue;

                int    num       = Mathf.Max(1, bpmPoints[seg].Numerator);
                int    segStart  = bpmPoints[seg].Time;
                int    segEnd    = (seg + 1 < bpmPoints.Count) ? bpmPoints[seg + 1].Time : totalLengthMs;

                double mspb = 60000.0 / bpm;            // ms per beat
                double msps = mspb / beatDivision;      // ms per subdivision

                // 当前 pass 是否需要绘制（间距太小则跳过）
                bool doSubBeat = (pass == 0) && (msps / msPerPx >= 2.0);
                bool doBeat    = (pass == 1) && (mspb / msPerPx >= 1.5);
                bool doMeasure = (pass == 2);
                if (!doSubBeat && !doBeat && !doMeasure) continue;

                int subdivIdx = 0;
                while (true)
                {
                    // 用 double 乘法，避免累积浮点误差
                    double subT = segStart + subdivIdx * msps;
                    if (subT >= segEnd - 0.01) break;

                    bool isMeasure = (subdivIdx % (num * beatDivision) == 0);
                    bool isBeat    = (subdivIdx % beatDivision == 0);

                    int y = TimeToY((int)Math.Round(subT), totalLengthMs, texH);

                    if (pass == 0 && !isBeat && doSubBeat)
                        DrawHLine(pixels, texW, texH, y, subBeatLineColor);
                    else if (pass == 1 && isBeat && !isMeasure && doBeat)
                        DrawHLine(pixels, texW, texH, y, beatLineColor);
                    else if (pass == 2 && isMeasure)
                        DrawHLine(pixels, texW, texH, y, measureLineColor);

                    subdivIdx++;
                }
            }
        }

        // ── 列分隔线（最后绘制，始终可见）──
        for (int col = 0; col <= columnCount; col++)
        {
            int x;
            if (col == 0)             x = 0;
            else if (col == columnCount) x = texW - 1;
            else x = Mathf.Clamp(Mathf.RoundToInt((float)col / columnCount * texW), 0, texW - 1);

            for (int y = 0; y < texH; y++)
                pixels[y * texW + x] = columnLineColor;
        }

        _texture.SetPixels(pixels);
        _texture.Apply();
        _rawImage.texture = _texture;
    }

    // ─────────────────────────────────────────────────
    //  内部工具
    // ─────────────────────────────────────────────────

    private void DrawHLine(Color[] pixels, int texW, int texH, int y, Color col)
    {
        if (y < 0 || y >= texH) return;
        int offset = y * texW;
        for (int x = 0; x < texW; x++)
            pixels[offset + x] = col;
    }

    // time=0 → y=0（底部），time=end → y=texH-1（顶部）
    private int TimeToY(int timeMs, int totalLengthMs, int texH)
        => Mathf.Clamp(Mathf.RoundToInt((float)timeMs / totalLengthMs * texH), 0, texH - 1);

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
