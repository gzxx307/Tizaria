using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 渲染音符网格背景：横向节拍线 + 纵向轨道分隔线。
///
/// Y 轴 = 时间（顶部 = time=0，底部 = time=totalLength），
/// X 轴 = 轨道列（均匀分布，0..columnCount-1）。
/// </summary>
[RequireComponent(typeof(RawImage))]
public class BeatGridRenderer : MonoBehaviour
{
    [Header("纹理")]
    public int maxTextureHeight = 4096;

    [Header("节拍分割")]
    [Tooltip("每拍细分数，例如 4 = 1/4 音符显示节拍线")]
    public int beatDivision = 4;

    [Header("颜色")]
    public Color backgroundColor = new Color(0.07f, 0.07f, 0.09f, 1f);
    public Color measureLineColor = new Color(0.55f, 0.55f, 0.65f, 1f);   // 小节线（最亮）
    public Color beatLineColor = new Color(0.30f, 0.30f, 0.38f, 1f);      // 拍线
    public Color subBeatLineColor = new Color(0.14f, 0.14f, 0.18f, 1f);   // 细分线（最暗）
    public Color columnLineColor = new Color(0.22f, 0.22f, 0.28f, 1f);    // 列分隔线

    private RawImage _rawImage;
    private Texture2D _texture;

    private void Awake() => _rawImage = GetComponent<RawImage>();
    private void OnDestroy() { if (_texture != null) Destroy(_texture); }

    // ─────────────────────────────────────────────────
    //  公开 API
    // ─────────────────────────────────────────────────

    /// <summary>
    /// 渲染节拍网格。
    /// </summary>
    /// <param name="bpmPoints">BPM 关键帧列表（按时间升序）</param>
    /// <param name="totalLengthMs">歌曲总时长（ms）</param>
    /// <param name="columnCount">轨道数</param>
    public void Render(List<BPMTimePoint> bpmPoints, int totalLengthMs, int columnCount)
    {
        if (bpmPoints == null || bpmPoints.Count == 0 || totalLengthMs <= 0 || columnCount < 1) return;

        columnCount = Mathf.Max(1, columnCount);
        int texW = Mathf.Max(columnCount * 8, 64);
        int texH = Mathf.Clamp(totalLengthMs / 10, 64, maxTextureHeight);

        RebuildTexture(texW, texH);

        Color[] pixels = new Color[texW * texH];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = backgroundColor;

        // ── 纵向列分隔线 ──
        for (int col = 1; col < columnCount; col++)
        {
            int x = Mathf.RoundToInt((float)col / columnCount * texW);
            x = Mathf.Clamp(x, 0, texW - 1);
            for (int y = 0; y < texH; y++)
                pixels[y * texW + x] = columnLineColor;
        }

        // ── 横向节拍线 ──
        for (int seg = 0; seg < bpmPoints.Count; seg++)
        {
            float bpm = bpmPoints[seg].BPM;
            int num = Mathf.Max(1, bpmPoints[seg].Numerator);
            int segStartMs = bpmPoints[seg].Time;
            int segEndMs = (seg + 1 < bpmPoints.Count) ? bpmPoints[seg + 1].Time : totalLengthMs;

            if (bpm <= 0f) continue;

            // 每拍时长（ms）及细分时长
            float mspb = 60000f / bpm;             // ms per beat
            float msps = mspb / beatDivision;      // ms per subdivision

            float t = segStartMs;
            int beatInMeasure = 0;

            while (t < segEndMs - 0.1f)
            {
                int y = TimeToY((int)t, totalLengthMs, texH);

                bool isMeasure = (beatInMeasure % num == 0);
                bool isBeat = (beatInMeasure % 1 == 0); // always true here, used for subdivision loop

                // 每个拍内部的细分线
                for (int sub = 0; sub < beatDivision; sub++)
                {
                    float subT = t + sub * msps;
                    if (subT >= segEndMs) break;
                    int sy = TimeToY((int)subT, totalLengthMs, texH);

                    Color lineCol;
                    if (sub == 0 && isMeasure) lineCol = measureLineColor;
                    else if (sub == 0) lineCol = beatLineColor;
                    else lineCol = subBeatLineColor;

                    DrawHLine(pixels, texW, texH, sy, lineCol);
                }

                t += mspb;
                beatInMeasure++;
            }
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
