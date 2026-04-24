using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using System.Collections.Generic;
using UnityEngine.EventSystems;

/// <summary>
/// Pause menu system with settings sub-panels.
/// Pause action (default ESC) to open/close. Reads from InputAction map for rebindability.
/// If pressed during clone/trace state, cleanly exits that state first.
///
/// External UI Override:
///   Assign a PauseMenuUIOverride component to uiOverride in the Inspector.
///   If present, the manager binds to those external references instead of
///   generating its own UI. Any null field in the override falls back to
///   the default runtime-generated element.
/// </summary>
public class PauseManager : MonoBehaviour
{
    public static PauseManager Instance { get; private set; }

    [Header("External UI (optional)")]
    [Tooltip("Assign a PauseMenuUIOverride to use a custom UI. Leave null to use the default runtime-generated UI.")]
    public PauseMenuUIOverride uiOverride;

    private bool isPaused = false;
    private float savedTimeScale = 1f;
    private bool usingExternalUI = false;

    // Input action for pause (read from PlayerControls Gameplay map)
    private InputAction pauseAction;

    // UI root
    private Canvas canvas;
    private GameObject pausePanel;
    private GameObject settingsPanel;
    private GameObject systemSettingsPanel;
    private GameObject controlOptionsPanel;
    private GameObject videoOptionsPanel;
    private GameObject audioOptionsPanel;
    private GameObject rebindPanel;
    private Transform rebindContentParent;

    // Panel stack for back navigation
    private Stack<GameObject> panelStack = new Stack<GameObject>();
    private GameObject currentPanel;

    // Cached references for settings UI
    private Slider sensitivitySlider;
    private TMP_Text sensitivityValueText;
    private Toggle invertYToggle;
    private Slider deadzoneSlider;
    private TMP_Text deadzoneValueText;
    private TMP_Dropdown resolutionDropdown;
    private TMP_Dropdown windowModeDropdown;
    private Toggle fpsToggle;
    private Slider fovSlider;
    private TMP_Text fovValueText;
    private Toggle shakeEnabledToggle;
    private Slider shakeIntensitySlider;
    private TMP_Text shakeIntensityValueText;
    private Toggle stateDebugToggle;
    private Toggle entityDetectionToggle;
    private Toggle hotkeyDisplayToggle;
    private Toggle versionDisplayToggle;

    // 音量控件
    private Slider masterVolumeSlider;
    private TMP_Text masterVolumeValueText;
    private Slider sfxVolumeSlider;
    private TMP_Text sfxVolumeValueText;
    private Slider musicVolumeSlider;
    private TMP_Text musicVolumeValueText;

    // Resolution list
    private List<Resolution> availableResolutions = new List<Resolution>();

    // Rebind state
    private InputActionRebindingExtensions.RebindingOperation currentRebindOp;
    private PlayerInput playerInput;

    // Colors (used by default UI generation only)
    private static readonly Color PANEL_BG = new Color(0.05f, 0.05f, 0.1f, 0.92f);
    private static readonly Color BUTTON_NORMAL = new Color(0.15f, 0.15f, 0.25f, 1f);
    private static readonly Color BUTTON_HIGHLIGHT = new Color(0.25f, 0.25f, 0.45f, 1f);
    private static readonly Color ACCENT = new Color(0.4f, 0.7f, 1f, 1f);
    private static readonly Color TEXT_COLOR = Color.white;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        SetupPauseAction();

        if (uiOverride != null && uiOverride.rootCanvas != null)
            BindExternalUI();
        else
            BuildDefaultUI();

        GameSettings.ApplyAll();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        currentRebindOp?.Dispose();
        if (pauseAction != null)
        {
            pauseAction.performed -= OnPausePerformed;
            pauseAction.Disable();
        }
    }

    /// <summary>
    /// Try to find the Pause action from the player's InputActionAsset.
    /// Falls back to a standalone action bound to Escape if not found.
    /// </summary>
    private void SetupPauseAction()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerInput = player.GetComponent<PlayerInput>();
            if (playerInput != null && playerInput.actions != null)
            {
                pauseAction = playerInput.actions.FindAction("Gameplay/Pause");
            }
        }

        // Fallback: create a standalone action if not found in the asset
        if (pauseAction == null)
        {
            var map = new InputActionMap("PauseMap");
            pauseAction = map.AddAction("Pause", binding: "<Keyboard>/escape");
            map.Enable();
        }

        pauseAction.performed -= OnPausePerformed;
        pauseAction.performed += OnPausePerformed;
        pauseAction.Enable();
    }

    private void OnPausePerformed(InputAction.CallbackContext ctx)
    {
        if (isPaused)
        {
            ResumeGame();
            return;
        }

        // If in clone/trace state, cleanly exit that state first
        if (!IsInNormalState())
        {
            ForceExitAllCloneStates();
            return;
        }

        TryPause();
    }

    /// <summary>
    /// Cleanly exits any active clone or trace-phantom state without
    /// solid generation, carrier swap, or recorded sequence preservation.
    /// </summary>
    private void ForceExitAllCloneStates()
    {
        var traceManager = FindFirstObjectByType<TraceCloneManager>();
        if (traceManager != null && traceManager.IsPhantomActive())
        {
            traceManager.ForceExitClean();
            return;
        }

        var cloneManager = FindFirstObjectByType<CloneManager>();
        if (cloneManager != null && cloneManager.IsTimeStopped())
        {
            cloneManager.ForceExitTimeStop(false);
            return;
        }

        if (TimeStopManager.Instance != null && TimeStopManager.Instance.IsTimeStopped)
        {
            TimeStopManager.Instance.Resume();
        }
    }

    private bool IsInNormalState()
    {
        if (TimeStopManager.Instance != null && TimeStopManager.Instance.IsTimeStopped)
            return false;

        var cloneManager = FindFirstObjectByType<CloneManager>();
        if (cloneManager != null && cloneManager.IsTimeStopped())
            return false;

        var traceManager = FindFirstObjectByType<TraceCloneManager>();
        if (traceManager != null && (traceManager.IsTimeStopped() || traceManager.IsPhantomActive()))
            return false;

        return true;
    }

    public void TryPause()
    {
        if (isPaused) return;
        if (!IsInNormalState()) return;

        isPaused = true;
        savedTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        var camOrbit = Camera.main?.GetComponent<SimpleCameraOrbit>();
        if (camOrbit != null)
            camOrbit.controlsEnabled = false;

        // Disable player movement during pause
        var pc = GameObject.FindGameObjectWithTag("Player")?.GetComponent<PlayerController>();
        if (pc != null) pc.canMove = false;

        ShowPanel(pausePanel);
        canvas.gameObject.SetActive(true);
    }

    public void ResumeGame()
    {
        if (!isPaused) return;

        isPaused = false;
        Time.timeScale = savedTimeScale;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        var camOrbit = Camera.main?.GetComponent<SimpleCameraOrbit>();
        if (camOrbit != null)
            camOrbit.controlsEnabled = true;

        var pc = GameObject.FindGameObjectWithTag("Player")?.GetComponent<PlayerController>();
        if (pc != null) pc.canMove = true;

        canvas.gameObject.SetActive(false);
        panelStack.Clear();

        GameSettings.Save();
        GameSettings.ApplyAll();
    }

    public bool IsPaused => isPaused;

    // ========== Panel Navigation ==========

    private void ShowPanel(GameObject panel)
    {
        if (currentPanel != null)
        {
            panelStack.Push(currentPanel);
            currentPanel.SetActive(false);
        }
        currentPanel = panel;
        currentPanel.SetActive(true);
    }

    private void GoBack()
    {
        if (panelStack.Count > 0)
        {
            currentPanel.SetActive(false);
            currentPanel = panelStack.Pop();
            currentPanel.SetActive(true);
        }
    }

    // ================================================================
    //  EXTERNAL UI BINDING
    // ================================================================

    private void BindExternalUI()
    {
        usingExternalUI = true;
        var ov = uiOverride;
        canvas = ov.rootCanvas;

        pausePanel = ov.pausePanel;
        settingsPanel = ov.settingsPanel;
        systemSettingsPanel = ov.systemSettingsPanel;
        controlOptionsPanel = ov.controlOptionsPanel;
        videoOptionsPanel = ov.videoOptionsPanel;
        audioOptionsPanel = ov.audioOptionsPanel;
        rebindPanel = ov.rebindPanel;
        rebindContentParent = ov.rebindContentParent;

        // Bind pause panel buttons
        ov.resumeButton?.onClick.AddListener(() => ResumeGame());
        AttachUISoundToButton(ov.resumeButton);
        ov.settingsButton?.onClick.AddListener(() => ShowPanel(settingsPanel));
        AttachUISoundToButton(ov.settingsButton);
        ov.quitToMenuButton?.onClick.AddListener(() =>
        {
            Debug.Log("[PauseManager] Exit to main menu - not yet implemented");
        });
        AttachUISoundToButton(ov.quitToMenuButton);

        // Bind settings panel buttons
        ov.systemSettingsButton?.onClick.AddListener(() => ShowPanel(systemSettingsPanel));
        AttachUISoundToButton(ov.systemSettingsButton);
        ov.controlOptionsButton?.onClick.AddListener(() => ShowPanel(controlOptionsPanel));
        AttachUISoundToButton(ov.controlOptionsButton);
        ov.videoOptionsButton?.onClick.AddListener(() => ShowPanel(videoOptionsPanel));
        AttachUISoundToButton(ov.videoOptionsButton);
        ov.audioOptionsButton?.onClick.AddListener(() => ShowPanel(audioOptionsPanel));
        AttachUISoundToButton(ov.audioOptionsButton);
        ov.settingsBackButton?.onClick.AddListener(() => GoBack());
        AttachUISoundToButton(ov.settingsBackButton);

        // System settings
        ov.restoreDefaultsButton?.onClick.AddListener(() =>
        {
            GameSettings.RestoreDefaults();
            GameSettings.ApplyAll();
            RefreshAllSettingsUI();
        });
        AttachUISoundToButton(ov.restoreDefaultsButton);
        ov.systemBackButton?.onClick.AddListener(() => GoBack());
        AttachUISoundToButton(ov.systemBackButton);

        // Control options
        ov.rebindButton?.onClick.AddListener(() =>
        {
            RefreshRebindPanel();
            ShowPanel(rebindPanel);
        });
        AttachUISoundToButton(ov.rebindButton);

        sensitivitySlider = ov.sensitivitySlider;
        sensitivityValueText = ov.sensitivityValueText;
        deadzoneSlider = ov.deadzoneSlider;
        deadzoneValueText = ov.deadzoneValueText;
        invertYToggle = ov.invertYToggle;

        BindControlListeners();
        ov.controlBackButton?.onClick.AddListener(() => { GameSettings.Save(); GoBack(); });
        AttachUISoundToButton(ov.controlBackButton);

        // Video options
        resolutionDropdown = ov.resolutionDropdown;
        windowModeDropdown = ov.windowModeDropdown;
        fpsToggle = ov.showFPSToggle;
        fovSlider = ov.fovSlider;
        fovValueText = ov.fovValueText;
        shakeEnabledToggle = ov.shakeEnabledToggle;
        shakeIntensitySlider = ov.shakeIntensitySlider;
        shakeIntensityValueText = ov.shakeIntensityValueText;

        BindVideoListeners();
        ov.videoBackButton?.onClick.AddListener(() => { GameSettings.Save(); GoBack(); });
        AttachUISoundToButton(ov.videoBackButton);

        // Audio options
        masterVolumeSlider = ov.masterVolumeSlider;
        masterVolumeValueText = ov.masterVolumeValueText;
        sfxVolumeSlider = ov.sfxVolumeSlider;
        sfxVolumeValueText = ov.sfxVolumeValueText;
        musicVolumeSlider = ov.musicVolumeSlider;
        musicVolumeValueText = ov.musicVolumeValueText;

        BindAudioListeners();
        ov.audioBackButton?.onClick.AddListener(() => { GameSettings.Save(); GoBack(); });
        AttachUISoundToButton(ov.audioBackButton);

        // Rebind back
        ov.rebindBackButton?.onClick.AddListener(() => { GameSettings.Save(); GoBack(); });
        AttachUISoundToButton(ov.rebindBackButton);

        // Hide all panels
        if (pausePanel != null) pausePanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (systemSettingsPanel != null) systemSettingsPanel.SetActive(false);
        if (controlOptionsPanel != null) controlOptionsPanel.SetActive(false);
        if (videoOptionsPanel != null) videoOptionsPanel.SetActive(false);
        if (audioOptionsPanel != null) audioOptionsPanel.SetActive(false);
        if (rebindPanel != null) rebindPanel.SetActive(false);
        canvas.gameObject.SetActive(false);

        RefreshAllSettingsUI();
    }

    // ================================================================
    //  SHARED LISTENER BINDING (used by both external and default UI)
    // ================================================================

    private void BindControlListeners()
    {
        if (sensitivitySlider != null)
        {
            sensitivitySlider.minValue = 1f;
            sensitivitySlider.maxValue = 30f;
            sensitivitySlider.value = GameSettings.Sensitivity;
            sensitivitySlider.onValueChanged.AddListener((v) =>
            {
                GameSettings.Sensitivity = v;
                if (sensitivityValueText != null) sensitivityValueText.text = v.ToString("F1");
                var orbit = Camera.main?.GetComponent<SimpleCameraOrbit>();
                if (orbit != null) orbit.lookSensitivity = v;
            });
            AttachUISoundToSlider(sensitivitySlider);
        }
        if (deadzoneSlider != null)
        {
            deadzoneSlider.minValue = 0.01f;
            deadzoneSlider.maxValue = 0.5f;
            deadzoneSlider.value = GameSettings.GamepadDeadzone;
            deadzoneSlider.onValueChanged.AddListener((v) =>
            {
                GameSettings.GamepadDeadzone = v;
                if (deadzoneValueText != null) deadzoneValueText.text = v.ToString("F2");
                InputSystem.settings.defaultDeadzoneMin = v;
            });
            AttachUISoundToSlider(deadzoneSlider);
        }
        if (invertYToggle != null)
        {
            invertYToggle.isOn = GameSettings.InvertY;
            invertYToggle.onValueChanged.AddListener((v) =>
            {
                GameSettings.InvertY = v;
                var orbit = Camera.main?.GetComponent<SimpleCameraOrbit>();
                if (orbit != null) orbit.invertY = v;
            });
            AttachUISoundToToggle(invertYToggle);
        }
    }

    private void BindVideoListeners()
    {
        if (resolutionDropdown != null)
        {
            PopulateResolutions();
            resolutionDropdown.onValueChanged.AddListener((idx) =>
            {
                if (idx >= 0 && idx < availableResolutions.Count)
                {
                    var res = availableResolutions[idx];
                    GameSettings.ResolutionWidth = res.width;
                    GameSettings.ResolutionHeight = res.height;
                    ApplyResolutionAndMode();
                }
            });
            AttachUISoundToDropdown(resolutionDropdown);
        }
        if (windowModeDropdown != null)
        {
            windowModeDropdown.ClearOptions();
            windowModeDropdown.AddOptions(new List<string> { "Windowed", "Fullscreen Window", "Fullscreen" });
            windowModeDropdown.value = GameSettings.FullscreenMode;
            windowModeDropdown.onValueChanged.AddListener((idx) =>
            {
                GameSettings.FullscreenMode = idx;
                ApplyResolutionAndMode();
            });
            AttachUISoundToDropdown(windowModeDropdown);
        }
        if (fpsToggle != null)
        {
            fpsToggle.isOn = GameSettings.ShowFPS;
            fpsToggle.onValueChanged.AddListener((v) =>
            {
                GameSettings.ShowFPS = v;
                var fpsDisplay = FindFirstObjectByType<FPSDisplay>();
                if (fpsDisplay != null) fpsDisplay.SetVisible(v);
            });
            AttachUISoundToToggle(fpsToggle);
        }
        if (fovSlider != null)
        {
            fovSlider.minValue = 40f;
            fovSlider.maxValue = 120f;
            fovSlider.value = GameSettings.FOV;
            fovSlider.onValueChanged.AddListener((v) =>
            {
                GameSettings.FOV = v;
                if (fovValueText != null) fovValueText.text = Mathf.RoundToInt(v).ToString();
                if (Camera.main != null) Camera.main.fieldOfView = v;
            });
            AttachUISoundToSlider(fovSlider);
        }
        if (shakeEnabledToggle != null)
        {
            shakeEnabledToggle.isOn = GameSettings.ShakeEnabled;
            shakeEnabledToggle.onValueChanged.AddListener((v) =>
            {
                GameSettings.ShakeEnabled = v;
                var orbit = Camera.main?.GetComponent<SimpleCameraOrbit>();
                if (orbit != null) orbit.enableShake = v;
            });
            AttachUISoundToToggle(shakeEnabledToggle);
        }
        if (shakeIntensitySlider != null)
        {
            shakeIntensitySlider.minValue = 0f;
            shakeIntensitySlider.maxValue = 2f;
            shakeIntensitySlider.value = GameSettings.ShakeIntensity;
            shakeIntensitySlider.onValueChanged.AddListener((v) =>
            {
                GameSettings.ShakeIntensity = v;
                if (shakeIntensityValueText != null) shakeIntensityValueText.text = v.ToString("F1");
                var orbit = Camera.main?.GetComponent<SimpleCameraOrbit>();
                if (orbit != null) orbit.shakeIntensityMultiplier = v;
            });
            AttachUISoundToSlider(shakeIntensitySlider);
        }
    }

    private void BindAudioListeners()
    {
        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.minValue = 0f;
            masterVolumeSlider.maxValue = 1f;
            masterVolumeSlider.value = GameSettings.MasterVolume;
            masterVolumeSlider.onValueChanged.AddListener((v) => {
                GameSettings.MasterVolume = v;
                if (masterVolumeValueText != null) masterVolumeValueText.text = Mathf.RoundToInt(v * 100) + "%";
                if (AudioManager.Instance != null) AudioManager.Instance.SetMasterVolume(v);
                if (MusicManager.Instance != null) MusicManager.Instance.SetMasterVolume(v); // 联动音乐
            });
            AttachUISoundToSlider(masterVolumeSlider);
        }
        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.minValue = 0f;
            sfxVolumeSlider.maxValue = 1f;
            sfxVolumeSlider.value = GameSettings.SFXVolume;
            sfxVolumeSlider.onValueChanged.AddListener((v) => {
                GameSettings.SFXVolume = v;
                if (sfxVolumeValueText != null) sfxVolumeValueText.text = Mathf.RoundToInt(v * 100) + "%";
                if (AudioManager.Instance != null) AudioManager.Instance.SetSFXVolume(v);
            });
            AttachUISoundToSlider(sfxVolumeSlider);
        }
        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.minValue = 0f;
            musicVolumeSlider.maxValue = 1f;
            musicVolumeSlider.value = GameSettings.MusicVolume;
            musicVolumeSlider.onValueChanged.AddListener((v) => {
                GameSettings.MusicVolume = v;
                if (musicVolumeValueText != null) musicVolumeValueText.text = Mathf.RoundToInt(v * 100) + "%";
                if (MusicManager.Instance != null) MusicManager.Instance.SetVolume(v);
            });
            AttachUISoundToSlider(musicVolumeSlider);
        }
    }

    // ================================================================
    //  UI SOUND ATTACHMENT
    // ================================================================

    private void AttachUISoundToButton(Button btn)
    {
        if (btn == null) return;
        btn.onClick.AddListener(() => {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayUIClick();
        });
    }

    private void AttachUISoundToToggle(Toggle toggle)
    {
        if (toggle == null) return;
        toggle.onValueChanged.AddListener((_) => {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayUIClick();
        });
    }

    private void AttachUISoundToSlider(Slider slider)
    {
        if (slider == null) return;
        var trigger = slider.gameObject.GetComponent<EventTrigger>();
        if (trigger == null) trigger = slider.gameObject.AddComponent<EventTrigger>();
        var entry = new EventTrigger.Entry();
        entry.eventID = EventTriggerType.PointerUp;
        entry.callback.AddListener((_) => {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayUIClick();
        });
        trigger.triggers.Add(entry);
    }

    private void AttachUISoundToDropdown(TMP_Dropdown dropdown)
    {
        if (dropdown == null) return;
        dropdown.onValueChanged.AddListener((_) => {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayUIClick();
        });
    }

    // ================================================================
    //  DEFAULT UI BUILDING (runtime-generated fallback)
    // ================================================================

    private void BuildDefaultUI()
    {
        usingExternalUI = false;

        var canvasObj = new GameObject("PauseMenuCanvas");
        canvasObj.transform.SetParent(transform);
        canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasObj.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
        canvasObj.AddComponent<GraphicRaycaster>();

        pausePanel = BuildPausePanel(canvasObj.transform);
        settingsPanel = BuildSettingsPanel(canvasObj.transform);
        systemSettingsPanel = BuildSystemSettingsPanel(canvasObj.transform);
        controlOptionsPanel = BuildControlOptionsPanel(canvasObj.transform);
        videoOptionsPanel = BuildVideoOptionsPanel(canvasObj.transform);
        audioOptionsPanel = BuildAudioOptionsPanel(canvasObj.transform);
        rebindPanel = BuildRebindPanel(canvasObj.transform);

        pausePanel.SetActive(false);
        settingsPanel.SetActive(false);
        systemSettingsPanel.SetActive(false);
        controlOptionsPanel.SetActive(false);
        videoOptionsPanel.SetActive(false);
        audioOptionsPanel.SetActive(false);
        rebindPanel.SetActive(false);
        canvasObj.SetActive(false);
    }

    // ----- Pause Panel -----
    private GameObject BuildPausePanel(Transform parent)
    {
        var panel = CreatePanel(parent, "PausePanel", 400, 350);

        var title = CreateTitle(panel.transform, "PAUSED", 42);
        SetAnchored(title, 0, 120, 350, 50);

        var btnContinue = CreateButton(panel.transform, "Resume", () => ResumeGame());
        SetAnchored(btnContinue, 0, 40, 300, 50);

        var btnSettings = CreateButton(panel.transform, "Settings", () => ShowPanel(settingsPanel));
        SetAnchored(btnSettings, 0, -30, 300, 50);

        var btnQuit = CreateButton(panel.transform, "Quit to Main Menu", () =>
        {
            Debug.Log("[PauseManager] Exit to main menu - not yet implemented");
        });
        SetAnchored(btnQuit, 0, -100, 300, 50);

        return panel;
    }

    // ----- Settings Panel -----
    private GameObject BuildSettingsPanel(Transform parent)
    {
        var panel = CreatePanel(parent, "SettingsPanel", 450, 450);

        var title = CreateTitle(panel.transform, "Settings", 38);
        SetAnchored(title, 0, 170, 400, 50);

        var btnSystem = CreateButton(panel.transform, "System", () => ShowPanel(systemSettingsPanel));
        SetAnchored(btnSystem, 0, 90, 340, 50);

        var btnControl = CreateButton(panel.transform, "Controls", () => ShowPanel(controlOptionsPanel));
        SetAnchored(btnControl, 0, 25, 340, 50);

        var btnVideo = CreateButton(panel.transform, "Video", () => ShowPanel(videoOptionsPanel));
        SetAnchored(btnVideo, 0, -40, 340, 50);

        var btnAudio = CreateButton(panel.transform, "Audio", () => ShowPanel(audioOptionsPanel));
        SetAnchored(btnAudio, 0, -105, 340, 50);

        var btnBack = CreateButton(panel.transform, "Back", () => GoBack());
        SetAnchored(btnBack, 0, -175, 340, 50);

        return panel;
    }

    // ----- System Settings -----
    private GameObject BuildSystemSettingsPanel(Transform parent)
    {
        var panel = CreatePanel(parent, "SystemSettingsPanel", 700, 580);

        var title = CreateTitle(panel.transform, "System Settings", 36);
        SetAnchored(title, 0, 250, 650, 45);

        float y = 185;

        // Show FPS (moved from Video)
        var fpsRow = CreateSettingRow(panel.transform, "Show FPS", y, 300);
        fpsToggle = CreateToggle(fpsRow.transform, GameSettings.ShowFPS, (v) =>
        {
            GameSettings.ShowFPS = v;
            var fpsDisplay = FindFirstObjectByType<FPSDisplay>();
            if (fpsDisplay != null) fpsDisplay.SetVisible(v);
        });
        SetAnchored(fpsRow, 0, y, 600, 40);
        y -= 55;

        // State Debug overlay
        var stateRow = CreateSettingRow(panel.transform, "State Debug Overlay", y, 300);
        stateDebugToggle = CreateToggle(stateRow.transform, GameSettings.ShowStateDebug, (v) =>
        {
            GameSettings.ShowStateDebug = v;
            var overlay = FindFirstObjectByType<DebugOverlayManager>();
            if (overlay != null) overlay.RefreshVisibility();
        });
        SetAnchored(stateRow, 0, y, 600, 40);
        y -= 55;

        // Entity Detection overlay
        var entityRow = CreateSettingRow(panel.transform, "Entity Detection Overlay", y, 300);
        entityDetectionToggle = CreateToggle(entityRow.transform, GameSettings.ShowEntityDetection, (v) =>
        {
            GameSettings.ShowEntityDetection = v;
            var overlay = FindFirstObjectByType<DebugOverlayManager>();
            if (overlay != null) overlay.RefreshVisibility();
        });
        SetAnchored(entityRow, 0, y, 600, 40);
        y -= 55;

        // Hotkey Display overlay
        var hotkeyRow = CreateSettingRow(panel.transform, "Hotkey Display", y, 300);
        hotkeyDisplayToggle = CreateToggle(hotkeyRow.transform, GameSettings.ShowHotkeyDisplay, (v) =>
        {
            GameSettings.ShowHotkeyDisplay = v;
            var overlay = FindFirstObjectByType<DebugOverlayManager>();
            if (overlay != null) overlay.RefreshVisibility();
        });
        SetAnchored(hotkeyRow, 0, y, 600, 40);
        y -= 55;

        // Version Display overlay
        var versionRow = CreateSettingRow(panel.transform, "Version Display", y, 300);
        versionDisplayToggle = CreateToggle(versionRow.transform, GameSettings.ShowVersion, (v) =>
        {
            GameSettings.ShowVersion = v;
            var overlay = FindFirstObjectByType<DebugOverlayManager>();
            if (overlay != null) overlay.RefreshVisibility();
        });
        SetAnchored(versionRow, 0, y, 600, 40);
        y -= 65;

        var btnRestore = CreateButton(panel.transform, "Restore Defaults", () =>
        {
            GameSettings.RestoreDefaults();
            GameSettings.ApplyAll();
            RefreshAllSettingsUI();
        });
        SetAnchored(btnRestore, 0, y, 400, 50);
        y -= 65;

        var btnBack = CreateButton(panel.transform, "Back", () => GoBack());
        SetAnchored(btnBack, 0, y, 400, 50);

        return panel;
    }

    // ----- Control Options -----
    private GameObject BuildControlOptionsPanel(Transform parent)
    {
        var panel = CreatePanel(parent, "ControlOptionsPanel", 720, 520);

        var title = CreateTitle(panel.transform, "Controls", 36);
        SetAnchored(title, 0, 215, 670, 45);

        float y = 145;

        var btnRebind = CreateButton(panel.transform, "Key Bindings", () => {
            RefreshRebindPanel();
            ShowPanel(rebindPanel);
        });
        SetAnchored(btnRebind, 0, y, 520, 45);
        y -= 65;

        // Sensitivity
        var sensRow = CreateSettingRow(panel.transform, "Look Sensitivity", y, 300);
        sensitivitySlider = CreateSlider(sensRow.transform, 1f, 30f, GameSettings.Sensitivity, (v) =>
        {
            GameSettings.Sensitivity = v;
            sensitivityValueText.text = v.ToString("F1");
            var orbit = Camera.main?.GetComponent<SimpleCameraOrbit>();
            if (orbit != null) orbit.lookSensitivity = v;
        });
        sensitivityValueText = CreateValueLabel(sensRow.transform, GameSettings.Sensitivity.ToString("F1"));
        SetAnchored(sensRow, 0, y, 620, 40);
        y -= 60;

        // Gamepad Deadzone
        var dzRow = CreateSettingRow(panel.transform, "Gamepad Deadzone", y, 300);
        deadzoneSlider = CreateSlider(dzRow.transform, 0.01f, 0.5f, GameSettings.GamepadDeadzone, (v) =>
        {
            GameSettings.GamepadDeadzone = v;
            deadzoneValueText.text = v.ToString("F2");
            InputSystem.settings.defaultDeadzoneMin = v;
        });
        deadzoneValueText = CreateValueLabel(dzRow.transform, GameSettings.GamepadDeadzone.ToString("F2"));
        SetAnchored(dzRow, 0, y, 620, 40);
        y -= 60;

        // Invert Y
        var invertRow = CreateSettingRow(panel.transform, "Invert Y-Axis", y, 300);
        invertYToggle = CreateToggle(invertRow.transform, GameSettings.InvertY, (v) =>
        {
            GameSettings.InvertY = v;
            var orbit = Camera.main?.GetComponent<SimpleCameraOrbit>();
            if (orbit != null) orbit.invertY = v;
        });
        SetAnchored(invertRow, 0, y, 620, 40);
        y -= 65;

        var btnBack = CreateButton(panel.transform, "Back", () => {
            GameSettings.Save();
            GoBack();
        });
        SetAnchored(btnBack, 0, y, 400, 45);

        return panel;
    }

    // ----- Video Options -----
    private GameObject BuildVideoOptionsPanel(Transform parent)
    {
        var panel = CreatePanel(parent, "VideoOptionsPanel", 750, 560);

        var title = CreateTitle(panel.transform, "Video Settings", 36);
        SetAnchored(title, 0, 240, 700, 45);

        float y = 180;

        // Resolution
        var resRow = CreateSettingRow(panel.transform, "Resolution", y, 300);
        resolutionDropdown = CreateDropdown(resRow.transform);
        PopulateResolutions();
        resolutionDropdown.onValueChanged.AddListener((idx) =>
        {
            if (idx >= 0 && idx < availableResolutions.Count)
            {
                var res = availableResolutions[idx];
                GameSettings.ResolutionWidth = res.width;
                GameSettings.ResolutionHeight = res.height;
                ApplyResolutionAndMode();
            }
        });
        SetAnchored(resRow, 0, y, 660, 45);
        y -= 58;

        // Window Mode
        var wmRow = CreateSettingRow(panel.transform, "Window Mode", y, 300);
        windowModeDropdown = CreateDropdown(wmRow.transform);
        windowModeDropdown.ClearOptions();
        windowModeDropdown.AddOptions(new List<string> { "Windowed", "Fullscreen Window", "Fullscreen" });
        windowModeDropdown.value = GameSettings.FullscreenMode;
        windowModeDropdown.onValueChanged.AddListener((idx) =>
        {
            GameSettings.FullscreenMode = idx;
            ApplyResolutionAndMode();
        });
        SetAnchored(wmRow, 0, y, 660, 45);
        y -= 58;

        // FOV
        var fovRow = CreateSettingRow(panel.transform, "Field of View", y, 300);
        fovSlider = CreateSlider(fovRow.transform, 40f, 120f, GameSettings.FOV, (v) =>
        {
            GameSettings.FOV = v;
            fovValueText.text = Mathf.RoundToInt(v).ToString();
            if (Camera.main != null) Camera.main.fieldOfView = v;
        });
        fovValueText = CreateValueLabel(fovRow.transform, Mathf.RoundToInt(GameSettings.FOV).ToString());
        SetAnchored(fovRow, 0, y, 660, 40);
        y -= 58;

        // Camera Shake Enable
        var shakeRow = CreateSettingRow(panel.transform, "Camera Shake", y, 300);
        shakeEnabledToggle = CreateToggle(shakeRow.transform, GameSettings.ShakeEnabled, (v) =>
        {
            GameSettings.ShakeEnabled = v;
            var orbit = Camera.main?.GetComponent<SimpleCameraOrbit>();
            if (orbit != null) orbit.enableShake = v;
        });
        SetAnchored(shakeRow, 0, y, 660, 40);
        y -= 58;

        // Shake Intensity
        var shakeIntRow = CreateSettingRow(panel.transform, "Shake Intensity", y, 300);
        shakeIntensitySlider = CreateSlider(shakeIntRow.transform, 0f, 2f, GameSettings.ShakeIntensity, (v) =>
        {
            GameSettings.ShakeIntensity = v;
            shakeIntensityValueText.text = v.ToString("F1");
            var orbit = Camera.main?.GetComponent<SimpleCameraOrbit>();
            if (orbit != null) orbit.shakeIntensityMultiplier = v;
        });
        shakeIntensityValueText = CreateValueLabel(shakeIntRow.transform, GameSettings.ShakeIntensity.ToString("F1"));
        SetAnchored(shakeIntRow, 0, y, 660, 40);
        y -= 65;

        var btnBack = CreateButton(panel.transform, "Back", () => {
            GameSettings.Save();
            GoBack();
        });
        SetAnchored(btnBack, 0, y, 400, 45);

        return panel;
    }

    // ----- Audio Options (with volume sliders) -----
    private GameObject BuildAudioOptionsPanel(Transform parent)
    {
        var panel = CreatePanel(parent, "AudioOptionsPanel", 750, 400);

        var title = CreateTitle(panel.transform, "Audio Settings", 36);
        SetAnchored(title, 0, 150, 700, 45);

        float y = 80;

        // Master Volume
        var masterRow = CreateSettingRow(panel.transform, "Master Volume", y, 300);
        masterVolumeSlider = CreateSlider(masterRow.transform, 0f, 1f, GameSettings.MasterVolume, (v) => {
            GameSettings.MasterVolume = v;
            masterVolumeValueText.text = Mathf.RoundToInt(v * 100) + "%";
            if (AudioManager.Instance != null) AudioManager.Instance.SetMasterVolume(v);
        });
        masterVolumeValueText = CreateValueLabel(masterRow.transform, Mathf.RoundToInt(GameSettings.MasterVolume * 100) + "%");
        SetAnchored(masterRow, 0, y, 660, 40);
        y -= 58;

        // SFX Volume
        var sfxRow = CreateSettingRow(panel.transform, "SFX Volume", y, 300);
        sfxVolumeSlider = CreateSlider(sfxRow.transform, 0f, 1f, GameSettings.SFXVolume, (v) => {
            GameSettings.SFXVolume = v;
            sfxVolumeValueText.text = Mathf.RoundToInt(v * 100) + "%";
            if (AudioManager.Instance != null) AudioManager.Instance.SetSFXVolume(v);
        });
        sfxVolumeValueText = CreateValueLabel(sfxRow.transform, Mathf.RoundToInt(GameSettings.SFXVolume * 100) + "%");
        SetAnchored(sfxRow, 0, y, 660, 40);
        y -= 58;

        // Music Volume
        var musicRow = CreateSettingRow(panel.transform, "Music Volume", y, 300);
        musicVolumeSlider = CreateSlider(musicRow.transform, 0f, 1f, GameSettings.MusicVolume, (v) => {
            GameSettings.MusicVolume = v;
            musicVolumeValueText.text = Mathf.RoundToInt(v * 100) + "%";
            if (MusicManager.Instance != null) MusicManager.Instance.SetVolume(v);
        });
        musicVolumeValueText = CreateValueLabel(musicRow.transform, Mathf.RoundToInt(GameSettings.MusicVolume * 100) + "%");
        SetAnchored(musicRow, 0, y, 660, 40);
        y -= 70;

        var btnBack = CreateButton(panel.transform, "Back", () => {
            GameSettings.Save();
            GoBack();
        });
        SetAnchored(btnBack, 0, y, 400, 45);

        return panel;
    }

    // ----- Rebind Panel -----
    private GameObject BuildRebindPanel(Transform parent)
    {
        var panel = CreatePanel(parent, "RebindPanel", 700, 600);

        var title = CreateTitle(panel.transform, "Key Bindings", 36);
        SetAnchored(title, 0, 260, 650, 45);

        var scrollObj = new GameObject("ScrollArea");
        scrollObj.transform.SetParent(panel.transform, false);
        var scrollRect = scrollObj.AddComponent<RectTransform>();
        scrollRect.anchorMin = new Vector2(0.5f, 0.5f);
        scrollRect.anchorMax = new Vector2(0.5f, 0.5f);
        scrollRect.sizeDelta = new Vector2(640, 420);
        scrollRect.anchoredPosition = new Vector2(0, -20);

        var contentObj = new GameObject("Content");
        contentObj.transform.SetParent(scrollObj.transform, false);
        var contentRect = contentObj.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.sizeDelta = new Vector2(0, 0);
        var vlg = contentObj.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 8;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.padding = new RectOffset(10, 10, 5, 5);
        contentObj.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = scrollObj.AddComponent<ScrollRect>();
        scroll.content = contentRect;
        scroll.vertical = true;
        scroll.horizontal = false;
        var scrollBg = scrollObj.AddComponent<Image>();
        scrollBg.color = new Color(0, 0, 0, 0.3f);
        scroll.viewport = scrollRect;

        rebindContentParent = contentObj.transform;

        float backY = -260;
        var btnBack = CreateButton(panel.transform, "Back", () => {
            GameSettings.Save();
            GoBack();
        });
        SetAnchored(btnBack, 0, backY, 360, 45);

        return panel;
    }

    // ========== Rebind Logic ==========

    private void RefreshRebindPanel()
    {
        if (playerInput == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) playerInput = player.GetComponent<PlayerInput>();
        }
        if (playerInput == null) return;

        Transform content;
        if (usingExternalUI && uiOverride != null && uiOverride.rebindContentParent != null)
            content = uiOverride.rebindContentParent;
        else
            content = rebindContentParent;

        if (content == null) return;

        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);

        var actionMap = playerInput.actions.FindActionMap("Gameplay");
        if (actionMap == null) return;

        foreach (var action in actionMap.actions)
        {
            for (int bi = 0; bi < action.bindings.Count; bi++)
            {
                var binding = action.bindings[bi];
                if (binding.isComposite) continue;

                string displayName = binding.isPartOfComposite
                    ? $"  {action.name} ({binding.name})"
                    : action.name;

                int bindingIndex = bi;
                CreateRebindRow(content, displayName,
                    InputControlPath.ToHumanReadableString(binding.effectivePath,
                        InputControlPath.HumanReadableStringOptions.OmitDevice),
                    () => StartRebind(action, bindingIndex, content));
            }
        }
    }

    private GameObject CreateRebindRow(Transform parent, string actionName, string currentKey, System.Action onRebind)
    {
        var row = new GameObject("RebindRow");
        row.transform.SetParent(parent, false);
        var rowRect = row.AddComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(0, 40);
        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 10;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = true;
        hlg.padding = new RectOffset(5, 5, 2, 2);

        var labelObj = new GameObject("Label");
        labelObj.transform.SetParent(row.transform, false);
        var labelText = labelObj.AddComponent<TextMeshProUGUI>();
        labelText.text = actionName;
        labelText.fontSize = 18;
        labelText.color = TEXT_COLOR;
        labelText.alignment = TextAlignmentOptions.MidlineLeft;
        var labelLE = labelObj.AddComponent<LayoutElement>();
        labelLE.preferredWidth = 280;
        labelLE.flexibleWidth = 1;

        var btnObj = new GameObject("RebindBtn");
        btnObj.transform.SetParent(row.transform, false);
        var btnImg = btnObj.AddComponent<Image>();
        btnImg.color = BUTTON_NORMAL;
        var btn = btnObj.AddComponent<Button>();
        var btnLE = btnObj.AddComponent<LayoutElement>();
        btnLE.preferredWidth = 200;

        var btnTextObj = new GameObject("Text");
        btnTextObj.transform.SetParent(btnObj.transform, false);
        var btnText = btnTextObj.AddComponent<TextMeshProUGUI>();
        btnText.text = currentKey;
        btnText.fontSize = 18;
        btnText.color = ACCENT;
        btnText.alignment = TextAlignmentOptions.Center;
        var btnTextRect = btnTextObj.GetComponent<RectTransform>();
        btnTextRect.anchorMin = Vector2.zero;
        btnTextRect.anchorMax = Vector2.one;
        btnTextRect.sizeDelta = Vector2.zero;

        btn.onClick.AddListener(() => onRebind?.Invoke());
        AttachUISoundToButton(btn);

        return row;
    }

    private void StartRebind(InputAction action, int bindingIndex, Transform content)
    {
        action.Disable();

        currentRebindOp = action.PerformInteractiveRebinding(bindingIndex)
            .WithControlsExcluding("Mouse")
            .WithCancelingThrough("<Keyboard>/escape")
            .OnMatchWaitForAnother(0.1f)
            .OnComplete(op =>
            {
                op.Dispose();
                currentRebindOp = null;
                action.Enable();
                RefreshRebindPanel();
            })
            .OnCancel(op =>
            {
                op.Dispose();
                currentRebindOp = null;
                action.Enable();
                RefreshRebindPanel();
            })
            .Start();
    }

    // ========== Resolution Helpers ==========

    private void PopulateResolutions()
    {
        availableResolutions.Clear();
        var allRes = Screen.resolutions;
        HashSet<string> seen = new HashSet<string>();
        int selectedIdx = 0;

        for (int i = 0; i < allRes.Length; i++)
        {
            string key = $"{allRes[i].width}x{allRes[i].height}";
            if (seen.Contains(key)) continue;
            seen.Add(key);
            availableResolutions.Add(allRes[i]);
        }

        var options = new List<string>();
        for (int i = 0; i < availableResolutions.Count; i++)
        {
            var r = availableResolutions[i];
            options.Add($"{r.width} x {r.height}");
            if (r.width == GameSettings.ResolutionWidth && r.height == GameSettings.ResolutionHeight)
                selectedIdx = i;
        }

        resolutionDropdown.ClearOptions();
        resolutionDropdown.AddOptions(options);
        resolutionDropdown.value = selectedIdx;
    }

    private void ApplyResolutionAndMode()
    {
        FullScreenMode mode;
        switch (GameSettings.FullscreenMode)
        {
            case 0: mode = FullScreenMode.Windowed; break;
            case 2: mode = FullScreenMode.ExclusiveFullScreen; break;
            default: mode = FullScreenMode.FullScreenWindow; break;
        }
        Screen.SetResolution(GameSettings.ResolutionWidth, GameSettings.ResolutionHeight, mode);
    }

    // ========== Refresh All Settings UI ==========

    private void RefreshAllSettingsUI()
    {
        if (sensitivitySlider != null)
        {
            sensitivitySlider.value = GameSettings.Sensitivity;
            if (sensitivityValueText != null)
                sensitivityValueText.text = GameSettings.Sensitivity.ToString("F1");
        }
        if (invertYToggle != null)
            invertYToggle.isOn = GameSettings.InvertY;
        if (deadzoneSlider != null)
        {
            deadzoneSlider.value = GameSettings.GamepadDeadzone;
            if (deadzoneValueText != null)
                deadzoneValueText.text = GameSettings.GamepadDeadzone.ToString("F2");
        }
        if (resolutionDropdown != null)
            PopulateResolutions();
        if (windowModeDropdown != null)
            windowModeDropdown.value = GameSettings.FullscreenMode;
        if (fpsToggle != null)
            fpsToggle.isOn = GameSettings.ShowFPS;
        if (fovSlider != null)
        {
            fovSlider.value = GameSettings.FOV;
            if (fovValueText != null)
                fovValueText.text = Mathf.RoundToInt(GameSettings.FOV).ToString();
        }
        if (shakeEnabledToggle != null)
            shakeEnabledToggle.isOn = GameSettings.ShakeEnabled;
        if (shakeIntensitySlider != null)
        {
            shakeIntensitySlider.value = GameSettings.ShakeIntensity;
            if (shakeIntensityValueText != null)
                shakeIntensityValueText.text = GameSettings.ShakeIntensity.ToString("F1");
        }
        if (stateDebugToggle != null)
            stateDebugToggle.isOn = GameSettings.ShowStateDebug;
        if (entityDetectionToggle != null)
            entityDetectionToggle.isOn = GameSettings.ShowEntityDetection;
        if (hotkeyDisplayToggle != null)
            hotkeyDisplayToggle.isOn = GameSettings.ShowHotkeyDisplay;
        if (versionDisplayToggle != null)
            versionDisplayToggle.isOn = GameSettings.ShowVersion;

        // 音量控件
        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.value = GameSettings.MasterVolume;
            if (masterVolumeValueText != null)
                masterVolumeValueText.text = Mathf.RoundToInt(GameSettings.MasterVolume * 100) + "%";
        }
        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.value = GameSettings.SFXVolume;
            if (sfxVolumeValueText != null)
                sfxVolumeValueText.text = Mathf.RoundToInt(GameSettings.SFXVolume * 100) + "%";
        }
        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.value = GameSettings.MusicVolume;
            if (musicVolumeValueText != null)
                musicVolumeValueText.text = Mathf.RoundToInt(GameSettings.MusicVolume * 100) + "%";
        }
    }

    public void RefreshSettingsUI() => RefreshAllSettingsUI();

    // ================================================================
    //  UI FACTORY HELPERS (default UI only)
    // ================================================================

    private GameObject CreatePanel(Transform parent, string name, float width, float height)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        var rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = Vector2.zero;

        var img = obj.AddComponent<Image>();
        img.color = PANEL_BG;

        var outline = obj.AddComponent<Outline>();
        outline.effectColor = new Color(0.3f, 0.5f, 0.8f, 0.4f);
        outline.effectDistance = new Vector2(2, -2);

        return obj;
    }

    private GameObject CreateTitle(Transform parent, string text, int fontSize)
    {
        var obj = new GameObject("Title");
        obj.transform.SetParent(parent, false);
        obj.AddComponent<RectTransform>();
        var tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = TEXT_COLOR;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = FontStyles.Bold;
        return obj;
    }

    private GameObject CreateButton(Transform parent, string text, UnityEngine.Events.UnityAction onClick)
    {
        var obj = new GameObject("Btn_" + text);
        obj.transform.SetParent(parent, false);
        obj.AddComponent<RectTransform>();

        var img = obj.AddComponent<Image>();
        img.color = BUTTON_NORMAL;

        var btn = obj.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor = BUTTON_NORMAL;
        colors.highlightedColor = BUTTON_HIGHLIGHT;
        colors.pressedColor = ACCENT;
        colors.selectedColor = BUTTON_HIGHLIGHT;
        btn.colors = colors;
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);
        AttachUISoundToButton(btn);

        var textObj = new GameObject("Text");
        textObj.transform.SetParent(obj.transform, false);
        var textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        var tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 24;
        tmp.color = TEXT_COLOR;
        tmp.alignment = TextAlignmentOptions.Center;

        return obj;
    }

    private GameObject CreateSettingRow(Transform parent, string label, float y, int labelWidth = 220)
    {
        var obj = new GameObject("Row_" + label);
        obj.transform.SetParent(parent, false);
        var rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);

        var labelObj = new GameObject("Label");
        labelObj.transform.SetParent(obj.transform, false);
        var labelRect = labelObj.AddComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0, 0.5f);
        labelRect.anchorMax = new Vector2(0, 0.5f);
        labelRect.pivot = new Vector2(0, 0.5f);
        labelRect.anchoredPosition = new Vector2(15, 0);
        labelRect.sizeDelta = new Vector2(labelWidth, 35);
        var tmp = labelObj.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 20;
        tmp.color = TEXT_COLOR;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;

        return obj;
    }

    private Slider CreateSlider(Transform parent, float min, float max, float value,
        UnityEngine.Events.UnityAction<float> onChange)
    {
        var obj = new GameObject("Slider");
        obj.transform.SetParent(parent, false);
        var rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(1, 0.5f);
        rect.anchorMax = new Vector2(1, 0.5f);
        rect.pivot = new Vector2(1, 0.5f);
        rect.anchoredPosition = new Vector2(-65, 0);
        rect.sizeDelta = new Vector2(220, 25);

        var bgObj = new GameObject("Background");
        bgObj.transform.SetParent(obj.transform, false);
        var bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0, 0.25f);
        bgRect.anchorMax = new Vector2(1, 0.75f);
        bgRect.sizeDelta = Vector2.zero;
        var bgImg = bgObj.AddComponent<Image>();
        bgImg.color = new Color(0.2f, 0.2f, 0.3f, 1f);

        var fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(obj.transform, false);
        var fillAreaRect = fillArea.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = new Vector2(0, 0.25f);
        fillAreaRect.anchorMax = new Vector2(1, 0.75f);
        fillAreaRect.sizeDelta = new Vector2(-20, 0);

        var fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(fillArea.transform, false);
        var fillRect = fillObj.AddComponent<RectTransform>();
        fillRect.sizeDelta = new Vector2(10, 0);
        var fillImg = fillObj.AddComponent<Image>();
        fillImg.color = ACCENT;

        var handleArea = new GameObject("Handle Slide Area");
        handleArea.transform.SetParent(obj.transform, false);
        var handleAreaRect = handleArea.AddComponent<RectTransform>();
        handleAreaRect.anchorMin = new Vector2(0, 0);
        handleAreaRect.anchorMax = new Vector2(1, 1);
        handleAreaRect.sizeDelta = new Vector2(-20, 0);

        var handleObj = new GameObject("Handle");
        handleObj.transform.SetParent(handleArea.transform, false);
        var handleRect = handleObj.AddComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(20, 0);
        var handleImg = handleObj.AddComponent<Image>();
        handleImg.color = Color.white;

        var slider = obj.AddComponent<Slider>();
        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handleImg;
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = value;
        slider.onValueChanged.AddListener(onChange);
        AttachUISoundToSlider(slider);

        return slider;
    }

    private TMP_Text CreateValueLabel(Transform parent, string text)
    {
        var obj = new GameObject("Value");
        obj.transform.SetParent(parent, false);
        var rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(1, 0.5f);
        rect.anchorMax = new Vector2(1, 0.5f);
        rect.pivot = new Vector2(1, 0.5f);
        rect.anchoredPosition = new Vector2(-10, 0);
        rect.sizeDelta = new Vector2(50, 30);
        var tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 18;
        tmp.color = ACCENT;
        tmp.alignment = TextAlignmentOptions.MidlineRight;
        return tmp;
    }

    private Toggle CreateToggle(Transform parent, bool value, UnityEngine.Events.UnityAction<bool> onChange)
    {
        var obj = new GameObject("Toggle");
        obj.transform.SetParent(parent, false);
        var rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(1, 0.5f);
        rect.anchorMax = new Vector2(1, 0.5f);
        rect.pivot = new Vector2(1, 0.5f);
        rect.anchoredPosition = new Vector2(-15, 0);
        rect.sizeDelta = new Vector2(30, 30);

        var bgObj = new GameObject("Background");
        bgObj.transform.SetParent(obj.transform, false);
        var bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        var bgImg = bgObj.AddComponent<Image>();
        bgImg.color = new Color(0.2f, 0.2f, 0.3f, 1f);

        var checkObj = new GameObject("Checkmark");
        checkObj.transform.SetParent(bgObj.transform, false);
        var checkRect = checkObj.AddComponent<RectTransform>();
        checkRect.anchorMin = new Vector2(0.15f, 0.15f);
        checkRect.anchorMax = new Vector2(0.85f, 0.85f);
        checkRect.sizeDelta = Vector2.zero;
        var checkImg = checkObj.AddComponent<Image>();
        checkImg.color = ACCENT;

        var toggle = obj.AddComponent<Toggle>();
        toggle.targetGraphic = bgImg;
        toggle.graphic = checkImg;
        toggle.isOn = value;
        toggle.onValueChanged.AddListener(onChange);
        AttachUISoundToToggle(toggle);

        return toggle;
    }

    private TMP_Dropdown CreateDropdown(Transform parent)
    {
        var obj = new GameObject("Dropdown");
        obj.transform.SetParent(parent, false);
        var rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(1, 0.5f);
        rect.anchorMax = new Vector2(1, 0.5f);
        rect.pivot = new Vector2(1, 0.5f);
        rect.anchoredPosition = new Vector2(-10, 0);
        rect.sizeDelta = new Vector2(260, 35);

        var img = obj.AddComponent<Image>();
        img.color = BUTTON_NORMAL;

        var labelObj = new GameObject("Label");
        labelObj.transform.SetParent(obj.transform, false);
        var labelRect = labelObj.AddComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0, 0);
        labelRect.anchorMax = new Vector2(1, 1);
        labelRect.offsetMin = new Vector2(10, 2);
        labelRect.offsetMax = new Vector2(-25, -2);
        var labelTMP = labelObj.AddComponent<TextMeshProUGUI>();
        labelTMP.fontSize = 18;
        labelTMP.color = TEXT_COLOR;
        labelTMP.alignment = TextAlignmentOptions.MidlineLeft;

        var arrowObj = new GameObject("Arrow");
        arrowObj.transform.SetParent(obj.transform, false);
        var arrowRect = arrowObj.AddComponent<RectTransform>();
        arrowRect.anchorMin = new Vector2(1, 0);
        arrowRect.anchorMax = new Vector2(1, 1);
        arrowRect.sizeDelta = new Vector2(25, 0);
        arrowRect.anchoredPosition = new Vector2(-12.5f, 0);
        var arrowTMP = arrowObj.AddComponent<TextMeshProUGUI>();
        arrowTMP.text = "\u25BC";
        arrowTMP.fontSize = 14;
        arrowTMP.color = TEXT_COLOR;
        arrowTMP.alignment = TextAlignmentOptions.Center;

        var templateObj = new GameObject("Template");
        templateObj.transform.SetParent(obj.transform, false);
        var templateRect = templateObj.AddComponent<RectTransform>();
        templateRect.anchorMin = new Vector2(0, 0);
        templateRect.anchorMax = new Vector2(1, 0);
        templateRect.pivot = new Vector2(0.5f, 1);
        templateRect.sizeDelta = new Vector2(0, 180);
        var templateImg = templateObj.AddComponent<Image>();
        templateImg.color = new Color(0.1f, 0.1f, 0.2f, 0.98f);
        var templateScroll = templateObj.AddComponent<ScrollRect>();

        var viewportObj = new GameObject("Viewport");
        viewportObj.transform.SetParent(templateObj.transform, false);
        var viewportRect = viewportObj.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.sizeDelta = Vector2.zero;
        var viewportImg = viewportObj.AddComponent<Image>();
        viewportImg.color = Color.white;
        var mask = viewportObj.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        var contentObj = new GameObject("Content");
        contentObj.transform.SetParent(viewportObj.transform, false);
        var contentRect = contentObj.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.sizeDelta = new Vector2(0, 28);

        var itemObj = new GameObject("Item");
        itemObj.transform.SetParent(contentObj.transform, false);
        var itemRect = itemObj.AddComponent<RectTransform>();
        itemRect.anchorMin = new Vector2(0, 0.5f);
        itemRect.anchorMax = new Vector2(1, 0.5f);
        itemRect.sizeDelta = new Vector2(0, 28);
        var itemToggle = itemObj.AddComponent<Toggle>();

        var itemBgObj = new GameObject("Item Background");
        itemBgObj.transform.SetParent(itemObj.transform, false);
        var itemBgRect = itemBgObj.AddComponent<RectTransform>();
        itemBgRect.anchorMin = Vector2.zero;
        itemBgRect.anchorMax = Vector2.one;
        itemBgRect.sizeDelta = Vector2.zero;
        var itemBgImg = itemBgObj.AddComponent<Image>();
        itemBgImg.color = BUTTON_HIGHLIGHT;

        var itemCheckObj = new GameObject("Item Checkmark");
        itemCheckObj.transform.SetParent(itemObj.transform, false);
        var itemCheckRect = itemCheckObj.AddComponent<RectTransform>();
        itemCheckRect.anchorMin = new Vector2(0, 0.5f);
        itemCheckRect.anchorMax = new Vector2(0, 0.5f);
        itemCheckRect.sizeDelta = new Vector2(20, 20);
        itemCheckRect.anchoredPosition = new Vector2(15, 0);
        var itemCheckImg = itemCheckObj.AddComponent<Image>();
        itemCheckImg.color = ACCENT;

        var itemLabelObj = new GameObject("Item Label");
        itemLabelObj.transform.SetParent(itemObj.transform, false);
        var itemLabelRect = itemLabelObj.AddComponent<RectTransform>();
        itemLabelRect.anchorMin = Vector2.zero;
        itemLabelRect.anchorMax = Vector2.one;
        itemLabelRect.offsetMin = new Vector2(30, 0);
        itemLabelRect.offsetMax = new Vector2(-10, 0);
        var itemLabelTMP = itemLabelObj.AddComponent<TextMeshProUGUI>();
        itemLabelTMP.fontSize = 18;
        itemLabelTMP.color = TEXT_COLOR;
        itemLabelTMP.alignment = TextAlignmentOptions.MidlineLeft;

        itemToggle.targetGraphic = itemBgImg;
        itemToggle.graphic = itemCheckImg;

        templateScroll.content = contentRect;
        templateScroll.viewport = viewportRect;

        templateObj.SetActive(false);

        var dropdown = obj.AddComponent<TMP_Dropdown>();
        dropdown.template = templateRect;
        dropdown.captionText = labelTMP;
        dropdown.itemText = itemLabelTMP;
        dropdown.alphaFadeSpeed = 0.15f;

        AttachUISoundToDropdown(dropdown);

        return dropdown;
    }

    private void SetAnchored(GameObject obj, float x, float y, float w, float h)
    {
        var rect = obj.GetComponent<RectTransform>();
        if (rect == null) return;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(w, h);
    }
}