using UnityEngine;
using UnityEngine.UI;

public class SettingsUIController : MonoBehaviour
{
    [Header("UI Panel")]
    [Tooltip("열고 닫을 설정창 패널 GameObject")]
    [SerializeField] private GameObject settingsPanel;

    [Header("Buttons (Optional)")]
    [Tooltip("설정창을 열 버튼 (선택사항)")]
    [SerializeField] private Button openButton;
    [Tooltip("설정창을 닫을 버튼 (선택사항)")]
    [SerializeField] private Button closeButton;

    [Header("Settings")]
    [Tooltip("시작할 때 설정창을 닫아둘지 여부")]
    [SerializeField] private bool hideOnStart = true;

    private void Awake()
    {
        // 버튼 이벤트 자동 연결
        if (openButton != null) openButton.onClick.AddListener(OpenSettings);
        if (closeButton != null) closeButton.onClick.AddListener(CloseSettings);
    }

    private void Start()
    {
        if (hideOnStart && settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }
    }

    /// <summary>
    /// 설정창을 엽니다.
    /// </summary>
    public void OpenSettings()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
        }
    }

    /// <summary>
    /// 설정창을 닫습니다.
    /// </summary>
    public void CloseSettings()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }
    }

    /// <summary>
    /// 하나의 버튼으로 설정창을 켰다 껐다 할 때 사용합니다.
    /// </summary>
    public void ToggleSettings()
    {
        if (settingsPanel != null)
        {
            bool isActive = settingsPanel.activeSelf;
            settingsPanel.SetActive(!isActive);
        }
    }
}