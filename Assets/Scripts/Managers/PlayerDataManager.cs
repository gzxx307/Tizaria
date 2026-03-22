using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 负责 PlayerDataSet 的持久化读写与玩家数据管理。
/// 挂载在 LoginScene 的 Managers 对象上，DontDestroyOnLoad 保证跨场景可用。
/// </summary>
public class PlayerDataManager : MonoBehaviour
{
    public static PlayerDataManager Instance { get; private set; }

    private const string SaveFileName = "players.json";
    private string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);

    private PlayerDataSet _dataSet;

    /// <summary> 当前加载的玩家数据集合（只读引用，修改请通过本类方法） </summary>
    public PlayerDataSet DataSet => _dataSet;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadPlayerDataSet();
    }

    // ─────────────────────────────────────────────────
    //  读写
    // ─────────────────────────────────────────────────

    /// <summary> 从磁盘加载 PlayerDataSet；文件不存在时初始化空集合 </summary>
    public void LoadPlayerDataSet()
    {
        if (File.Exists(SavePath))
        {
            try
            {
                string json = File.ReadAllText(SavePath);
                _dataSet = JsonUtility.FromJson<PlayerDataSet>(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[PlayerDataManager] 读取存档失败: {e.Message}");
                _dataSet = null;
            }
        }

        // 兜底初始化
        if (_dataSet?.Players == null)
            _dataSet = new PlayerDataSet { Players = new List<PlayerData>() };

        Debug.Log($"[PlayerDataManager] 已加载 {_dataSet.Players.Count} 个玩家档案");
    }

    /// <summary> 将当前 DataSet 序列化保存到磁盘 </summary>
    public void SavePlayerDataSet()
    {
        try
        {
            string json = JsonUtility.ToJson(_dataSet, true);
            File.WriteAllText(SavePath, json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[PlayerDataManager] 保存存档失败: {e.Message}");
        }
    }

    // ─────────────────────────────────────────────────
    //  玩家操作
    // ─────────────────────────────────────────────────

    /// <summary>
    /// 使用指定名称创建一个带默认值的新玩家（不自动添加到集合，需手动调用 AddPlayer）
    /// </summary>
    public PlayerData CreateNewPlayer(string playerName)
    {
        string now = Transformer.DateTimeToString(DateTime.Now);

        return new PlayerData
        {
            Uid = Guid.NewGuid().ToString(),
            Name = playerName,
            Ranks = 0,
            AvatarPath = "",
            CreationTime = now,
            LastLoginTime = now,

            PlayerStat = new PlayerStat
            {
                TotalPlayTime = 0f,
                TotalPlayCount = 0,
                TotalMapCount = 0,
                TotalMapCompletedCount = 0,
                CompletedMapIds = new List<PlayerBeatmapInfo>()
            },

            PlayerSetting = new PlayerSetting
            {
                // 默认键位：D F / J K
                KeyBindings = new List<KeyCode>
                {
                    KeyCode.D, KeyCode.F, KeyCode.J, KeyCode.K
                },
                GlobalMapOffset = 0f,
                MapSpeed = 1f,
                KeySize = 1f,
                KeyPosition = 0f,
                SkinId = "default",
                MasterVolume = 1f,
                MusicVolume = 1f,
                UIVolume = 1f,
                HitVolume = 1f
            }
        };
    }

    /// <summary> 将玩家添加到集合并立即保存 </summary>
    public void AddPlayer(PlayerData player)
    {
        _dataSet.Players.Add(player);
        SavePlayerDataSet();
    }

    /// <summary> 刷新玩家最后登录时间并保存 </summary>
    public void UpdateLastLogin(PlayerData player)
    {
        player.LastLoginTime = Transformer.DateTimeToString(DateTime.Now);
        SavePlayerDataSet();
    }

    /// <summary> 检查名称是否已被使用（不区分大小写） </summary>
    public bool IsNameTaken(string name)
    {
        return _dataSet.Players.Exists(
            p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
    }
}
