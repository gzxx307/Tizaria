using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 挂载在关键帧标记按钮上，记录该关键帧的类型与时间，点击后删除对应关键帧。
/// </summary>
[RequireComponent(typeof(Button))]
public class KeyframeMarker : MonoBehaviour
{
    public enum MarkerType { BPM, SV }

    [HideInInspector] public MarkerType Type;
    [HideInInspector] public int TimeMs;
    [HideInInspector] public float Value;

    private Button _button;
    private Text _label;

    private void Awake()
    {
        _button = GetComponent<Button>();
        _button.onClick.AddListener(OnClick);
        _label = GetComponentInChildren<Text>(true);
    }

    /// <summary>
    /// 初始化关键帧标记，由控制器调用。
    /// </summary>
    public void Init(MarkerType type, int timeMs, float value)
    {
        Type = type;
        TimeMs = timeMs;
        Value = value;

        if (_label != null)
            _label.text = type == MarkerType.BPM
                ? $"{value:F1}"
                : $"×{value:F2}";
    }

    private void OnClick()
    {
        if (BeatmapEditorManager.Instance == null) return;

        if (Type == MarkerType.BPM)
            BeatmapEditorManager.Instance.RemoveBPMAt(TimeMs);
        else
            BeatmapEditorManager.Instance.RemoveSVAt(TimeMs);
    }
}
