using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 将 AudioClip 渲染为振幅波形图，显示在同 GameObject 的 RawImage 上。
///
/// 使用方式：
///   1. 将此脚本挂载在含 RawImage 的 GameObject 上
///   2. 调用 RenderFull(clip) 渲染整首歌，或 RenderRange(clip, startSec, endSec) 渲染片段
///   3. 渲染完成后触发 OnRenderComplete 事件（可选监听）
/// </summary>
[RequireComponent(typeof(RawImage))]
public class WaveformRenderer : MonoBehaviour
{
    // ─────────────────────────────────────────────────
    //  Inspector 设置
    // ─────────────────────────────────────────────────

    [Header("纹理分辨率")]
    [Tooltip("波形图横向像素数，越大细节越清晰但生成越慢")]
    public int textureWidth = 2048;
    [Tooltip("波形图纵向像素数")]
    public int textureHeight = 128;

    [Header("颜色")]
    public Color backgroundColor = new Color(0.08f, 0.08f, 0.10f, 1f);
    [Tooltip("正常振幅区域的波形颜色")]
    public Color waveformColor = new Color(0.25f, 0.75f, 0.40f, 1f);
    [Tooltip("高振幅（峰值）区域的波形颜色")]
    public Color peakColor = new Color(1.00f, 0.40f, 0.20f, 1f);
    [Tooltip("中轴参考线颜色")]
    public Color centerLineColor = new Color(0.3f, 0.3f, 0.3f, 1f);

    [Header("阈值")]
    [Range(0f, 1f)]
    [Tooltip("振幅超过此值时改用 peakColor 着色")]
    public float peakThreshold = 0.75f;

    [Header("性能")]
    [Tooltip("协程每帧最多处理的像素列数，调低可减少帧卡顿")]
    public int columnsPerFrame = 64;

    // ─────────────────────────────────────────────────
    //  公开事件
    // ─────────────────────────────────────────────────

    /// <summary>波形纹理生成完毕时触发</summary>
    public event Action OnRenderComplete;

    // ─────────────────────────────────────────────────
    //  私有字段
    // ─────────────────────────────────────────────────

    private RawImage _rawImage;
    private Texture2D _texture;
    private Coroutine _renderCoroutine;

    // 当前正在渲染的 clip 与时间范围（供外部查询）
    public AudioClip CurrentClip { get; private set; }
    public float CurrentStartSec { get; private set; }
    public float CurrentEndSec { get; private set; }

    // ─────────────────────────────────────────────────
    //  生命周期
    // ─────────────────────────────────────────────────

    private void Awake()
    {
        _rawImage = GetComponent<RawImage>();
    }

    private void OnDestroy()
    {
        if (_texture != null)
            Destroy(_texture);
    }

    // ─────────────────────────────────────────────────
    //  公开 API
    // ─────────────────────────────────────────────────

    /// <summary>
    /// 渲染整个 AudioClip 的全局波形。
    /// </summary>
    public void RenderFull(AudioClip clip)
    {
        if (clip == null)
        {
            Debug.LogWarning("[WaveformRenderer] clip 为空，跳过渲染");
            return;
        }
        RenderRange(clip, 0f, clip.length);
    }

    /// <summary>
    /// 渲染指定时间范围（秒）内的波形，可用于时间轴缩放/滚动。
    /// </summary>
    /// <param name="clip">音频资源</param>
    /// <param name="startSec">起始时间（秒）</param>
    /// <param name="endSec">结束时间（秒）</param>
    public void RenderRange(AudioClip clip, float startSec, float endSec)
    {
        if (clip == null)
        {
            Debug.LogWarning("[WaveformRenderer] clip 为空，跳过渲染");
            return;
        }

        CurrentClip = clip;
        CurrentStartSec = Mathf.Max(0f, startSec);
        CurrentEndSec = Mathf.Min(clip.length, endSec);

        if (_renderCoroutine != null)
            StopCoroutine(_renderCoroutine);

        _renderCoroutine = StartCoroutine(RenderCoroutine(clip, CurrentStartSec, CurrentEndSec));
    }

    // ─────────────────────────────────────────────────
    //  内部渲染协程
    // ─────────────────────────────────────────────────

    private IEnumerator RenderCoroutine(AudioClip clip, float startSec, float endSec)
    {
        // 1. 读取所有 PCM 数据（交错多声道：sample[i*channels + ch]）
        int totalSamples = clip.samples;
        int channels = clip.channels;
        int sampleRate = clip.frequency;

        float[] allSamples = new float[totalSamples * channels];
        clip.GetData(allSamples, 0);

        int startSample = Mathf.Clamp(Mathf.RoundToInt(startSec * sampleRate), 0, totalSamples - 1);
        int endSample = Mathf.Clamp(Mathf.RoundToInt(endSec * sampleRate), startSample + 1, totalSamples);
        int rangeSamples = endSample - startSample;

        // 2. 创建（或复用）纹理
        if (_texture == null || _texture.width != textureWidth || _texture.height != textureHeight)
        {
            if (_texture != null) Destroy(_texture);
            _texture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
        }

        Color[] pixels = new Color[textureWidth * textureHeight];

        // 3. 背景填充
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = backgroundColor;

        // 4. 中轴参考线
        int midY = textureHeight / 2;
        for (int x = 0; x < textureWidth; x++)
            pixels[midY * textureWidth + x] = centerLineColor;

        // 5. 逐列绘制振幅柱（分帧执行）
        int frameCount = 0;
        for (int x = 0; x < textureWidth; x++)
        {
            // 当前列对应的样本区间
            int sStart = startSample + (int)((long)x * rangeSamples / textureWidth);
            int sEnd = startSample + (int)((long)(x + 1) * rangeSamples / textureWidth);
            sEnd = Mathf.Min(sEnd, endSample);
            if (sEnd <= sStart) sEnd = sStart + 1;

            // 取区间内峰值振幅（各声道平均后取最大）
            float maxAmp = 0f;
            for (int s = sStart; s < sEnd && s < totalSamples; s++)
            {
                float sum = 0f;
                for (int c = 0; c < channels; c++)
                    sum += Mathf.Abs(allSamples[s * channels + c]);
                float avg = sum / channels;
                if (avg > maxAmp) maxAmp = avg;
            }

            // 柱高度（以中轴为中心向上下延伸）
            int halfH = Mathf.RoundToInt(maxAmp * midY);
            Color col = (maxAmp >= peakThreshold) ? peakColor : waveformColor;

            int yMin = Mathf.Max(0, midY - halfH);
            int yMax = Mathf.Min(textureHeight - 1, midY + halfH);
            for (int y = yMin; y <= yMax; y++)
                pixels[y * textureWidth + x] = col;

            // 每处理 columnsPerFrame 列就让出一帧
            frameCount++;
            if (frameCount >= columnsPerFrame)
            {
                frameCount = 0;
                yield return null;
            }
        }

        // 6. 提交纹理
        _texture.SetPixels(pixels);
        _texture.Apply();
        _rawImage.texture = _texture;

        _renderCoroutine = null;
        OnRenderComplete?.Invoke();
        Debug.Log($"[WaveformRenderer] 波形渲染完成: {clip.name} [{startSec:F2}s ~ {endSec:F2}s]");
    }
}
