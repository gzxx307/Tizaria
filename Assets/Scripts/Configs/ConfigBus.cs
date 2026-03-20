using UnityEngine;

/// <summary>
/// 配置文件总线，挂载在游戏开始时
/// </summary>
public class ConfigBus : MonoBehaviour
{
    // 单例实例
    public static ConfigBus Instance { get; private set; }

    // 音符得分比例配置表
    [SerializeField] private NoteScoreSO noteScoreSo;
    // 判定时间窗口（ms）配置表
    [SerializeField] private JudgementWindowSO judgementWindowSo;
    // 结算等级判定配置表
    [SerializeField] private RankingRuleSO rankingRuleSo;

    public NoteScoreSO NoteScore => noteScoreSo;
    public JudgementWindowSO JudgementWindow => judgementWindowSo;
    public RankingRuleSO RankingRule => rankingRuleSo;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        // 跨场景存储
        DontDestroyOnLoad(gameObject);
    }
    
    private void Start()
    {
        // 防止空引用爆炸
        if (noteScoreSo == null) noteScoreSo = ScriptableObject.CreateInstance<NoteScoreSO>();
        if (judgementWindowSo == null) judgementWindowSo = ScriptableObject.CreateInstance<JudgementWindowSO>();
        if (rankingRuleSo == null) rankingRuleSo = ScriptableObject.CreateInstance<RankingRuleSO>();
    }
}