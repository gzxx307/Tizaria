using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LoginManager : MonoBehaviour
{
    // ─────────────────────────────────────────────────
    //  Inspector 引用
    // ─────────────────────────────────────────────────

    [Header("面板")]
    [SerializeField] private GameObject loginSignupPanel;
    [SerializeField] private GameObject loginPanel;
    [SerializeField] private GameObject signupPanel;

    [Header("LoginSignupPanel 按钮")]
    [SerializeField] private Button loginButton;
    [SerializeField] private Button signupButton;

    [Header("LoginPanel")]
    [SerializeField] private Button loginBackButton;
    [SerializeField] private Transform playerListContent;  // ScrollView > Viewport > Content
    [SerializeField] private PlayerItemUI playerItemTemplate; // Player1 模板

    [Header("SignupPanel")]
    [SerializeField] private Button signupBackButton;
    [SerializeField] private Button confirmButton;
    [SerializeField] private TMP_InputField inputName;

    [Header("设置")]
    [Tooltip("登录或注册成功后跳转的场景名")]
    [SerializeField] private string nextSceneName = "SongSelectScene";

    // ─────────────────────────────────────────────────
    //  私有状态
    // ─────────────────────────────────────────────────

    private readonly List<PlayerItemUI> _spawnedItems = new List<PlayerItemUI>();

    // ─────────────────────────────────────────────────
    //  生命周期
    // ─────────────────────────────────────────────────

    private void Awake()
    {
        // 初始面板状态
        ShowPanel(PanelType.LoginSignup);
    }

    private void Start()
    {
        BindButtons();
        BuildPlayerList();
    }

    // ─────────────────────────────────────────────────
    //  按钮绑定
    // ─────────────────────────────────────────────────

    private void BindButtons()
    {
        loginButton.onClick.AddListener(OnLoginButtonClicked);
        signupButton.onClick.AddListener(OnSignupButtonClicked);
        loginBackButton.onClick.AddListener(OnBackClicked);
        signupBackButton.onClick.AddListener(OnBackClicked);
        confirmButton.onClick.AddListener(OnConfirmButtonClicked);
    }

    // ─────────────────────────────────────────────────
    //  面板切换
    // ─────────────────────────────────────────────────

    private enum PanelType { LoginSignup, Login, Signup }

    private void ShowPanel(PanelType panel)
    {
        loginSignupPanel.SetActive(panel == PanelType.LoginSignup);
        loginPanel.SetActive(panel == PanelType.Login);
        signupPanel.SetActive(panel == PanelType.Signup);
    }

    private void OnLoginButtonClicked()  => ShowPanel(PanelType.Login);
    private void OnSignupButtonClicked() => ShowPanel(PanelType.Signup);
    private void OnBackClicked()         => ShowPanel(PanelType.LoginSignup);

    // ─────────────────────────────────────────────────
    //  LoginPanel：玩家列表
    // ─────────────────────────────────────────────────

    private void BuildPlayerList()
    {
        if (PlayerDataManager.Instance == null)
        {
            Debug.LogError("[LoginManager] 未找到 PlayerDataManager，请确保场景中存在该对象！");
            return;
        }

        PlayerDataSet dataSet = PlayerDataManager.Instance.DataSet;

        // 清理旧列表项
        foreach (var item in _spawnedItems)
            if (item != null) Destroy(item.gameObject);
        _spawnedItems.Clear();

        // 隐藏模板
        playerItemTemplate.gameObject.SetActive(false);

        if (dataSet?.Players == null || dataSet.Players.Count == 0)
        {
            // TODO: 可以在此显示"还没有玩家，请先注册"提示
            Debug.Log("[LoginManager] 当前没有玩家档案");
            return;
        }

        foreach (var player in dataSet.Players)
        {
            PlayerItemUI item = Instantiate(playerItemTemplate, playerListContent);
            item.gameObject.SetActive(true);
            item.Setup(player, OnPlayerSelected);
            _spawnedItems.Add(item);
        }
    }

    /// <summary> 点击列表中某个玩家 → 登录并跳转 </summary>
    private void OnPlayerSelected(PlayerData player)
    {
        PlayerDataManager.Instance.UpdateLastLogin(player);
        GameRoot.Instance.SetPlayerData(player);
        SceneManager.LoadScene(nextSceneName);
    }

    // ─────────────────────────────────────────────────
    //  SignupPanel：创建新玩家
    // ─────────────────────────────────────────────────

    private void OnConfirmButtonClicked()
    {
        string name = inputName.text.Trim();

        // 验证：不能为空
        if (string.IsNullOrEmpty(name))
        {
            Debug.LogWarning("[LoginManager] 玩家名称不能为空");
            // TODO: 显示错误提示 UI
            return;
        }

        // 验证：名称不能重复
        if (PlayerDataManager.Instance.IsNameTaken(name))
        {
            Debug.LogWarning($"[LoginManager] 名称 \"{name}\" 已被使用");
            // TODO: 显示错误提示 UI
            return;
        }

        // 创建 → 保存 → 直接以该玩家身份登录
        PlayerData newPlayer = PlayerDataManager.Instance.CreateNewPlayer(name);
        PlayerDataManager.Instance.AddPlayer(newPlayer);

        GameRoot.Instance.SetPlayerData(newPlayer);
        SceneManager.LoadScene(nextSceneName);
    }
}
