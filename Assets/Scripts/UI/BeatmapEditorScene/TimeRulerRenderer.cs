using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 渲染竖向时间刻度尺：在 RawImage 纹理上绘制刻度线。
/// Y 轴 = 时间（顶部 = time=0），
/// 大刻度 = 整秒，中刻度 = 0.5 秒，小刻度 = 0.25 秒（可配置）。
///
/// 注意：纹理只含刻度线，不含文字标签。
/// 文字标签由控制器脚本在 KeyframeContainer 中动态生成 Text 组件。
/// </summary>
[RequireComponent(typeof(RawImage))]
public class TimeRulerRenderer : MonoBehaviour
{
    [Header("纹理")]
    public int textureWidth = 40;
    public int maxTextureHeight = 4096;

    [Header("颜色")]
    public Color backgroundColor = new Color(0.06f, 0.06f, 0.08f, 1f);
    public Color majorTickColor = new Color(0.80f, 0.80f, 0.85f, 1f);  // 整秒
    public Color midTickColor = new Color(0.50f, 0.50f, 0.55f, 1f);    // 0.5 秒
    public Color minorTickColor = new Color(0.25f, 0.25f, 0.30f, 1f);  // 0.25 秒

    [Header("刻度线宽度（占纹理宽度的比例）")]
    [Range(0f, 1f)] public float majorTickLength = 1.0f;
    [Range(0f, 1f)] public float midTickLength = 0.5f;
    [Range(0f, 1f)] public float minorTickLength = 0.3f;

    private RawImage _rawImage;
    private Texture2D _texture;

    private void Awake() => _rawImage = GetComponent<RawImage>();
    private void OnDestroy() { if (_texture != null) Destroy(_texture); }

    // ─────────────────────────────────────────────────
    //  公开 API
    // ─────────────────────────────────────────────────

    /// <summary>
    /// 渲染时间刻度尺。
    /// </summary>
    /// <param name="totalLengthMs">歌曲总时长（ms）</param>
    public void Render(int totalLengthMs)
    {
        if (totalLengthMs <= 0) return;

        int texH = Mathf.Clamp(totalLengthMs / 10, 64, maxTextureHeight);
        RebuildTexture(textureWidth, texH);

        Color[] pixels = new Color[textureWidth * texH];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = backgroundColor;

        float totalSec = totalLengthMs / 1000f;

        // 小刻度：每 0.25 秒
        for (float t = 0f; t <= totalSec + 0.001f; t += 0.25f)
        {
            int y = SecToY(t, totalSec, texH);
            bool isMajor = Mathf.Approximately(t % 1f, 0f);
            bool isMid = !isMajor && Mathf.Approximately(t % 0.5f, 0f);

            Color col;
            float lenRatio;
            if (isMajor) { col = majorTickColor; lenRatio = majorTickLength; }
            else if (isMid) { col = midTickColor; lenRatio = midTickLength; }
            else { col = minorTickColor; lenRatio = minorTickLength; }

            int tickLen = Mathf.RoundToInt(lenRatio * textureWidth);
            int xStart = textureWidth - tickLen;

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
    }

    // ─────────────────────────────────────────────────
    //  内部工具
    // ─────────────────────────────────────────────────

    private int SecToY(float sec, float totalSec, int texH)
        => Mathf.Clamp(Mathf.RoundToInt(sec / totalSec * texH), 0, texH - 1);

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
