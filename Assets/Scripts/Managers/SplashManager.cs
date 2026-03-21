using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class SplashManager : MonoBehaviour
{
    public GameObject Logo;
    private SpriteRenderer _logoSpriteRenderer;

    // 图标淡入速度
    public float ColorChangeSpeed = 1.0f;
    // 图标移动加速度
    public float Acceleration = 2.0f;
    // 图标的目标移动距离
    public int Distance = 1;

    // 标题（Tizaria）
    public string Title = "Tizaria";
    public TextMeshProUGUI TitleText;

    public float CharFadeSpeed = 5.0f;
    // 每个字符显示前等待的帧数
    public int CharIntervalFrames = 3;

    public string ClickTip = "Click anywhere to complete";
    public float TipFadeInSpeed = 0.7f;
    public TextMeshProUGUI TipText;

    // 提示文本闪烁频率
    public float TipBlinkDuration = 2.0f;
    // 文本闪烁alpha最小值
    public float TipMinAlpha = 0.2f;
    
    // 正在进行动画
    private bool _isSplashing = true;
    private Coroutine _blinkCoroutine;
    
    // 黑幕GameObject
    public GameObject BlackMaskPanel;
    private Image BlackMaskImage;
    public float MaskFadeInSpeed = 1.0f;
    
    // 正在进入游戏
    private bool _isEnteringGame = false;
    
    private void Start()
    {
        _logoSpriteRenderer = Logo.GetComponent<SpriteRenderer>();
        if (_logoSpriteRenderer != null) StartCoroutine(Splash());
        BlackMaskImage = BlackMaskPanel.GetComponent<Image>();
    }

    private void Update()
    {
        if (Input.GetMouseButton(0))
        {
            if (_isSplashing) SkipSplashing();
            else if (!_isEnteringGame) StartCoroutine(EnterGame());
        }
    }

    private IEnumerator Splash()
    {
        // Logo淡入
        yield return StartCoroutine(LogoFadeIn());
        // 等 0.5s
        yield return new WaitForSeconds(0.5f);
        // Logo上移
        yield return StartCoroutine(LogoMoveUp());
        // 打字机淡入
        TitleText.text = Title;
        yield return StartCoroutine(TypewriterFadeIn(TitleText));
        // 等 0.7s
        yield return new WaitForSeconds(0.7f);
        // 确认能够进入游戏
        _isSplashing = false;
        // 点击进入游戏提示
        TipText.text = ClickTip;
        StartCoroutine(TipBlink(TipText));
    }

    // Logo 淡入
    private IEnumerator LogoFadeIn()
    {
        var c = _logoSpriteRenderer.color;
        c.a = 0f;
        _logoSpriteRenderer.color = c;

        while (c.a < 1f)
        {
            c.a = Mathf.Min(c.a + ColorChangeSpeed * Time.deltaTime, 1f);
            _logoSpriteRenderer.color = c;
            yield return null;
        }
    }
    
    // Logo 上移
    private IEnumerator LogoMoveUp()
    {
        while (Logo.transform.position.y < Distance - 0.01f)
        {
            var v = Logo.transform.position;
            v.y -= Acceleration * (v.y - Distance) * Time.deltaTime;
            Logo.transform.position = v;
            yield return null;
        }

        // 对齐目标位置
        var pos = Logo.transform.position;
        pos.y = Distance;
        Logo.transform.position = pos;
    }

    // 利用 TMP 顶点颜色逐字控制 alpha
    private IEnumerator TypewriterFadeIn(TextMeshProUGUI tmp)
    {
        tmp.ForceMeshUpdate();
        int total = tmp.textInfo.characterCount;

        // 初始全部透明
        SetAllCharactersAlpha(tmp, 0);

        for (int i = 0; i < total; i++)
        {
            // 打字间隔
            for (int f = 0; f < CharIntervalFrames; f++)
                yield return null;

            // 第 i 个字淡入
            float alpha = 0f;
            while (alpha < 1f)
            {
                alpha = Mathf.Min(alpha + CharFadeSpeed * Time.deltaTime, 1f);
                SetCharacterAlpha(tmp, i, alpha);
                yield return null;
            }
        }
    }
    
    // 闪烁ClickTip
    private IEnumerator TipBlink(TextMeshProUGUI tmp)
    {
        var color = tmp.color;
        color.a = 0f;
        tmp.color = color;

        // 首先将alpha抬到TipMinAlpha避免透明度瞬间变化
        while (color.a < 1)
        {
            color.a += TipFadeInSpeed * Time.deltaTime;
            tmp.color = color;
            yield return null;
        }
        
        float t = Mathf.PI * 0.5f;
        // 开始闪烁
        while (true)
        {
            t += Time.deltaTime / TipBlinkDuration * Mathf.PI;
            // sin 映射到 [TipMinAlpha, 1]
            float alpha = Mathf.Lerp(TipMinAlpha, 1f, (Mathf.Sin(t) + 1f) * 0.5f);
            color.a = alpha;
            tmp.color = color;
            yield return null;
        }
    }
    
    // 跳过动画
    private void SkipSplashing()
    {
        StopAllCoroutines();

        // Logo 直接显示在目标位置
        var c = _logoSpriteRenderer.color;
        c.a = 1f;
        _logoSpriteRenderer.color = c;
        var pos = Logo.transform.position;
        pos.y = Distance;
        Logo.transform.position = pos;

        // 标题直接全部显示
        TitleText.text = Title;
        SetAllCharactersAlpha(TitleText, 1f);

        BeginWaitingForInput();
    }
    // 动画播完（或跳过）后进入等待点击状态
    private void BeginWaitingForInput()
    {
        _isSplashing = false;
        TipText.text = ClickTip;
        if (_blinkCoroutine != null) StopCoroutine(_blinkCoroutine);
        _blinkCoroutine = StartCoroutine(TipBlink(TipText));
    }
    
    // 进入游戏
    private IEnumerator EnterGame()
    {
        // 进入正在进入游戏状态
        _isEnteringGame = true;
        
        // 黑幕淡入
        while (BlackMaskImage.color.a < 1f)
        {
            var color = BlackMaskImage.color;
            color.a += MaskFadeInSpeed * Time.deltaTime;
            BlackMaskImage.color = color;
            yield return null;
        }
        
        SceneManager.LoadScene("LoginScene");
    }

    // 工具函数

    // 设置单个字符的 alpha
    private void SetCharacterAlpha(TextMeshProUGUI tmp, int charIndex, float alpha)
    {
        var info = tmp.textInfo;
        if (charIndex >= info.characterCount) return;

        var ch = info.characterInfo[charIndex];
        if (!ch.isVisible) return;

        byte a = (byte)(alpha * 255);
        var colors = info.meshInfo[ch.materialReferenceIndex].colors32;
        int v = ch.vertexIndex;
        colors[v].a = colors[v+1].a = colors[v+2].a = colors[v+3].a = a;

        tmp.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
    }

    // 批量将所有字符设为指定 alpha（初始化用）
    private void SetAllCharactersAlpha(TextMeshProUGUI tmp, float alpha)
    {
        tmp.ForceMeshUpdate();
        byte a = (byte)(alpha * 255);
        foreach (var mesh in tmp.textInfo.meshInfo)
            for (int v = 0; v < mesh.colors32.Length; v++)
                mesh.colors32[v].a = a;
        tmp.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
    }
    
}