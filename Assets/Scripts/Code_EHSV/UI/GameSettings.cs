using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Centralized settings data with PlayerPrefs persistence.
/// All settings connect to real game systems when applied.
/// </summary>
public static class GameSettings
{
    // ========== Keys ==========
    private const string KEY_SENSITIVITY = "Settings_Sensitivity";
    private const string KEY_INVERT_Y = "Settings_InvertY";
    private const string KEY_GAMEPAD_DEADZONE = "Settings_GamepadDeadzone";
    private const string KEY_RESOLUTION_W = "Settings_ResWidth";
    private const string KEY_RESOLUTION_H = "Settings_ResHeight";
    private const string KEY_FULLSCREEN_MODE = "Settings_FullscreenMode";
    private const string KEY_SHOW_FPS = "Settings_ShowFPS";
    private const string KEY_FOV = "Settings_FOV";
    private const string KEY_SHAKE_ENABLED = "Settings_ShakeEnabled";
    private const string KEY_SHAKE_INTENSITY = "Settings_ShakeIntensity";
    private const string KEY_SHOW_STATE_DEBUG = "Settings_ShowStateDebug";
    private const string KEY_SHOW_ENTITY_DETECTION = "Settings_ShowEntityDetection";
    private const string KEY_SHOW_HOTKEY_DISPLAY = "Settings_ShowHotkeyDisplay";
    private const string KEY_SHOW_VERSION = "Settings_ShowVersion";

    // ========== Defaults ==========
    public const float DEFAULT_SENSITIVITY = 8f;
    public const bool DEFAULT_INVERT_Y = false;
    public const float DEFAULT_GAMEPAD_DEADZONE = 0.15f;
    public const int DEFAULT_FULLSCREEN_MODE = 1; // 0=Windowed, 1=FullscreenWindow, 2=Fullscreen
    public const bool DEFAULT_SHOW_FPS = false;
    public const float DEFAULT_FOV = 60f;
    public const bool DEFAULT_SHAKE_ENABLED = true;
    public const float DEFAULT_SHAKE_INTENSITY = 1f;
    public const bool DEFAULT_SHOW_STATE_DEBUG = false;
    public const bool DEFAULT_SHOW_ENTITY_DETECTION = false;
    public const bool DEFAULT_SHOW_HOTKEY_DISPLAY = false;
    public const bool DEFAULT_SHOW_VERSION = true;

    // ========== Current Values ==========
    public static float Sensitivity { get; set; }
    public static bool InvertY { get; set; }
    public static float GamepadDeadzone { get; set; }
    public static int ResolutionWidth { get; set; }
    public static int ResolutionHeight { get; set; }
    public static int FullscreenMode { get; set; }
    public static bool ShowFPS { get; set; }
    public static float FOV { get; set; }
    public static bool ShakeEnabled { get; set; }
    public static float ShakeIntensity { get; set; }
    public static bool ShowStateDebug { get; set; }
    public static bool ShowEntityDetection { get; set; }
    public static bool ShowHotkeyDisplay { get; set; }
    public static bool ShowVersion { get; set; }

    static GameSettings()
    {
        Load();
    }

    public static void Load()
    {
        Sensitivity = PlayerPrefs.GetFloat(KEY_SENSITIVITY, DEFAULT_SENSITIVITY);
        InvertY = PlayerPrefs.GetInt(KEY_INVERT_Y, DEFAULT_INVERT_Y ? 1 : 0) == 1;
        GamepadDeadzone = PlayerPrefs.GetFloat(KEY_GAMEPAD_DEADZONE, DEFAULT_GAMEPAD_DEADZONE);
        ResolutionWidth = PlayerPrefs.GetInt(KEY_RESOLUTION_W, Screen.currentResolution.width);
        ResolutionHeight = PlayerPrefs.GetInt(KEY_RESOLUTION_H, Screen.currentResolution.height);
        FullscreenMode = PlayerPrefs.GetInt(KEY_FULLSCREEN_MODE, DEFAULT_FULLSCREEN_MODE);
        ShowFPS = PlayerPrefs.GetInt(KEY_SHOW_FPS, DEFAULT_SHOW_FPS ? 1 : 0) == 1;
        FOV = PlayerPrefs.GetFloat(KEY_FOV, DEFAULT_FOV);
        ShakeEnabled = PlayerPrefs.GetInt(KEY_SHAKE_ENABLED, DEFAULT_SHAKE_ENABLED ? 1 : 0) == 1;
        ShakeIntensity = PlayerPrefs.GetFloat(KEY_SHAKE_INTENSITY, DEFAULT_SHAKE_INTENSITY);
        ShowStateDebug = PlayerPrefs.GetInt(KEY_SHOW_STATE_DEBUG, DEFAULT_SHOW_STATE_DEBUG ? 1 : 0) == 1;
        ShowEntityDetection = PlayerPrefs.GetInt(KEY_SHOW_ENTITY_DETECTION, DEFAULT_SHOW_ENTITY_DETECTION ? 1 : 0) == 1;
        ShowHotkeyDisplay = PlayerPrefs.GetInt(KEY_SHOW_HOTKEY_DISPLAY, DEFAULT_SHOW_HOTKEY_DISPLAY ? 1 : 0) == 1;
        ShowVersion = PlayerPrefs.GetInt(KEY_SHOW_VERSION, DEFAULT_SHOW_VERSION ? 1 : 0) == 1;
    }

    public static void Save()
    {
        PlayerPrefs.SetFloat(KEY_SENSITIVITY, Sensitivity);
        PlayerPrefs.SetInt(KEY_INVERT_Y, InvertY ? 1 : 0);
        PlayerPrefs.SetFloat(KEY_GAMEPAD_DEADZONE, GamepadDeadzone);
        PlayerPrefs.SetInt(KEY_RESOLUTION_W, ResolutionWidth);
        PlayerPrefs.SetInt(KEY_RESOLUTION_H, ResolutionHeight);
        PlayerPrefs.SetInt(KEY_FULLSCREEN_MODE, FullscreenMode);
        PlayerPrefs.SetInt(KEY_SHOW_FPS, ShowFPS ? 1 : 0);
        PlayerPrefs.SetFloat(KEY_FOV, FOV);
        PlayerPrefs.SetInt(KEY_SHAKE_ENABLED, ShakeEnabled ? 1 : 0);
        PlayerPrefs.SetFloat(KEY_SHAKE_INTENSITY, ShakeIntensity);
        PlayerPrefs.SetInt(KEY_SHOW_STATE_DEBUG, ShowStateDebug ? 1 : 0);
        PlayerPrefs.SetInt(KEY_SHOW_ENTITY_DETECTION, ShowEntityDetection ? 1 : 0);
        PlayerPrefs.SetInt(KEY_SHOW_HOTKEY_DISPLAY, ShowHotkeyDisplay ? 1 : 0);
        PlayerPrefs.SetInt(KEY_SHOW_VERSION, ShowVersion ? 1 : 0);
        PlayerPrefs.Save();
    }

    public static void RestoreDefaults()
    {
        Sensitivity = DEFAULT_SENSITIVITY;
        InvertY = DEFAULT_INVERT_Y;
        GamepadDeadzone = DEFAULT_GAMEPAD_DEADZONE;
        ResolutionWidth = Screen.currentResolution.width;
        ResolutionHeight = Screen.currentResolution.height;
        FullscreenMode = DEFAULT_FULLSCREEN_MODE;
        ShowFPS = DEFAULT_SHOW_FPS;
        FOV = DEFAULT_FOV;
        ShakeEnabled = DEFAULT_SHAKE_ENABLED;
        ShakeIntensity = DEFAULT_SHAKE_INTENSITY;
        ShowStateDebug = DEFAULT_SHOW_STATE_DEBUG;
        ShowEntityDetection = DEFAULT_SHOW_ENTITY_DETECTION;
        ShowHotkeyDisplay = DEFAULT_SHOW_HOTKEY_DISPLAY;
        ShowVersion = DEFAULT_SHOW_VERSION;

        // Reset all key bindings to defaults
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            var playerInput = player.GetComponent<PlayerInput>();
            if (playerInput != null && playerInput.actions != null)
            {
                foreach (var map in playerInput.actions.actionMaps)
                {
                    map.RemoveAllBindingOverrides();
                }
            }
        }

        Save();
    }

    /// <summary>
    /// Apply all settings to the actual game systems.
    /// </summary>
    public static void ApplyAll()
    {
        // Camera settings
        var cam = Camera.main;
        if (cam != null)
        {
            cam.fieldOfView = FOV;
            var orbit = cam.GetComponent<SimpleCameraOrbit>();
            if (orbit != null)
            {
                orbit.lookSensitivity = Sensitivity;
                orbit.invertY = InvertY;
                orbit.enableShake = ShakeEnabled;
                orbit.shakeIntensityMultiplier = ShakeIntensity;
            }
        }

        // Resolution & fullscreen
        FullScreenMode mode;
        switch (FullscreenMode)
        {
            case 0: mode = UnityEngine.FullScreenMode.Windowed; break;
            case 2: mode = UnityEngine.FullScreenMode.ExclusiveFullScreen; break;
            default: mode = UnityEngine.FullScreenMode.FullScreenWindow; break;
        }
        Screen.SetResolution(ResolutionWidth, ResolutionHeight, mode);

        // Gamepad deadzone
        UnityEngine.InputSystem.InputSystem.settings.defaultDeadzoneMin = GamepadDeadzone;

        // FPS display
        var fpsDisplay = Object.FindFirstObjectByType<FPSDisplay>();
        if (fpsDisplay != null)
            fpsDisplay.SetVisible(ShowFPS);

        // Debug overlays
        var debugOverlay = Object.FindFirstObjectByType<DebugOverlayManager>();
        if (debugOverlay != null)
            debugOverlay.RefreshVisibility();
    }
}
