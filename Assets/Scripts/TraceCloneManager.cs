using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class TraceCloneManager : MonoBehaviour
{
    [Header("References")]
    public GameObject playerPrefab;
    public Material phantomMaterial;
    public Material traceCloneMaterial;

    [Header("Settings")]
    public float recordFixedDeltaTime = 0.02f;

    // 状态
    private bool isTimeStopped = false;
    private bool isPhantomActive = false;
    private GameObject currentPhantom;
    private GameObject currentTraceClone;
    private PlayerController phantomController;
    private PlayerController playerController;
    private SimpleCameraOrbit cameraOrbit;
    private GameObject player;

    // 保存摄像机原始状态
    private Transform originalCameraTarget;
    private Vector3 originalHeadOffset;
    private bool originalCameraControlsEnabled;

    // 当前录制帧
    private List<RecordedFrame> recordedFrames = new List<RecordedFrame>();
    private float recordStartTime;
    private float lastRecordTime;
    private bool isRecording = false;

    // 已保存的操作序列（用于从视界分身退出时停时生成循迹分身）
    private List<RecordedFrame> savedFrames = new List<RecordedFrame>();
    private bool hasSavedSequence = false;

    private InputAction eAction;
    private CloneManager cloneManager;

    [System.Serializable]
    public struct RecordedFrame
    {
        public float time;
        public Vector2 moveInput;
        public bool jump;
        public bool sprint;
        public float cameraYaw;
    }

    private void Awake()
    {
        player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) Debug.LogError("[TraceClone] Player not found");
        playerController = player.GetComponent<PlayerController>();
        cameraOrbit = Camera.main.GetComponent<SimpleCameraOrbit>();
        cloneManager = GetComponent<CloneManager>();

        var inputActions = new InputActionMap();
        eAction = inputActions.AddAction("E", binding: "<Keyboard>/e");
        eAction.performed += OnEPerformed;
        inputActions.Enable();
    }

    private void Update()
    {
        // 持续录制
        if (isTimeStopped && isPhantomActive && isRecording)
        {
            if (Time.unscaledTime - lastRecordTime >= recordFixedDeltaTime)
            {
                RecordCurrentFrame();
                lastRecordTime = Time.unscaledTime;
            }
        }
    }

    private void OnEPerformed(InputAction.CallbackContext ctx)
    {
        if (!isTimeStopped)
            ActivateTracePhantom();
        else if (isPhantomActive)
            ExitTracePhantomAndSpawnClone();
    }

    /// <summary>
    /// 由CloneManager在循迹之影状态按Q时调用，切换到视界分身
    /// </summary>
    public void HandleQDuringPhantom()
    {
        if (isTimeStopped && isPhantomActive)
            SwitchToVisionClone();
    }

    private void ActivateTracePhantom()
    {
        if (isPhantomActive) return;

        // 进入循迹之影状态将覆盖先前的操作序列
        hasSavedSequence = false;
        savedFrames.Clear();

        // 如果视界分身处于时停状态，强制退出（不生成实体）
        if (cloneManager != null && cloneManager.IsTimeStopped())
        {
            cloneManager.ForceExitTimeStop(false);
        }

        // 不销毁已有循迹分身——它会在新的循迹分身被召唤时才被替换

        Debug.Log(">>> [TraceClone] Enter trace phantom state (time stop)");

        // 保存摄像机原始状态
        originalCameraTarget = cameraOrbit.target;
        originalHeadOffset = cameraOrbit.headOffset;
        originalCameraControlsEnabled = cameraOrbit.controlsEnabled;

        // 切换至自由第三人称模式
        cameraOrbit.SetFreeLookMode(true, 6f);

        // 冻结本体
        playerController.canMove = false;
        playerController.freezeGravity = true;

        // 在本体位置生成循迹之影
        Vector3 spawnPos = player.transform.position;
        Quaternion spawnRot = player.transform.rotation;
        currentPhantom = Instantiate(playerPrefab, spawnPos, spawnRot);
        currentPhantom.name = "TracePhantom";
        currentPhantom.tag = "TracePhantom";
        ReplaceMaterials(currentPhantom, phantomMaterial);

        phantomController = currentPhantom.GetComponent<PlayerController>();
        phantomController.faceMovementDirection = false;
        phantomController.canMove = true;
        phantomController.freezeGravity = false;
        phantomController.useExternalInput = false;
        phantomController.enabled = true;

        var phantomCam = currentPhantom.GetComponentInChildren<Camera>();
        if (phantomCam != null) phantomCam.enabled = false;

        IgnoreCollisionBetween(player, currentPhantom, true);

        // 如果场上存在循迹分身，也忽略与影子的碰撞
        if (currentTraceClone != null)
            IgnoreCollisionBetween(currentTraceClone, currentPhantom, true);

        // 摄像机跟随影子
        cameraOrbit.target = currentPhantom.transform;

        // 开始录制（覆盖先前序列）
        recordedFrames.Clear();
        recordStartTime = Time.unscaledTime;
        lastRecordTime = Time.unscaledTime;
        isRecording = true;

        isPhantomActive = true;
        isTimeStopped = true;

        DisablePlayerInput(player, true);
        EnablePlayerInput(currentPhantom, true);

        Debug.Log($"[TraceClone] Recording started, t0={recordStartTime}");
    }

    private void RecordCurrentFrame()
    {
        if (phantomController == null) return;
        float elapsed = Time.unscaledTime - recordStartTime;
        Vector2 move = phantomController.GetRawMoveInput();
        bool jump = phantomController.GetRawJumpPressed();
        bool sprint = phantomController.IsSprinting;
        float cameraYaw = Camera.main.transform.eulerAngles.y;

        RecordedFrame frame = new RecordedFrame
        {
            time = elapsed,
            moveInput = move,
            jump = jump,
            sprint = sprint,
            cameraYaw = cameraYaw
        };
        recordedFrames.Add(frame);

        if (recordedFrames.Count % 50 == 0)
            Debug.Log($"[TraceClone] Recorded {recordedFrames.Count} frames, t={elapsed:F2}, input={move}");
    }

    /// <summary>
    /// 按E退出循迹之影：记录序列 + 退出时停 + 在本体位置生成循迹分身
    /// </summary>
    private void ExitTracePhantomAndSpawnClone()
    {
        if (!isTimeStopped || !isPhantomActive) return;

        RecordCurrentFrame();
        Debug.Log($">>> [TraceClone] Exit trace phantom, recorded {recordedFrames.Count} frames");

        Time.timeScale = 1f;

        // 恢复摄像机
        cameraOrbit.target = originalCameraTarget;
        cameraOrbit.SetFreeLookMode(false);
        cameraOrbit.headOffset = originalHeadOffset;
        cameraOrbit.SetControlsEnabled(originalCameraControlsEnabled);

        // 恢复本体
        playerController.canMove = true;
        playerController.freezeGravity = false;

        // 在本体位置生成循迹分身（会销毁已有的）
        SpawnTraceClone(new List<RecordedFrame>(recordedFrames));

        // 销毁影子
        Destroy(currentPhantom);
        currentPhantom = null;
        phantomController = null;

        isPhantomActive = false;
        isTimeStopped = false;
        isRecording = false;
        recordedFrames.Clear();
        hasSavedSequence = false;

        DisablePlayerInput(player, false);

        Debug.Log("[TraceClone] Trace clone spawned and replaying");
    }

    /// <summary>
    /// 按Q从循迹之影切换到视界分身：保存操作序列，清理影子，进入视界分身
    /// </summary>
    private void SwitchToVisionClone()
    {
        if (!isTimeStopped || !isPhantomActive) return;

        RecordCurrentFrame();
        Debug.Log($">>> [TraceClone] Switch to vision clone, saved {recordedFrames.Count} frames");

        // 保存操作序列以便视界分身退出时停时使用
        savedFrames = new List<RecordedFrame>(recordedFrames);
        hasSavedSequence = true;

        // 恢复摄像机
        cameraOrbit.target = originalCameraTarget;
        cameraOrbit.SetFreeLookMode(false);
        cameraOrbit.headOffset = originalHeadOffset;
        cameraOrbit.SetControlsEnabled(originalCameraControlsEnabled);

        // 恢复本体
        playerController.canMove = true;
        playerController.freezeGravity = false;

        // 销毁影子
        Destroy(currentPhantom);
        currentPhantom = null;
        phantomController = null;

        isPhantomActive = false;
        isTimeStopped = false;
        isRecording = false;
        recordedFrames.Clear();

        DisablePlayerInput(player, false);

        // 激活视界分身
        if (cloneManager != null)
            cloneManager.ActivateVisionCloneFromTrace();
    }

    /// <summary>
    /// 在本体位置生成循迹分身并开始回放
    /// </summary>
    private void SpawnTraceClone(List<RecordedFrame> frames)
    {
        if (frames == null || frames.Count == 0) return;

        // 销毁已有循迹分身
        if (currentTraceClone != null) Destroy(currentTraceClone);

        Vector3 spawnPos = player.transform.position;
        Quaternion spawnRot = player.transform.rotation;
        currentTraceClone = Instantiate(playerPrefab, spawnPos, spawnRot);
        currentTraceClone.tag = "TraceClone";
        currentTraceClone.name = "TraceClone";
        ReplaceMaterials(currentTraceClone, traceCloneMaterial);

        IgnoreCollisionBetween(player, currentTraceClone, true);

        // 禁用循迹分身的PlayerInput，防止接收键盘输入
        var traceInput = currentTraceClone.GetComponent<PlayerInput>();
        if (traceInput != null) traceInput.enabled = false;

        var traceCam = currentTraceClone.GetComponentInChildren<Camera>();
        if (traceCam != null) traceCam.enabled = false;

        var traceController = currentTraceClone.GetComponent<PlayerController>();
        traceController.enabled = true;
        traceController.canMove = true;
        traceController.useExternalInput = true;
        traceController.useCameraOverride = true;
        traceController.faceMovementDirection = false;
        traceController.freezeGravity = false;

        var replay = currentTraceClone.AddComponent<TraceCloneReplay>();
        replay.Initialize(frames, traceController);

        Debug.Log($"[TraceClone] Trace clone spawned at {spawnPos}, frames={frames.Count}");
    }

    /// <summary>
    /// 从保存的序列生成循迹分身（由CloneManager在视界分身退出时停时调用）
    /// </summary>
    public void SpawnTraceCloneFromSaved()
    {
        if (!hasSavedSequence || savedFrames.Count == 0) return;

        SpawnTraceClone(new List<RecordedFrame>(savedFrames));
        hasSavedSequence = false;
        savedFrames.Clear();
        Debug.Log("[TraceClone] Spawned trace clone from saved sequence");
    }

    public bool HasSavedSequence() => hasSavedSequence;
    public bool IsTimeStopped() => isTimeStopped;
    public bool IsPhantomActive() => isPhantomActive;

    /// <summary>
    /// 强制退出循迹之影并记录序列（保留序列供后续使用）
    /// </summary>
    public void ForceExitAndRecord()
    {
        if (!isPhantomActive) return;

        RecordCurrentFrame();

        cameraOrbit.target = originalCameraTarget;
        cameraOrbit.SetFreeLookMode(false);
        cameraOrbit.headOffset = originalHeadOffset;
        cameraOrbit.SetControlsEnabled(originalCameraControlsEnabled);

        playerController.canMove = true;
        playerController.freezeGravity = false;

        Destroy(currentPhantom);
        currentPhantom = null;
        phantomController = null;

        isPhantomActive = false;
        isTimeStopped = false;
        isRecording = false;

        DisablePlayerInput(player, false);
    }

    private void ReplaceMaterials(GameObject obj, Material mat)
    {
        var renderers = obj.GetComponentsInChildren<Renderer>();
        foreach (var rend in renderers)
            rend.material = mat;
    }

    private void IgnoreCollisionBetween(GameObject a, GameObject b, bool ignore)
    {
        var collidersA = a.GetComponents<Collider>();
        var collidersB = b.GetComponents<Collider>();
        foreach (var ca in collidersA)
            foreach (var cb in collidersB)
                Physics.IgnoreCollision(ca, cb, ignore);
    }

    private void DisablePlayerInput(GameObject obj, bool disable)
    {
        var input = obj.GetComponent<PlayerInput>();
        if (input != null) input.enabled = !disable;
    }

    private void EnablePlayerInput(GameObject obj, bool enable)
    {
        var input = obj.GetComponent<PlayerInput>();
        if (input != null) input.enabled = enable;
    }
}