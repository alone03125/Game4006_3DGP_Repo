using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class TraceCloneManager : MonoBehaviour
{
    [Header("References")]
    public GameObject playerPrefab;
    public Material phantomMaterial;
    public Material traceCloneMaterial;

    [Header("录制设置")]
    [Tooltip("状态快照间隔（秒），用于空间修正校验点")]
    public float snapshotInterval = 0.1f;

    [Header("回放空间修正")]
    [Tooltip("空间修正容差：位置偏差在此范围内执行刚性位置修正")]
    public float correctionTolerance = 0.3f;
    [Tooltip("修正消除阈值：偏差超过此值则永久取消本次行动的空间修正")]
    public float correctionBreakThreshold = 1.5f;
    [Tooltip("跳跃前瞻时间窗口（秒）：落地时在此时间内搜索跳跃事件")]
    public float jumpLookaheadWindow = 0.15f;
    [Tooltip("跳跃回溯时间窗口（秒）：落地时在过去此时间内搜索未执行的跳跃事件")]
    public float jumpLookbackWindow = 0.12f;

    // === 事件驱动的录制数据结构 ===

    /// <summary>
    /// 输入事件：记录输入状态变化的精确时刻
    /// </summary>
    [System.Serializable]
    public struct InputEvent
    {
        public float time;           // 精确时间戳
        public InputEventType type;
        public Vector2 moveValue;    // Move事件的值
        public float cameraYaw;      // 每个事件都记录当时的摄像机朝向
        public bool sprintState;     // 每个事件都记录当时的冲刺状态
    }

    public enum InputEventType
    {
        MoveChanged,     // 移动输入变化
        JumpPressed,     // 跳跃按下
        JumpReleased,    // 跳跃松开
        SprintChanged,   // 冲刺状态变化
    }

    /// <summary>
    /// 状态快照：定期记录的关键状态，用于空间修正
    /// </summary>
    [System.Serializable]
    public struct StateSnapshot
    {
        public float time;
        public Vector3 position;
        public Quaternion rotation;
        public bool grounded;
        public Vector3 velocity;     // 记录速度用于更精确的校验
    }

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

    // 录制数据
    private List<InputEvent> recordedEvents = new List<InputEvent>();
    private List<StateSnapshot> recordedSnapshots = new List<StateSnapshot>();
    private float recordStartTime;
    private float lastSnapshotTime;
    private bool isRecording = false;

    // 上一帧输入状态（用于检测变化）
    private Vector2 prevMoveInput;
    private bool prevJumpState;
    private bool prevSprintState;

    // 已保存的操作序列
    private List<InputEvent> savedEvents = new List<InputEvent>();
    private List<StateSnapshot> savedSnapshots = new List<StateSnapshot>();
    private bool hasSavedSequence = false;

    private CloneManager cloneManager;

    private void Awake()
    {
        player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) Debug.LogError("[TraceClone] Player not found");
        playerController = player.GetComponent<PlayerController>();
        cameraOrbit = Camera.main.GetComponent<SimpleCameraOrbit>();
        cloneManager = GetComponent<CloneManager>();
    }

    /// <summary>
    /// 由PlayerInput组件通过SendMessages调用，绑定TraceActivate操作
    /// </summary>
    void OnTraceActivate(InputValue value)
    {
        if (!value.isPressed) return;
        if (!isTimeStopped)
            ActivateTracePhantom();
        else if (isPhantomActive)
            ExitTracePhantomAndSpawnClone();
    }

    private void Update()
    {
        if (!isTimeStopped || !isPhantomActive || !isRecording) return;
        if (phantomController == null) return;

        float now = Time.unscaledTime;
        float elapsed = now - recordStartTime;

        // 检测输入变化并生成事件
        Vector2 curMove = phantomController.GetRawMoveInput();
        bool curJump = phantomController.GetRawJumpPressed();
        bool curSprint = phantomController.IsSprinting;
        float curYaw = Camera.main.transform.eulerAngles.y;

        if (curMove != prevMoveInput)
        {
            recordedEvents.Add(new InputEvent {
                time = elapsed,
                type = InputEventType.MoveChanged,
                moveValue = curMove,
                cameraYaw = curYaw,
                sprintState = curSprint
            });
            prevMoveInput = curMove;
        }

        if (curJump && !prevJumpState)
        {
            recordedEvents.Add(new InputEvent {
                time = elapsed,
                type = InputEventType.JumpPressed,
                moveValue = curMove,
                cameraYaw = curYaw,
                sprintState = curSprint
            });
        }
        else if (!curJump && prevJumpState)
        {
            recordedEvents.Add(new InputEvent {
                time = elapsed,
                type = InputEventType.JumpReleased,
                moveValue = curMove,
                cameraYaw = curYaw,
                sprintState = curSprint
            });
        }
        prevJumpState = curJump;

        if (curSprint != prevSprintState)
        {
            recordedEvents.Add(new InputEvent {
                time = elapsed,
                type = InputEventType.SprintChanged,
                moveValue = curMove,
                cameraYaw = curYaw,
                sprintState = curSprint
            });
            prevSprintState = curSprint;
        }

        // 定期状态快照
        if (now - lastSnapshotTime >= snapshotInterval)
        {
            var cc = phantomController.Controller;
            recordedSnapshots.Add(new StateSnapshot {
                time = elapsed,
                position = currentPhantom.transform.position,
                rotation = currentPhantom.transform.rotation,
                grounded = cc != null && cc.isGrounded,
                velocity = Vector3.zero
            });
            lastSnapshotTime = now;

            if (recordedSnapshots.Count % 20 == 0)
                Debug.Log($"[TraceClone] {recordedEvents.Count} events, {recordedSnapshots.Count} snapshots, t={elapsed:F3}s");
        }
    }

    public void HandleQDuringPhantom()
    {
        if (isTimeStopped && isPhantomActive)
            SwitchToVisionClone();
    }

    private void ActivateTracePhantom()
    {
        if (isPhantomActive) return;

        hasSavedSequence = false;
        savedEvents.Clear();
        savedSnapshots.Clear();

        if (cloneManager != null && cloneManager.IsTimeStopped())
            cloneManager.ForceExitTimeStop(false);

        Debug.Log(">>> [TraceClone] Enter trace phantom state (time stop)");

        originalCameraTarget = cameraOrbit.target;
        originalHeadOffset = cameraOrbit.headOffset;
        originalCameraControlsEnabled = cameraOrbit.controlsEnabled;

        cameraOrbit.SetFreeLookMode(true, 6f);

        playerController.canMove = false;
        playerController.freezeGravity = true;

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

        if (currentTraceClone != null)
            IgnoreCollisionBetween(currentTraceClone, currentPhantom, true);

        cameraOrbit.target = currentPhantom.transform;

        // 初始化录制
        recordedEvents.Clear();
        recordedSnapshots.Clear();
        recordStartTime = Time.unscaledTime;
        lastSnapshotTime = Time.unscaledTime;
        isRecording = true;

        prevMoveInput = Vector2.zero;
        prevJumpState = false;
        prevSprintState = false;

        // 记录初始状态快照
        recordedSnapshots.Add(new StateSnapshot {
            time = 0f,
            position = spawnPos,
            rotation = spawnRot,
            grounded = true,
            velocity = Vector3.zero
        });

        isPhantomActive = true;
        isTimeStopped = true;

        DisablePlayerInput(player, true);
        EnablePlayerInput(currentPhantom, true);

        Debug.Log($"[TraceClone] Event recording started");
    }

    private void FinalizeRecording()
    {
        if (phantomController == null) return;
        float elapsed = Time.unscaledTime - recordStartTime;

        // 最终快照
        var cc = phantomController.Controller;
        recordedSnapshots.Add(new StateSnapshot {
            time = elapsed,
            position = currentPhantom.transform.position,
            rotation = currentPhantom.transform.rotation,
            grounded = cc != null && cc.isGrounded,
            velocity = Vector3.zero
        });

        Debug.Log($"[TraceClone] Recording finalized: {recordedEvents.Count} events, {recordedSnapshots.Count} snapshots, duration={elapsed:F3}s");
    }

    private void ExitTracePhantomAndSpawnClone()
    {
        if (!isTimeStopped || !isPhantomActive) return;

        FinalizeRecording();

        Time.timeScale = 1f;

        cameraOrbit.target = originalCameraTarget;
        cameraOrbit.SetFreeLookMode(false);
        cameraOrbit.headOffset = originalHeadOffset;
        cameraOrbit.SetControlsEnabled(originalCameraControlsEnabled);

        playerController.canMove = true;
        playerController.freezeGravity = false;

        SpawnTraceClone(
            new List<InputEvent>(recordedEvents),
            new List<StateSnapshot>(recordedSnapshots)
        );

        Destroy(currentPhantom);
        currentPhantom = null;
        phantomController = null;

        isPhantomActive = false;
        isTimeStopped = false;
        isRecording = false;
        recordedEvents.Clear();
        recordedSnapshots.Clear();
        hasSavedSequence = false;

        DisablePlayerInput(player, false);

        Debug.Log("[TraceClone] Trace clone spawned and replaying");
    }

    private void SwitchToVisionClone()
    {
        if (!isTimeStopped || !isPhantomActive) return;

        FinalizeRecording();

        savedEvents = new List<InputEvent>(recordedEvents);
        savedSnapshots = new List<StateSnapshot>(recordedSnapshots);
        hasSavedSequence = true;

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
        recordedEvents.Clear();
        recordedSnapshots.Clear();

        DisablePlayerInput(player, false);

        if (cloneManager != null)
            cloneManager.ActivateVisionCloneFromTrace();
    }

    private void SpawnTraceClone(List<InputEvent> events, List<StateSnapshot> snapshots)
    {
        if (events == null || events.Count == 0) return;

        if (currentTraceClone != null) Destroy(currentTraceClone);

        Vector3 spawnPos = player.transform.position;
        Quaternion spawnRot = player.transform.rotation;
        currentTraceClone = Instantiate(playerPrefab, spawnPos, spawnRot);
        currentTraceClone.tag = "TraceClone";
        currentTraceClone.name = "TraceClone";
        ReplaceMaterials(currentTraceClone, traceCloneMaterial);

        IgnoreCollisionBetween(player, currentTraceClone, true);

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
        traceController.useFixedUpdateMode = true;  // 确定性FixedUpdate驱动

        var replay = currentTraceClone.AddComponent<TraceCloneReplay>();
        replay.Initialize(events, snapshots, traceController,
            correctionTolerance, correctionBreakThreshold,
            jumpLookaheadWindow, jumpLookbackWindow);

        Debug.Log($"[TraceClone] Trace clone spawned, {events.Count} events, {snapshots.Count} snapshots");
    }

    public void SpawnTraceCloneFromSaved()
    {
        if (!hasSavedSequence || savedEvents.Count == 0) return;

        SpawnTraceClone(
            new List<InputEvent>(savedEvents),
            new List<StateSnapshot>(savedSnapshots)
        );
        hasSavedSequence = false;
        savedEvents.Clear();
        savedSnapshots.Clear();
        Debug.Log("[TraceClone] Spawned trace clone from saved sequence");
    }

    public bool HasSavedSequence() => hasSavedSequence;
    public bool IsTimeStopped() => isTimeStopped;
    public bool IsPhantomActive() => isPhantomActive;

    public void ForceExitAndRecord()
    {
        if (!isPhantomActive) return;

        FinalizeRecording();

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