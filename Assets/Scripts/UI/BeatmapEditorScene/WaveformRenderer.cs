using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 将 AudioClip 渲染为振幅波形图，显示在同 GameObject 的 RawImage 上。
///
/// vertical = true（默认）：
///   竖向模式，适用于垂直滚动的谱面编辑器。
///   Y 轴 = 时间（顶部 = time=0，底部 = time=end），
///   X 轴 = 振幅（以纹理宽度中心为基准，向左右延伸）。
///
/// vertical = false：
///   横向模式，X 轴 = 时间，Y 轴 = 振幅（以高度中心为基准）。
/// </summary>
[RequireComponent(typeof(RawImage))]
public class WaveformRenderer : MonoBehaviour
{
    // ─────────────────────────────────────────────────
    //  Inspector 设置
    // ─────────────────────────────────────────────────

    [Header("方向")]
    [Tooltip("true = 竖向（时间 = Y 轴），false = 横向（时间 = X 轴）")]
    public bool vertical = true;

    [Header("纹理分辨率")]
    [Tooltip("沿时间轴方向的最大像素数")]
    public int maxTimeAxisResolution = 4096;
    [Tooltip("垂直振幅轴方向的像素数")]
    public int amplitudeAxisResolution = 64;

    [Header("颜色")]
    public Color backgroundColor = new Color(0.08f, 0.08f, 0.10f, 1f);
    public Color waveformColor = new Color(0.25f, 0.75f, 0.40f, 1f);
    public Color peakColor = new Color(1.00f, 0.40f, 0.20f, 1f);
    public Color centerLineColor = new Color(0.25f, 0.25f, 0.28f, 1f);

    [Header("阈值")]
    [Range(0f, 1f)]
    public float peakThreshold = 0.75f;

    [Header("性能")]
    [Tooltip("协程每帧最多处理的像素行/列数")]
    public int samplesPerFrame = 64;

    // ─────────────────────────────────────────────────
    //  公开事件
    // ─────────────────────────────────────────────────

    public event Action OnRenderComplete;

    // ─────────────────────────────────────────────────
    //  私有字段
    // ─────────────────────────────────────────────────

    private RawImage _rawImage;
    private Texture2D _texture;
    private Coroutine _renderCoroutine;

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
        if (_texture != null) Destroy(_texture);
    }

    // ─────────────────────────────────────────────────
    //  公开 API
    // ─────────────────────────────────────────────────

    /// <summary>渲染整首歌的全局波形。</summary>
    public void RenderFull(AudioClip clip)
    {
        if (clip == null) { Debug.LogWarning("[WaveformRenderer] clip 为空"); return; }
        RenderRange(clip, 0f, clip.length);
    }

    /// <summary>渲染指定时间范围（秒）内的波形，可用于局部缩放显示。</summary>
    public void RenderRange(AudioClip clip, float startSec, float endSec)
    {
        if (clip == null) { Debug.LogWarning("[WaveformRenderer] clip 为空"); return; }
        CurrentClip = clip;
        CurrentStartSec = Mathf.Max(0f, startSec);
        CurrentEndSec = Mathf.Min(clip.length, endSec);

        if (_renderCoroutine != null) StopCoroutine(_renderCoroutine);
        _renderCoroutine = StartCoroutine(RenderCoroutine(clip, CurrentStartSec, CurrentEndSec));
    }

    // ─────────────────────────────────────────────────
    //  渲染协程
    // ─────────────────────────────────────────────────

    private IEnumerator RenderCoroutine(AudioClip clip, float startSec, float endSec)
    {
        int totalSamples = clip.samples;
        int channels = clip.channels;
        int sampleRate = clip.frequency;

        float[] allSamples = new float[totalSamples * channels];
        clip.GetData(allSamples, 0);

        int startSample = Mathf.Clamp(Mathf.RoundToInt(startSec * sampleRate), 0, totalSamples - 1);
        int endSample = Mathf.Clamp(Mathf.RoundToInt(endSec * sampleRate), startSample + 1, totalSamples);
        int rangeSamples = endSample - startSample;

        // 确定纹理尺寸
        int texW, texH;
        if (vertical)
        {
            texW = amplitudeAxisResolution;
            texH = Mathf.Min(maxTimeAxisResolution, rangeSamples); // 高度 = 时间轴
        }
        else
        {
            texW = Mathf.Min(maxTimeAxisResolution, rangeSamples);
            texH = amplitudeAxisResolution;
        }
        texW = Mathf.Max(1, texW);
        texH = Mathf.Max(1, texH);

        if (_texture == null || _texture.width != texW || _texture.height != texH)
        {
            if (_texture != null) Destroy(_texture);
            _texture = new Texture2D(texW, texH, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
        }

        Color[] pixels = new Color[texW * texH];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = backgroundColor;

        // 绘制中心线
        if (vertical)
        {
            int midX = texW / 2;
            for (int y = 0; y < texH; y++) pixels[y * texW + midX] = centerLineColor;
        }
        else
        {
            int midY = texH / 2;
            for (int x = 0; x < texW; x++) pixels[midY * texW + x] = centerLineColor;
        }

        // 采样维度（沿时间轴的步数）
        int steps = vertical ? texH : texW;
        int frameCount = 0;

        for (int i = 0; i < steps; i++)
        {
            int sStart = startSample + (int)((long)i * rangeSamples / steps);
            int sEnd = startSample + (int)((long)(i + 1) * rangeSamples / steps);
            sEnd = Mathf.Min(sEnd, endSample);
            if (sEnd <= sStart) sEnd = sStart + 1;

            float maxAmp = 0f;
            for (int s = sStart; s < sEnd && s < totalSamples; s++)
            {
                float sum = 0f;
                for (int c = 0; c < channels; c++)
                    sum += Mathf.Abs(allSamples[s * channels + c]);
                float avg = sum / channels;
                if (avg > maxAmp) maxAmp = avg;
            }

            Color col = maxAmp >= peakThreshold ? peakColor : waveformColor;

            if (vertical)
            {
                // i = 纹理行（y），X 轴为振幅
                int midX = texW / 2;
                int halfW = Mathf.RoundToInt(maxAmp * midX);
                int xMin = Mathf.Max(0, midX - halfW);
                int xMax = Mathf.Min(texW - 1, midX + halfW);
                int y = i;
                for (int x = xMin; x <= xMax; x++)
                    pixels[y * texW + x] = col;
            }
            else
            {
                // i = 纹理列（x），Y 轴为振幅
                int midY = texH / 2;
                int halfH = Mathf.RoundToInt(maxAmp * midY);
                int yMin = Mathf.Max(0, midY - halfH);
                int yMax = Mathf.Min(texH - 1, midY + halfH);
                int x = i;
                for (int y = yMin; y <= yMax; y++)
                    pixels[y * texW + x] = col;
            }

            frameCount++;
            if (frameCount >= samplesPerFrame)
            {
                frameCount = 0;
                yield return null;
            }
        }

        _texture.SetPixels(pixels);
        _texture.Apply();
        _rawImage.texture = _texture;

        _renderCoroutine = null;
        OnRenderComplete?.Invoke();
        Debug.Log($"[WaveformRenderer] 渲染完成: {clip.name} [{startSec:F2}s ~ {endSec:F2}s] ({texW}×{texH})");
    }
}
