using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Optional external UI override for PauseManager.
/// Attach this to a Canvas GameObject and assign all panel/control references.
/// If PauseManager finds this component (via serialized field or scene search),
/// it will use these references instead of generating default UI at runtime.
///
/// Any field left null will fall back to the default runtime-generated equivalent.
/// </summary>
public class PauseMenuUIOverride : MonoBehaviour
{
    [Header("Root Canvas (required)")]
    public Canvas rootCanvas;

    // ========== Panels ==========
    [Header("Panels")]
    public GameObject pausePanel;
    public GameObject settingsPanel;
    public GameObject systemSettingsPanel;
    public GameObject controlOptionsPanel;
    public GameObject videoOptionsPanel;
    public GameObject audioOptionsPanel;
    public GameObject rebindPanel;

    // ========== Pause Panel Buttons ==========
    [Header("Pause Panel")]
    public Button resumeButton;
    public Button settingsButton;
    public Button quitToMenuButton;

    // ========== Settings Panel Buttons ==========
    [Header("Settings Panel")]
    public Button systemSettingsButton;
    public Button controlOptionsButton;
    public Button videoOptionsButton;
    public Button audioOptionsButton;
    public Button settingsBackButton;

    // ========== System Settings ==========
    [Header("System Settings")]
    public Button restoreDefaultsButton;
    public Button systemBackButton;

    // ========== Control Options ==========
    [Header("Control Options")]
    public Button rebindButton;
    public Slider sensitivitySlider;
    public TMP_Text sensitivityValueText;
    public Slider deadzoneSlider;
    public TMP_Text deadzoneValueText;
    public Toggle invertYToggle;
    public Button controlBackButton;

    // ========== Video Options ==========
    [Header("Video Options")]
    public TMP_Dropdown resolutionDropdown;
    public TMP_Dropdown windowModeDropdown;
    public Toggle showFPSToggle;
    public Slider fovSlider;
    public TMP_Text fovValueText;
    public Toggle shakeEnabledToggle;
    public Slider shakeIntensitySlider;
    public TMP_Text shakeIntensityValueText;
    public Button videoBackButton;

    // ========== Audio Options ==========
    [Header("Audio Options")]
    public Slider masterVolumeSlider;
    public TMP_Text masterVolumeValueText;
    public Slider sfxVolumeSlider;
    public TMP_Text sfxVolumeValueText;
    public Slider musicVolumeSlider;
    public TMP_Text musicVolumeValueText;
    public Button audioBackButton;

    // ========== Rebind Panel ==========
    [Header("Rebind Panel")]
    public Transform rebindContentParent;
    public Button rebindBackButton;
}