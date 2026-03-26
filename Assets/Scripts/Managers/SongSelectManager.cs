using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// SongSelectScene 的主管理器。
/// 负责：
///   1. 将 GameRoot.PlayerData 渲染到 PlayerPanel
///   2. 驱动 BeatmapCarousel 的封面滑动与谱面切换
///   3. 根据当前选中谱面刷新 ScorePanel
/// </summary>
public class SongSelectManager : MonoBehaviour
{
    // ─────────────────────────────────────────────────
    //  Inspector 引用
    // ─────────────────────────────────────────────────

    [Header("PlayerPanel")]
    [SerializeField] private TextMeshProUGUI playerNameText;
    [SerializeField] private TextMeshProUGUI playerUidText;
    [SerializeField] private TextMeshProUGUI playerRksText;

    [Header("BeatmapCarousel — SlideContainer")]
    [SerializeField] private RectTransform slideContainer;
    [SerializeField] private RawImage coverLeft;
    [SerializeField] private RawImage coverCurrent;
    [SerializeField] private RawImage coverRight;

    [Tooltip("每次切换时 SlideContainer 滑动的像素距离（应与 CoverLeft/Right 的间距匹配）")]
    [SerializeField] private float slideSpacing = 100f;

    [Tooltip("速度插值系数，越大则滑动越快趋近目标位置")]
    [SerializeField] private float slideSpeed = 10f;

    [Header("BeatmapCarousel — 导航按钮")]
    [SerializeField] private CoverNavButton leftNavButton;
    [SerializeField] private CoverNavButton rightNavButton;

    [Header("ScorePanel")]
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI accText;
    [SerializeField] private TextMeshProUGUI rankingText;

    // ─────────────────────────────────────────────────
    //  私有状态
    // ─────────────────────────────────────────────────

    private int _currentIndex;
    private bool _isSliding;

    // ─────────────────────────────────────────────────
    //  生命周期
    // ─────────────────────────────────────────────────

    private void Start()
    {
        if (!GameRoot.CheckToLoad()) return;

        // 绑定导航按钮回调
        if (leftNavButton != null)
            leftNavButton.onNavigate.AddListener(NavigatePrev);
        if (rightNavButton != null)
            rightNavButton.onNavigate.AddListener(NavigateNext);

        RefreshPlayerPanel();
        RefreshCarousel();
    }

    // ─────────────────────────────────────────────────
    //  PlayerPanel
    // ─────────────────────────────────────────────────

    private void RefreshPlayerPanel()
    {
        PlayerData data = GameRoot.Instance.PlayerData;
        if (data == null)
        {
            Debug.LogWarning("[SongSelectManager] PlayerData 为空");
            return;
        }

        if (playerNameText != null) playerNameText.text = data.Name;
        if (playerUidText != null) playerUidText.text = data.Uid;
        if (playerRksText != null) playerRksText.text = data.Ranks.ToString("F2");
    }

    // ─────────────────────────────────────────────────
    //  BeatmapCarousel — 导航
    // ─────────────────────────────────────────────────

    /// <summary> 向左切换到上一个谱面 </summary>
    public void NavigatePrev()
    {
        if (_isSliding) return;
        var sets = GameRoot.Instance.BeatmapSets;
        if (sets == null || sets.Count <= 1) return;

        StartCoroutine(SlideAndSwitch(isNext: false));
    }

    /// <summary> 向右切换到下一个谱面 </summary>
    public void NavigateNext()
    {
        if (_isSliding) return;
        var sets = GameRoot.Instance.BeatmapSets;
        if (sets == null || sets.Count <= 1) return;

        StartCoroutine(SlideAndSwitch(isNext: true));
    }

    /// <summary>
    /// 速度线性插值滑动 SlideContainer，到位后更新封面纹理并重置容器位置。
    /// </summary>
    private IEnumerator SlideAndSwitch(bool isNext)
    {
        _isSliding = true;

        float targetX = isNext ? -slideSpacing : slideSpacing;

        // 速度 Lerp：每帧以 slideSpeed 为系数向目标趋近
        while (Mathf.Abs(slideContainer.anchoredPosition.x - targetX) > 0.5f)
        {
            Vector2 pos = slideContainer.anchoredPosition;
            pos.x = Mathf.Lerp(pos.x, targetX, slideSpeed * Time.deltaTime);
            slideContainer.anchoredPosition = pos;
            yield return null;
        }

        // 精确停止
        Vector2 snapped = slideContainer.anchoredPosition;
        snapped.x = targetX;
        slideContainer.anchoredPosition = snapped;

        // 更新索引
        var sets = GameRoot.Instance.BeatmapSets;
        if (isNext)
            _currentIndex = (_currentIndex + 1) % sets.Count;
        else
            _currentIndex = (_currentIndex - 1 + sets.Count) % sets.Count;

        // 立即归零（封面贴图已重置，视觉上不会出现跳变）
        snapped.x = 0f;
        slideContainer.anchoredPosition = snapped;

        RefreshCarousel();
        _isSliding = false;
    }

    // ─────────────────────────────────────────────────
    //  BeatmapCarousel — 封面刷新
    // ─────────────────────────────────────────────────

    private void RefreshCarousel()
    {
        var sets = GameRoot.Instance.BeatmapSets;

        if (sets == null || sets.Count == 0)
        {
            SetCovers(null, null, null);
            GameRoot.Instance.SetSelection(null, null);
            RefreshScorePanel(null);
            return;
        }

        int total = sets.Count;
        int prevIdx = (_currentIndex - 1 + total) % total;
        int nextIdx = (_currentIndex + 1) % total;

        SetCovers(
            total > 1 ? sets[prevIdx].CoverTexture : null,
            sets[_currentIndex].CoverTexture,
            total > 1 ? sets[nextIdx].CoverTexture : null
        );

        // 默认选取当前谱面集的第一个难度
        var currentSet = sets[_currentIndex];
        var firstMap = currentSet.Beatmaps != null && currentSet.Beatmaps.Count > 0
            ? currentSet.Beatmaps[0]
            : null;

        GameRoot.Instance.SetSelection(currentSet, firstMap);
        RefreshScorePanel(firstMap);
    }

    private void SetCovers(Texture2D left, Texture2D center, Texture2D right)
    {
        if (coverLeft != null) coverLeft.texture = left;
        if (coverCurrent != null) coverCurrent.texture = center;
        if (coverRight != null) coverRight.texture = right;
    }

    // ─────────────────────────────────────────────────
    //  ScorePanel
    // ─────────────────────────────────────────────────

    private void RefreshScorePanel(BeatmapData beatmap)
    {
        if (beatmap == null)
        {
            SetScoreDisplay("--------", "--.--%", "-");
            return;
        }

        PlayerData playerData = GameRoot.Instance.PlayerData;
        PlayerBeatmapInfo info = playerData?.PlayerStat?.CompletedMapIds
            ?.Find(x => x.BeatmapId == beatmap.Id);

        if (info == null)
        {
            SetScoreDisplay("--------", "--.--%", "-");
        }
        else
        {
            SetScoreDisplay(
                info.HighestScore.ToString("D8"),
                $"{info.HighestAccuracy * 100f:F2}%",
                info.HighestRank.ToString()
            );
        }
    }

    private void SetScoreDisplay(string score, string acc, string rank)
    {
        if (scoreText != null) scoreText.text = score;
        if (accText != null) accText.text = acc;
        if (rankingText != null) rankingText.text = rank;
    }
}
