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
    [Tooltip("状态快照间隔（秒）")]
    public float snapshotInterval = 0.1f;

    // === 数据结构 ===
    [System.Serializable]
    public struct InputEvent
    {
        public float time;
        public InputEventType type;
        public Vector2 moveValue;
        public float cameraYaw;
        public bool sprintState;
        public bool jumpState;
    }

    public enum InputEventType
    {
        MoveChanged,
        JumpPressed,
        JumpReleased,
        SprintChanged,
    }

    [System.Serializable]
    public struct StateSnapshot
    {
        public float time;
        public Vector3 position;
        public Quaternion rotation;
        public bool grounded;
        public Vector3 velocity;
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

    private Transform originalCameraTarget;
    private Vector3 originalHeadOffset;
    private bool originalCameraControlsEnabled;

    // 录制数据
    private List<InputEvent> recordedEvents = new List<InputEvent>();
    private List<StateSnapshot> recordedSnapshots = new List<StateSnapshot>();
    private float recordStartFixedTime;
    private float lastSnapshotFixedTime;
    private bool isRecording = false;

    private Vector2 prevMoveInput;
    private bool prevJumpState;
    private bool prevSprintState;

    // 保存的序列
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

    void OnTraceActivate(InputValue value)
    {
        if (!value.isPressed) return;
        if (!isTimeStopped)
            ActivateTracePhantom();
        else if (isPhantomActive)
            ExitTracePhantomAndSpawnClone();
    }

    private void FixedUpdate()
    {
        if (!isTimeStopped || !isPhantomActive || !isRecording) return;
        if (phantomController == null) return;

        float nowFixed = Time.fixedTime;
        float elapsed = nowFixed - recordStartFixedTime;

        // 检测输入变化
        Vector2 curMove = phantomController.GetRawMoveInput();
        bool curJump = phantomController.GetRawJumpPressed();
        bool curSprint = phantomController.IsSprinting;
        float curYaw = Camera.main.transform.eulerAngles.y;

        if (curMove != prevMoveInput)
        {
            recordedEvents.Add(new InputEvent
            {
                time = elapsed,
                type = InputEventType.MoveChanged,
                moveValue = curMove,
                cameraYaw = curYaw,
                sprintState = curSprint,
                jumpState = curJump
            });
            prevMoveInput = curMove;
        }

        if (curJump && !prevJumpState)
        {
            recordedEvents.Add(new InputEvent
            {
                time = elapsed,
                type = InputEventType.JumpPressed,
                moveValue = curMove,
                cameraYaw = curYaw,
                sprintState = curSprint,
                jumpState = true
            });
        }
        else if (!curJump && prevJumpState)
        {
            recordedEvents.Add(new InputEvent
            {
                time = elapsed,
                type = InputEventType.JumpReleased,
                moveValue = curMove,
                cameraYaw = curYaw,
                sprintState = curSprint,
                jumpState = false
            });
        }
        prevJumpState = curJump;

        if (curSprint != prevSprintState)
        {
            recordedEvents.Add(new InputEvent
            {
                time = elapsed,
                type = InputEventType.SprintChanged,
                moveValue = curMove,
                cameraYaw = curYaw,
                sprintState = curSprint,
                jumpState = curJump
            });
            prevSprintState = curSprint;
        }

        // 定期状态快照
        if (nowFixed - lastSnapshotFixedTime >= snapshotInterval)
        {
            var cc = phantomController.Controller;
            Vector3 velocity = phantomController.GetCurrentHorizontalSpeed() * currentPhantom.transform.forward +
                               Vector3.up * phantomController.GetCurrentVerticalSpeed();

            recordedSnapshots.Add(new StateSnapshot
            {
                time = elapsed,
                position = currentPhantom.transform.position,
                rotation = currentPhantom.transform.rotation,
                grounded = cc != null && cc.isGrounded,
                velocity = velocity
            });
            lastSnapshotFixedTime = nowFixed;
        }
    }

    private void ActivateTracePhantom()
    {
        if (isPhantomActive) return;

        hasSavedSequence = false;
        savedEvents.Clear();
        savedSnapshots.Clear();

        if (cloneManager != null && cloneManager.IsTimeStopped())
            cloneManager.ForceExitTimeStop(false);

        Debug.Log(">>> [TraceClone] Enter trace phantom state");

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

        recordedEvents.Clear();
        recordedSnapshots.Clear();
        recordStartFixedTime = Time.fixedTime;
        lastSnapshotFixedTime = Time.fixedTime;
        isRecording = true;

        prevMoveInput = Vector2.zero;
        prevJumpState = false;
        prevSprintState = false;

        recordedSnapshots.Add(new StateSnapshot
        {
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
    }

    private void FinalizeRecording()
    {
        if (phantomController == null) return;
        float elapsed = Time.fixedTime - recordStartFixedTime;

        var cc = phantomController.Controller;
        recordedSnapshots.Add(new StateSnapshot
        {
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
    }

    public void SwitchToVisionClone()
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
        currentTraceClone.name = "TraceClone_Follow";
        ReplaceMaterials(currentTraceClone, traceCloneMaterial);

        IgnoreCollisionBetween(player, currentTraceClone, true);

        var traceInput = currentTraceClone.GetComponent<PlayerInput>();
        if (traceInput != null) traceInput.enabled = false;

        var traceCam = currentTraceClone.GetComponentInChildren<Camera>();
        if (traceCam != null) traceCam.enabled = false;

        var traceController = currentTraceClone.GetComponent<PlayerController>();
        traceController.enabled = true;

        // 添加路径跟随组件（替代原来的 TraceCloneReplay）
        var follow = currentTraceClone.AddComponent<TraceCloneFollowPath>();
        follow.Initialize(events, snapshots, traceController);

        Debug.Log("[TraceClone] Spawned path-following trace clone.");
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
    }

    public bool HasSavedSequence() => hasSavedSequence;
    public bool IsTimeStopped() => isTimeStopped;
    public bool IsPhantomActive() => isPhantomActive;

    public void HandleQDuringPhantom()
    {
        if (isTimeStopped && isPhantomActive)
            SwitchToVisionClone();
    }

    private void ReplaceMaterials(GameObject obj, Material mat)
    {
        foreach (var rend in obj.GetComponentsInChildren<Renderer>())
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