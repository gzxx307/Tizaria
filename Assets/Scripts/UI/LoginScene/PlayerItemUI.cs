using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 挂载在 Player1（玩家列表模板项）上。
/// 由 LoginManager 在运行时调用 Setup() 填充数据。
/// </summary>
[RequireComponent(typeof(Button))]
public class PlayerItemUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI rksText;
    [SerializeField] private TextMeshProUGUI lastLoginText;

    private PlayerData _data;
    private Action<PlayerData> _onSelect;

    /// <summary>
    /// 填充玩家数据并绑定点击回调
    /// </summary>
    /// <param name="data"> 对应的 PlayerData </param>
    /// <param name="onSelect"> 点击时触发，传出选中的 PlayerData </param>
    public void Setup(PlayerData data, Action<PlayerData> onSelect)
    {
        _data = data;
        _onSelect = onSelect;

        if (nameText != null)
            nameText.text = data.Name;

        if (rksText != null)
            rksText.text = $"rks{data.Ranks}";

        if (lastLoginText != null)
            lastLoginText.text = data.LastLoginTime;

        var btn = GetComponent<Button>();
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(OnClicked);
    }

    private void OnClicked()
    {
        _onSelect?.Invoke(_data);
    }
}
