using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance { get; private set; }

    [Header("Audio Settings")]
    [SerializeField] private AudioMixer mainMixer;

    // PlayerPrefs Keys
    private const string KEY_MASTER_VOL = "MasterVol";
    private const string KEY_BGM_VOL = "BGMVol";
    private const string KEY_SFX_VOL = "SFXVol";
    private const string KEY_RESOLUTION_IDX = "ResIdx";

    public List<Resolution> Resolutions { get; private set; } = new List<Resolution>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitResolutions();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        LoadSettings();
    }

    #region Resolution Methods
    private void InitResolutions()
    {
        Resolutions.Clear();
        Resolution[] allResolutions = Screen.resolutions;

        // 주사율(Hz) 차이로 중복 표시되는 해상도를 가로/세로 기준 중복 제거
        HashSet<string> resolutionHashSet = new HashSet<string>();

        foreach (var res in allResolutions)
        {
            string resString = $"{res.width}x{res.height}";
            if (!resolutionHashSet.Contains(resString))
            {
                resolutionHashSet.Add(resString);
                Resolutions.Add(res);
            }
        }
    }

    // 항상 전체 화면(FullScreenMode.FullScreenWindow 또는 true)으로 적용
    public void SetResolution(int index)
    {
        if (index < 0 || index >= Resolutions.Count) return;

        Resolution res = Resolutions[index];
        Screen.SetResolution(res.width, res.height, FullScreenMode.FullScreenWindow);

        // 에디터 콘솔에서 적용 여부 확인용 로그
        Debug.Log($"[해상도 변경 적용] {res.width} x {res.height}");

        PlayerPrefs.SetInt(KEY_RESOLUTION_IDX, index);
        PlayerPrefs.Save();
    }
    #endregion

    #region Audio Methods
    // Slider 값(0.0001 ~ 1.0)을 Decibel(-80dB ~ 0dB) 로 변환
    public void SetVolume(string parameterName, float sliderValue)
    {
        float db = Mathf.Log10(Mathf.Max(0.0001f, sliderValue)) * 20f;
        mainMixer.SetFloat(parameterName, db);

        PlayerPrefs.SetFloat(parameterName, sliderValue);
        PlayerPrefs.Save();
    }

    public float GetSavedVolume(string parameterName, float defaultValue = 1f)
    {
        return PlayerPrefs.GetFloat(parameterName, defaultValue);
    }
    #endregion

    private void LoadSettings()
    {
        // 1. 볼륨 불러오기 및 적용
        float masterVol = GetSavedVolume("MasterVolume", 1f);
        float bgmVol = GetSavedVolume("BGMVolume", 1f);
        float sfxVol = GetSavedVolume("SFXVolume", 1f);

        SetVolume("MasterVolume", masterVol);
        SetVolume("BGMVolume", bgmVol);
        SetVolume("SFXVolume", sfxVol);

        // 2. 해상도 불러오기 및 적용
        int savedResIdx = PlayerPrefs.GetInt(KEY_RESOLUTION_IDX, -1);

        if (savedResIdx == -1)
        {
            // 기본값: 현재 모니터 해상도와 일치하는 항목 찾기
            for (int i = 0; i < Resolutions.Count; i++)
            {
                if (Resolutions[i].width == Screen.currentResolution.width &&
                    Resolutions[i].height == Screen.currentResolution.height)
                {
                    savedResIdx = i;
                    break;
                }
            }
            if (savedResIdx == -1) savedResIdx = Resolutions.Count - 1;
        }

        SetResolution(savedResIdx);
    }
}