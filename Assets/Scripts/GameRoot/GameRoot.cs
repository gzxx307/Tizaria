using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;


/// <summary>
/// 整个游戏生命周期持续存在的类（类似UE的GameInstance）
/// </summary>
public class GameRoot : MonoBehaviour
{
    public static GameRoot Instance;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        SceneManager.LoadScene("SplashScene");
        
    }

    private void Start()
    {
    }

    // 当前游戏的Player数据
    public PlayerData PlayerData { get; private set; }
    // 设置PlayerData
    public void SetPlayerData(PlayerData playerData)
    {
        PlayerData = playerData;
    }
    // 清除PlayerData，切换账号和退出游戏用
    public void ClearPlayerData()
    {
        PlayerData = null;
    }
    
    // 只读谱面列表
    private List<BeatmapDataSet> _beatmapSets = new List<BeatmapDataSet>();
    public List<BeatmapDataSet> BeatmapSets => _beatmapSets;
    // 设置BeatmapSets
    public void SetBeatmapSets(List<BeatmapDataSet> beatmapSets)
    {
        if (beatmapSets == null) return;
        _beatmapSets.Clear();
        _beatmapSets.AddRange(beatmapSets);
    }
    // 动态添加谱面个数，导入谱面时调用
    public void AddBeatmapSet(BeatmapDataSet beatmapSet)
    {
        if (beatmapSet == null || _beatmapSets.Contains(beatmapSet)) return;
        _beatmapSets.Add(beatmapSet);
    }
    
    // 选择的谱面集合
    public BeatmapDataSet SelectedBeatmapDataSet { get; private set; }
    // 选择的谱面
    public BeatmapData SelectedBeatmapData { get; private set; }
    // 更新选择状态
    public void SetSelection(BeatmapDataSet set, BeatmapData data)
    {
        SelectedBeatmapDataSet = set;
        SelectedBeatmapData = data;
    }
    // 清除选择状态
    public void ClearSelection()
    {
        SelectedBeatmapDataSet = null;
        SelectedBeatmapData = null;
    }
    
    // 当前PlaySession
    public PlaySession CurrentSession { get; private set; }
    // 开始Session
    public void BeginPlaySession(PlaySession session)
    {
        CurrentSession = session;
    }
    // 结束 / 销毁Session
    public void EndPlaySession()
    {
        CurrentSession = null;
    }
}