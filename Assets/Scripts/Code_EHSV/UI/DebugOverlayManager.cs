using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Manages debug overlay displays and debug shortcuts (F1-F4).
/// Overlays: FPS (top-right, managed by FPSDisplay), State Debug (top-center),
/// Entity Detection (top-left), Hotkey Display (top-right below FPS), Version (bottom-left, default on).
/// Debug shortcuts: F1=toggle debug mode, F2=free camera (debug only),
/// F3=disable rendering (debug only), F4=quick exit.
/// </summary>
public class DebugOverlayManager : MonoBehaviour
{
    [Header("Version")]
    [Tooltip("Version string displayed in the bottom-left corner")]
    public string versionString = "v0.1.0-dev";

    private Canvas overlayCanvas;
    private TMP_Text stateDebugText;
    private TMP_Text entityDetectionText;
    private TMP_Text hotkeyDisplayText;
    private TMP_Text versionText;

    private GameObject stateDebugObj;
    private GameObject entityDetectionObj;
    private GameObject hotkeyDisplayObj;
    private GameObject versionObj;

    // Debug mode: active when any debug overlay is on
    private bool IsDebugModeActive =>
        GameSettings.ShowStateDebug || GameSettings.ShowEntityDetection || GameSettings.ShowHotkeyDisplay;
    private bool freeCameraActive = false;
    private bool lightingDisabled = false;

    // Input actions
    private InputAction debugToggleAction;
    private InputAction freeCameraAction;
    private InputAction disableRenderingAction;
    private InputAction quickExitAction;

    // Free camera state
    private Vector3 savedCameraPos;
    private Quaternion savedCameraRot;
    private float freeCamSpeed = 10f;
    private float freeCamYaw;
    private float freeCamPitch;

    // Saved state for free camera gameplay freeze
    private float savedTimeScale;
    private bool savedPlayerCanMove;
    private bool savedPlayerFreezeGravity;

    // Saved state for lighting toggle
    private Light[] savedLights;
    private bool[] savedLightStates;
    private Color savedAmbientColor;
    private float savedAmbientIntensity;

    void Awake()
    {
        BuildOverlayUI();
        SetupInputActions();
        RefreshVisibility();
    }

    void OnDestroy()
    {
        if (debugToggleAction != null)
        {
            debugToggleAction.performed -= OnDebugToggle;
            debugToggleAction.Disable();
        }
        if (freeCameraAction != null)
        {
            freeCameraAction.performed -= OnFreeCamera;
            freeCameraAction.Disable();
        }
        if (disableRenderingAction != null)
        {
            disableRenderingAction.performed -= OnDisableRendering;
            disableRenderingAction.Disable();
        }
        if (quickExitAction != null)
        {
            quickExitAction.performed -= OnQuickExit;
            quickExitAction.Disable();
        }
    }

    private void SetupInputActions()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        PlayerInput playerInput = null;
        if (player != null)
            playerInput = player.GetComponent<PlayerInput>();

        if (playerInput != null && playerInput.actions != null)
        {
            debugToggleAction = playerInput.actions.FindAction("Gameplay/DebugToggle");
            freeCameraAction = playerInput.actions.FindAction("Gameplay/DebugFreeCamera");
            disableRenderingAction = playerInput.actions.FindAction("Gameplay/DebugDisableRendering");
            quickExitAction = playerInput.actions.FindAction("Gameplay/DebugQuickExit");
        }

        // Fallback standalone actions if not found in the asset
        if (debugToggleAction == null)
        {
            var map = new InputActionMap("DebugMap");
            debugToggleAction = map.AddAction("DebugToggle", binding: "<Keyboard>/f1");
            freeCameraAction = map.AddAction("DebugFreeCamera", binding: "<Keyboard>/f2");
            disableRenderingAction = map.AddAction("DebugDisableRendering", binding: "<Keyboard>/f3");
            quickExitAction = map.AddAction("DebugQuickExit", binding: "<Keyboard>/f4");
            map.Enable();
        }

        debugToggleAction.performed += OnDebugToggle;
        freeCameraAction.performed += OnFreeCamera;
        disableRenderingAction.performed += OnDisableRendering;
        quickExitAction.performed += OnQuickExit;

        debugToggleAction.Enable();
        freeCameraAction.Enable();
        disableRenderingAction.Enable();
        quickExitAction.Enable();
    }

    private void OnDebugToggle(InputAction.CallbackContext ctx)
    {
        bool anyOn = GameSettings.ShowStateDebug || GameSettings.ShowEntityDetection || GameSettings.ShowHotkeyDisplay;

        if (anyOn)
        {
            // Any debug overlay is on -> turn all off
            GameSettings.ShowStateDebug = false;
            GameSettings.ShowEntityDetection = false;
            GameSettings.ShowHotkeyDisplay = false;

            // Also disable sub-features
            if (freeCameraActive) ToggleFreeCamera();
            if (lightingDisabled) ToggleLighting();

            Debug.Log("[Debug] All debug overlays OFF");
        }
        else
        {
            // All off -> turn all on
            GameSettings.ShowStateDebug = true;
            GameSettings.ShowEntityDetection = true;
            GameSettings.ShowHotkeyDisplay = true;

            Debug.Log("[Debug] All debug overlays ON");
        }

        GameSettings.Save();
        RefreshVisibility();

        // Sync PauseManager UI if menu is open
        if (PauseManager.Instance != null && PauseManager.Instance.IsPaused)
            PauseManager.Instance.RefreshSettingsUI();
    }

    private void OnFreeCamera(InputAction.CallbackContext ctx)
    {
        if (!IsDebugModeActive) return;
        ToggleFreeCamera();
    }

    private void OnDisableRendering(InputAction.CallbackContext ctx)
    {
        if (!IsDebugModeActive) return;
        ToggleLighting();
    }

    private void OnQuickExit(InputAction.CallbackContext ctx)
    {
        Debug.Log("[Debug] Quick exit triggered");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void ToggleFreeCamera()
    {
        freeCameraActive = !freeCameraActive;
        var cam = Camera.main;
        var orbit = cam?.GetComponent<SimpleCameraOrbit>();
        var player = GameObject.FindGameObjectWithTag("Player");
        var pc = player?.GetComponent<PlayerController>();

        if (freeCameraActive)
        {
            // Save camera state
            savedCameraPos = cam.transform.position;
            savedCameraRot = cam.transform.rotation;
            freeCamYaw = cam.transform.eulerAngles.y;
            freeCamPitch = cam.transform.eulerAngles.x;
            if (orbit != null) orbit.enabled = false;

            // Freeze gameplay: pause time, disable player & clone operations
            savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            if (pc != null)
            {
                savedPlayerCanMove = pc.canMove;
                savedPlayerFreezeGravity = pc.freezeGravity;
                pc.canMove = false;
                pc.freezeGravity = true;
            }

            // Disable clone & trace managers to prevent any actions
            var cloneMgr = FindFirstObjectByType<CloneManager>();
            if (cloneMgr != null) cloneMgr.enabled = false;
            var traceMgr = FindFirstObjectByType<TraceCloneManager>();
            if (traceMgr != null) traceMgr.enabled = false;

            // Disable player input to block all gameplay actions
            var playerInput = player?.GetComponent<PlayerInput>();
            if (playerInput != null) playerInput.enabled = false;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            Debug.Log("[Debug] Free camera ON - gameplay frozen");
        }
        else
        {
            // Restore camera
            cam.transform.position = savedCameraPos;
            cam.transform.rotation = savedCameraRot;
            if (orbit != null) orbit.enabled = true;

            // Restore gameplay
            Time.timeScale = savedTimeScale;
            if (pc != null)
            {
                pc.canMove = savedPlayerCanMove;
                pc.freezeGravity = savedPlayerFreezeGravity;
            }

            // Re-enable clone & trace managers
            var cloneMgr = FindFirstObjectByType<CloneManager>();
            if (cloneMgr != null) cloneMgr.enabled = true;
            var traceMgr = FindFirstObjectByType<TraceCloneManager>();
            if (traceMgr != null) traceMgr.enabled = true;

            // Re-enable player input
            var playerInput = player?.GetComponent<PlayerInput>();
            if (playerInput != null) playerInput.enabled = true;

            Debug.Log("[Debug] Free camera OFF - gameplay resumed");
        }
    }

    private void ToggleLighting()
    {
        lightingDisabled = !lightingDisabled;

        if (lightingDisabled)
        {
            // Save and disable all scene lights
            savedLights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            savedLightStates = new bool[savedLights.Length];
            for (int i = 0; i < savedLights.Length; i++)
            {
                savedLightStates[i] = savedLights[i].enabled;
                savedLights[i].enabled = false;
            }

            // Set bright flat ambient so everything is uniformly lit
            savedAmbientColor = RenderSettings.ambientLight;
            savedAmbientIntensity = RenderSettings.ambientIntensity;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Color.white;
            RenderSettings.ambientIntensity = 1.5f;

            Debug.Log("[Debug] Lighting disabled - flat ambient ON");
        }
        else
        {
            // Restore all lights
            if (savedLights != null)
            {
                for (int i = 0; i < savedLights.Length; i++)
                {
                    if (savedLights[i] != null)
                        savedLights[i].enabled = savedLightStates[i];
                }
            }

            // Restore ambient
            RenderSettings.ambientLight = savedAmbientColor;
            RenderSettings.ambientIntensity = savedAmbientIntensity;

            Debug.Log("[Debug] Lighting restored");
        }
    }

    void Update()
    {
        UpdateStateDebug();
        UpdateEntityDetection();
        UpdateHotkeyDisplay();

        // Free camera movement
        if (freeCameraActive && Camera.main != null)
        {
            var cam = Camera.main;
            var mouse = Mouse.current;
            var kb = Keyboard.current;

            if (mouse != null)
            {
                Vector2 delta = mouse.delta.ReadValue() * 0.15f;
                freeCamYaw += delta.x;
                freeCamPitch -= delta.y;
                freeCamPitch = Mathf.Clamp(freeCamPitch, -89f, 89f);
                cam.transform.rotation = Quaternion.Euler(freeCamPitch, freeCamYaw, 0);
            }

            if (kb != null)
            {
                float speed = freeCamSpeed * Time.unscaledDeltaTime;
                if (kb.leftShiftKey.isPressed) speed *= 3f;
                Vector3 move = Vector3.zero;
                if (kb.wKey.isPressed) move += cam.transform.forward;
                if (kb.sKey.isPressed) move -= cam.transform.forward;
                if (kb.aKey.isPressed) move -= cam.transform.right;
                if (kb.dKey.isPressed) move += cam.transform.right;
                if (kb.eKey.isPressed) move += Vector3.up;
                if (kb.qKey.isPressed) move -= Vector3.up;
                cam.transform.position += move.normalized * speed;
            }
        }
    }

    private void UpdateStateDebug()
    {
        if (stateDebugText == null || !stateDebugObj.activeSelf) return;

        string state = "Normal";
        if (PauseManager.Instance != null && PauseManager.Instance.IsPaused)
            state = "PAUSED";
        else if (TimeStopManager.Instance != null && TimeStopManager.Instance.IsTimeStopped)
            state = "TimeStop";

        var clone = FindFirstObjectByType<CloneManager>();
        if (clone != null && clone.IsTimeStopped())
            state = "VisionClone";

        var trace = FindFirstObjectByType<TraceCloneManager>();
        if (trace != null && trace.IsPhantomActive())
            state = "TracePhantom";
        else if (trace != null && trace.IsTimeStopped())
            state = "TraceClone";

        string freeStr = freeCameraActive ? " [FreeCam]" : "";
        string renderStr = lightingDisabled ? " [NoLight]" : "";
        string debugLine = IsDebugModeActive ? "\nDebug Mode" : "";

        stateDebugText.text = $"State: {state}{freeStr}{renderStr}\n" +
            $"TimeScale: {Time.timeScale:F2}{debugLine}";
    }

    private void UpdateEntityDetection()
    {
        if (entityDetectionText == null || !entityDetectionObj.activeSelf) return;

        int cloneCount = GameObject.FindGameObjectsWithTag("VisionClone")?.Length ?? 0;
        int solidCount = GameObject.FindGameObjectsWithTag("SolidClone")?.Length ?? 0;
        int traceCount = GameObject.FindGameObjectsWithTag("TraceClone")?.Length ?? 0;
        int phantomCount = GameObject.FindGameObjectsWithTag("TracePhantom")?.Length ?? 0;

        entityDetectionText.text = $"Entities:\n" +
            $"  Vision Clones: {cloneCount}\n" +
            $"  Solid Clones: {solidCount}\n" +
            $"  Trace Clones: {traceCount}\n" +
            $"  Phantoms: {phantomCount}";
    }

    private void UpdateHotkeyDisplay()
    {
        if (hotkeyDisplayText == null || !hotkeyDisplayObj.activeSelf) return;

        string text = "Hotkeys:\n";
        text += "  ESC - Pause / Exit Clone\n";
        text += "  Q - Vision Clone\n";
        text += "  E - Trace Phantom\n";
        text += "  Tab - Switch Carrier\n";
        text += "  F1 - Debug Mode\n";
        if (IsDebugModeActive)
        {
            text += "  F2 - Free Camera\n";
            text += "  F3 - Toggle Lighting\n";
        }
        text += "  F4 - Quick Exit";

        hotkeyDisplayText.text = text;
    }

    private void BuildOverlayUI()
    {
        var canvasObj = new GameObject("DebugOverlayCanvas");
        canvasObj.transform.SetParent(transform);
        overlayCanvas = canvasObj.AddComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = 190;
        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        // State Debug - top center
        stateDebugObj = CreateOverlayText("StateDebug", TextAlignmentOptions.Top,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -10), new Vector2(500, 60));
        stateDebugText = stateDebugObj.GetComponentInChildren<TMP_Text>();

        // Entity Detection - top left
        entityDetectionObj = CreateOverlayText("EntityDetection", TextAlignmentOptions.TopLeft,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10, -10), new Vector2(300, 120));
        entityDetectionText = entityDetectionObj.GetComponentInChildren<TMP_Text>();

        // Hotkey Display - top right (below FPS area)
        hotkeyDisplayObj = CreateOverlayText("HotkeyDisplay", TextAlignmentOptions.TopRight,
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-10, -50), new Vector2(300, 200));
        hotkeyDisplayText = hotkeyDisplayObj.GetComponentInChildren<TMP_Text>();

        // Version Display - bottom left
        versionObj = CreateOverlayText("VersionDisplay", TextAlignmentOptions.BottomLeft,
            new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(10, 10), new Vector2(300, 30));
        versionText = versionObj.GetComponentInChildren<TMP_Text>();
        versionText.text = versionString;
        versionText.fontSize = 16;
        versionText.color = new Color(0.7f, 0.7f, 0.7f, 0.6f);
    }

    private GameObject CreateOverlayText(string name, TextAlignmentOptions alignment,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 offset, Vector2 size)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(overlayCanvas.transform, false);
        var rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = anchorMin;
        rect.anchoredPosition = offset;
        rect.sizeDelta = size;

        var text = obj.AddComponent<TextMeshProUGUI>();
        text.fontSize = 18;
        text.color = new Color(0.9f, 0.9f, 0.9f, 0.85f);
        text.alignment = alignment;
        text.enableWordWrapping = true;
        text.raycastTarget = false;

        return obj;
    }

    public void RefreshVisibility()
    {
        if (stateDebugObj != null) stateDebugObj.SetActive(GameSettings.ShowStateDebug);
        if (entityDetectionObj != null) entityDetectionObj.SetActive(GameSettings.ShowEntityDetection);
        if (hotkeyDisplayObj != null) hotkeyDisplayObj.SetActive(GameSettings.ShowHotkeyDisplay);
        if (versionObj != null) versionObj.SetActive(GameSettings.ShowVersion);
    }
}
