using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

/// <summary>
/// 挂载在 LeftCover / RightCover 上，提供：
///   • 悬停时向左/右平滑插值移动 hoverOffset 像素（距目标越远速度越快）
///   • 按下时移回原位
///   • 松开时（仍在按钮上）移回悬停位置，并触发 onNavigate 事件
///   • 移出时移回原位
/// </summary>
public class CoverNavButton : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler,
    IPointerClickHandler
{
    // ─────────────────────────────────────────────────
    //  Inspector 参数
    // ─────────────────────────────────────────────────

    public enum MoveDirection { Left, Right }

    [Header("方向")]
    [Tooltip("Left：悬停时向左移；Right：悬停时向右移")]
    [SerializeField] private MoveDirection moveDirection = MoveDirection.Left;

    [Header("动画参数")]
    [Tooltip("悬停时沿方向偏移的像素距离")]
    [SerializeField] private float hoverOffset = 16f;

    [Tooltip("平滑插值速度系数，值越大则趋近目标越快（推荐范围：10~30）")]
    [SerializeField] private float smoothSpeed = 20f;

    [Tooltip("当与目标的距离小于此值时视为到达，停止插值（像素）")]
    [SerializeField] private float snapThreshold = 0.1f;

    [Header("回调")]
    [Tooltip("点击（PointerUp 在按钮上）时触发，用于切换谱面")]
    public UnityEvent onNavigate;

    // ─────────────────────────────────────────────────
    //  私有状态
    // ─────────────────────────────────────────────────

    private RectTransform _rect;
    private Vector2 _originPos;
    private Vector2 _hoverPos;

    private Coroutine _currentAnim;
    private bool _isHovering;

    // ─────────────────────────────────────────────────
    //  生命周期
    // ─────────────────────────────────────────────────

    private void Awake()
    {
        _rect = GetComponent<RectTransform>();
        _originPos = _rect.anchoredPosition;

        float offsetX = moveDirection == MoveDirection.Left ? -hoverOffset : hoverOffset;
        _hoverPos = _originPos + new Vector2(offsetX, 0f);
    }

    // ─────────────────────────────────────────────────
    //  Pointer 事件
    // ─────────────────────────────────────────────────

    public void OnPointerEnter(PointerEventData eventData)
    {
        _isHovering = true;
        PlayAnim(_hoverPos);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isHovering = false;
        PlayAnim(_originPos);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        // 按下：移回原位
        PlayAnim(_originPos);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        // 松开且仍在按钮区域：移回悬停位置
        if (_isHovering)
            PlayAnim(_hoverPos);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // PointerDown 与 PointerUp 都在本按钮上才触发
        onNavigate?.Invoke();
    }

    // ─────────────────────────────────────────────────
    //  动画
    // ─────────────────────────────────────────────────

    private void PlayAnim(Vector2 target)
    {
        if (_currentAnim != null) StopCoroutine(_currentAnim);
        _currentAnim = StartCoroutine(AnimTo(target));
    }

    /// <summary>
    /// 速度插值趋近目标位置：每帧以 smoothSpeed 为系数执行 Lerp，
    /// 距目标越远则当帧位移越大，产生先快后慢的缓入效果。
    /// </summary>
    private IEnumerator AnimTo(Vector2 target)
    {
        while (Vector2.Distance(_rect.anchoredPosition, target) > snapThreshold)
        {
            _rect.anchoredPosition = Vector2.Lerp(_rect.anchoredPosition, target, smoothSpeed * Time.deltaTime);
            yield return null;
        }

        _rect.anchoredPosition = target;
    }
}
