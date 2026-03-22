using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

// LoginScene的按钮
public class ButtonHoverImage : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private RectTransform hoverImage;
    [SerializeField] private float targetWidth = 264f;
    [SerializeField] private float animDuration = 0.15f;

    private Coroutine _currentAnim;

    private void Start()
    {
        if (hoverImage == null)
        {
            Debug.LogWarning($"[ButtonHoverImage] {name}: hoverImage 未赋值");
            return;
        }
        SetWidth(0f);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (hoverImage == null) return;
        PlayAnim(targetWidth);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (hoverImage == null) return;
        PlayAnim(0f);
    }

    // 播放动画（启用一个协程）
    private void PlayAnim(float to)
    {
        if (_currentAnim != null) StopCoroutine(_currentAnim);
        _currentAnim = StartCoroutine(AnimWidth(to));
    }

    // 插值到目标宽度
    private IEnumerator AnimWidth(float to)
    {
        float from = hoverImage.sizeDelta.x;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / animDuration;
            float ease = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 2f);
            SetWidth(Mathf.Lerp(from, to, ease));
            yield return null;
        }
        SetWidth(to);
    }

    private void SetWidth(float w)
    {
        var sd = hoverImage.sizeDelta;
        sd.x = w;
        hoverImage.sizeDelta = sd;
    }
}