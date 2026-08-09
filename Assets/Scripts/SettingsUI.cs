using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SettingsUI : MonoBehaviour
{
    [Header("Audio UI")]
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private Slider sfxSlider;

    [Header("Display UI")]
    [SerializeField] private TMP_Dropdown resolutionDropdown;

    private void Start()
    {
        InitializeUI();
    }

    private void InitializeUI()
    {
        var manager = SettingsManager.Instance;
        if (manager == null) return;

        // --- 볼륨 슬라이더 값 불러오기 및 이벤트 바인딩 ---
        masterSlider.value = manager.GetSavedVolume("MasterVolume", 1f);
        bgmSlider.value = manager.GetSavedVolume("BGMVolume", 1f);
        sfxSlider.value = manager.GetSavedVolume("SFXVolume", 1f);

        masterSlider.onValueChanged.AddListener(val => manager.SetVolume("MasterVolume", val));
        bgmSlider.onValueChanged.AddListener(val => manager.SetVolume("BGMVolume", val));
        sfxSlider.onValueChanged.AddListener(val => manager.SetVolume("SFXVolume", val));

        // --- 해상도 드롭다운 옵션 채우기 ---
        resolutionDropdown.ClearOptions();
        List<string> options = new List<string>();

        int currentResIdx = PlayerPrefs.GetInt("ResIdx", manager.Resolutions.Count - 1);

        for (int i = 0; i < manager.Resolutions.Count; i++)
        {
            var res = manager.Resolutions[i];
            options.Add($"{res.width} x {res.height}");
        }

        resolutionDropdown.AddOptions(options);
        resolutionDropdown.value = Mathf.Clamp(currentResIdx, 0, manager.Resolutions.Count - 1);
        resolutionDropdown.RefreshShownValue();

        // --- 해상도 이벤트 바인딩 ---
        resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
    }

    private void OnResolutionChanged(int index)
    {
        SettingsManager.Instance.SetResolution(index);
    }
}