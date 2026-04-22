using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class TraceCloneManager : MonoBehaviour
{
    [Header("References")]
    public GameObject playerPrefab;
    public Material phantomMaterial;
    public Material traceCloneMaterial;

    // === 逐Tick录制数据结构 ===

    /// <summary>
    /// 每个FixedUpdate记录一条，回放时逐条喂入，确保确定性一致
    /// </summary>
    [System.Serializable]
    public struct TickRecord
    {
        public Vector2 moveInput;
        public bool jumpTrigger;   // 该Tick是否有新的跳跃按下
        public bool sprint;
        public float cameraYaw;

        public bool activatePressed; //錄製場景互動按鍵
        public bool activateReleased;
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

    //紀錄玩家互動狀態
    private PlayerInteraction phantomInteraction; //B
    private InteractionActor phantomActor; //B

    // 保存摄像机原始状态
    private Transform originalCameraTarget;
    private Vector3 originalHeadOffset;
    private bool originalCameraControlsEnabled;

    // 录制数据
    private List<TickRecord> recordedTicks = new List<TickRecord>();
    private bool isRecording = false;

    // 已保存的操作序列（切换到视界分身时暂存）
    private List<TickRecord> savedTicks = new List<TickRecord>();
    private bool hasSavedSequence = false;

    // Trace clone pause state during time-stop
    private bool isTraceClonePaused = false;

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

    /// <summary>
    /// 逐Tick录制：每个FixedUpdate记录一条phantom的输入状态
    /// </summary>
    private void FixedUpdate()
    {
        if (!isTimeStopped || !isPhantomActive || !isRecording) return;
        if (phantomController == null) return;

        recordedTicks.Add(new TickRecord {
            moveInput = phantomController.GetRawMoveInput(),
            jumpTrigger = phantomController.ConsumeJumpTrigger(),
            sprint = phantomController.IsSprinting,
            cameraYaw = Camera.main.transform.eulerAngles.y,
            
            activatePressed = phantomInteraction != null && phantomInteraction.ConsumeActivatePressed(), //B
            activateReleased = phantomInteraction != null && phantomInteraction.ConsumeActivateReleased() //B
        });
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
        savedTicks.Clear();

        if (cloneManager != null && cloneManager.IsTimeStopped())
            cloneManager.ForceExitTimeStop(false, false);

        PauseTraceClone();

        Debug.Log(">>> [TraceClone] Enter trace phantom state (time stop)");
        // 切换至时停音乐
        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.TransitionToTimeStop();
        }

        // ========== 暂停本体动画（循迹录制期间也算时停） ==========
        var playerAnim = player.GetComponentInChildren<PlayerAnimation>();
        if (playerAnim != null)
        {
            playerAnim.SetPauseAnimation(true);
        }

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
        phantomController.useFixedUpdateMode = true;
        phantomController.enabled = true;

        phantomInteraction = currentPhantom.GetComponent<PlayerInteraction>();
        if (phantomInteraction == null) Debug.LogError("[TraceClone] PlayerInteraction not found");

        var phantomCam = currentPhantom.GetComponentInChildren<Camera>();
        if (phantomCam != null) phantomCam.enabled = false;

        // ========== 确保幻影分身的动画正常启用 ==========
        var phantomAnim = currentPhantom.GetComponentInChildren<PlayerAnimation>();
        if (phantomAnim != null)
        {
            phantomAnim.enabled = true;
            phantomAnim.SetPauseAnimation(false);
        }
        var phantomAnimator = currentPhantom.GetComponentInChildren<Animator>();
        if (phantomAnimator != null)
        {
            phantomAnimator.enabled = true;
            phantomAnimator.speed = 1f;
        }

        IgnoreCollisionBetween(player, currentPhantom, true);

        if (currentTraceClone != null)
            IgnoreCollisionBetween(currentTraceClone, currentPhantom, true);

        cameraOrbit.target = currentPhantom.transform;

        recordedTicks.Clear();
        isRecording = true;

        isPhantomActive = true;
        isTimeStopped = true;

        DisablePlayerInput(player, true);
        EnablePlayerInput(currentPhantom, true);

        Debug.Log("[TraceClone] Tick-based recording started");

        phantomInteraction = currentPhantom.GetComponent<PlayerInteraction>();
        phantomActor = currentPhantom.GetComponent<InteractionActor>();

        if (phantomInteraction != null)
            phantomInteraction.SetExecuteInteractImmediately(false);

        if (phantomActor != null)
            phantomActor.SetCanAffectWorldMechanisms(false);
    }

    private void ExitTracePhantomAndSpawnClone()
    {
        if (!isTimeStopped || !isPhantomActive) return;

        Debug.Log($"[TraceClone] Recording finalized: {recordedTicks.Count} ticks");

        Time.timeScale = 1f;

        // ========== 恢复本体动画 ==========
        var playerAnim = player.GetComponentInChildren<PlayerAnimation>();
        if (playerAnim != null)
        {
            playerAnim.SetPauseAnimation(false);
        }

        // 切换回正常音乐
        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.TransitionToNormal();
        }

        cameraOrbit.target = originalCameraTarget;
        cameraOrbit.SetFreeLookMode(false);
        cameraOrbit.headOffset = originalHeadOffset;
        cameraOrbit.SetControlsEnabled(originalCameraControlsEnabled);

        playerController.canMove = true;
        playerController.freezeGravity = false;

        SpawnTraceClone(new List<TickRecord>(recordedTicks));

        Destroy(currentPhantom);
        currentPhantom = null;
        phantomController = null;
        phantomInteraction = null;

        isPhantomActive = false;
        isTimeStopped = false;
        isRecording = false;
        recordedTicks.Clear();
        hasSavedSequence = false;

        DisablePlayerInput(player, false);

        Debug.Log("[TraceClone] Trace clone spawned and replaying");
    }

    private void SwitchToVisionClone()
    {
        if (!isTimeStopped || !isPhantomActive) return;

        savedTicks = new List<TickRecord>(recordedTicks);
        hasSavedSequence = true;

        Debug.Log($"[TraceClone] Saved {savedTicks.Count} ticks for later replay");

        // ========== 恢复本体动画（切换到视界分身时会由 CloneManager 再次暂停） ==========
        var playerAnim = player.GetComponentInChildren<PlayerAnimation>();
        if (playerAnim != null)
        {
            playerAnim.SetPauseAnimation(false);
        }

        cameraOrbit.target = originalCameraTarget;
        cameraOrbit.SetFreeLookMode(false);
        cameraOrbit.headOffset = originalHeadOffset;
        cameraOrbit.SetControlsEnabled(originalCameraControlsEnabled);

        playerController.canMove = true;
        playerController.freezeGravity = false;

        Destroy(currentPhantom);
        currentPhantom = null;
        phantomController = null;
        phantomInteraction = null;

        isPhantomActive = false;
        isTimeStopped = false;
        isRecording = false;
        recordedTicks.Clear();

        DisablePlayerInput(player, false);

        if (cloneManager != null)
            cloneManager.ActivateVisionCloneFromTrace();
    }

    private void SpawnTraceClone(List<TickRecord> ticks)
    {
        if (ticks == null || ticks.Count == 0) return;

        if (currentTraceClone != null) Destroy(currentTraceClone);
        isTraceClonePaused = false;

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
        traceController.useFixedUpdateMode = true;

        var traceInteraction = currentTraceClone.GetComponent<PlayerInteraction>();
        if (traceInteraction == null)
            Debug.LogError("[TraceClone] PlayerInteraction not found");

        var replay = currentTraceClone.AddComponent<TraceCloneReplay>();
        replay.Initialize(ticks, traceController, traceInteraction);

        if (traceInteraction != null)
            traceInteraction.SetExecuteInteractImmediately(true);

        var traceActor = currentTraceClone.GetComponent<InteractionActor>();
        if (traceActor != null)
            traceActor.SetCanAffectWorldMechanisms(true);

        // ========== 确保循迹分身的动画正常启用 ==========
        var traceAnim = currentTraceClone.GetComponentInChildren<PlayerAnimation>();
        if (traceAnim != null)
        {
            traceAnim.enabled = true;
            traceAnim.SetPauseAnimation(false);
        }
        var traceAnimator = currentTraceClone.GetComponentInChildren<Animator>();
        if (traceAnimator != null)
        {
            traceAnimator.enabled = true;
            traceAnimator.speed = 1f;
        }

        Debug.Log($"[TraceClone] Trace clone spawned, {ticks.Count} ticks to replay");
    }

    public void SpawnTraceCloneFromSaved()
    {
        if (!hasSavedSequence || savedTicks.Count == 0) return;

        SpawnTraceClone(new List<TickRecord>(savedTicks));
        hasSavedSequence = false;
        savedTicks.Clear();
        Debug.Log("[TraceClone] Spawned trace clone from saved sequence");
    }

    public bool HasSavedSequence() => hasSavedSequence;
    public bool IsTimeStopped() => isTimeStopped;
    public bool IsPhantomActive() => isPhantomActive;

    /// <summary>
    /// Pauses the current trace clone's replay and hides it.
    /// Called when any time-stop begins to prevent coexistence.
    /// </summary>
    public void PauseTraceClone()
    {
        if (currentTraceClone == null) return;
        var replay = currentTraceClone.GetComponent<TraceCloneReplay>();
        if (replay != null) replay.enabled = false;
        var renderers = currentTraceClone.GetComponentsInChildren<Renderer>();
        foreach (var r in renderers) r.enabled = false;
        isTraceClonePaused = true;
        Debug.Log("[TraceClone] Trace clone paused and hidden");

        var actor = currentTraceClone.GetComponent<InteractionActor>();
        if (actor != null) actor.SetCanAffectWorldMechanisms(false);

        var colliders = currentTraceClone.GetComponentsInChildren<Collider>();
        foreach (var c in colliders) c.enabled = false;
    }

    /// <summary>
    /// Resumes the current trace clone's replay and makes it visible.
    /// Called when time-stop ends without a new trace sequence.
    /// </summary>
    public void ResumeTraceClone()
    {
        if (currentTraceClone == null || !isTraceClonePaused) return;
        var replay = currentTraceClone.GetComponent<TraceCloneReplay>();
        if (replay != null) replay.enabled = true;
        var renderers = currentTraceClone.GetComponentsInChildren<Renderer>();
        foreach (var r in renderers) r.enabled = true;
        isTraceClonePaused = false;
        Debug.Log("[TraceClone] Trace clone resumed and visible");


        var actor = currentTraceClone.GetComponent<InteractionActor>();
        if (actor != null) actor.SetCanAffectWorldMechanisms(true);

        var colliders = currentTraceClone.GetComponentsInChildren<Collider>();
        foreach (var c in colliders) c.enabled = true;
    }

    /// <summary>
    /// Discards any saved sequence from a previous trace phantom session.
    /// </summary>
    public void ClearSavedSequence()
    {
        hasSavedSequence = false;
        savedTicks.Clear();
    }

    public void ForceExitAndRecord()
    {
        if (!isPhantomActive) return;

        cameraOrbit.target = originalCameraTarget;
        cameraOrbit.SetFreeLookMode(false);
        cameraOrbit.headOffset = originalHeadOffset;
        cameraOrbit.SetControlsEnabled(originalCameraControlsEnabled);

        playerController.canMove = true;
        playerController.freezeGravity = false;

        Destroy(currentPhantom);
        currentPhantom = null;
        phantomController = null;
        phantomInteraction = null; //B

        isPhantomActive = false;
        isTimeStopped = false;
        isRecording = false;

        DisablePlayerInput(player, false);
    }

    /// <summary>
    /// Cleanly exits trace phantom state without spawning a clone or saving any recorded data.
    /// Called by PauseManager when ESC is pressed during trace phantom state.
    /// </summary>
    public void ForceExitClean()
    {
        if (!isPhantomActive) return;

        Debug.Log("[TraceClone] Clean exit (ESC) - discarding recorded ticks");

        // ========== 恢复本体动画 ==========
        var playerAnim = player.GetComponentInChildren<PlayerAnimation>();
        if (playerAnim != null)
        {
            playerAnim.SetPauseAnimation(false);
        }

        // 切换回正常音乐
        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.TransitionToNormal();
        }

        cameraOrbit.target = originalCameraTarget;
        cameraOrbit.SetFreeLookMode(false);
        cameraOrbit.headOffset = originalHeadOffset;
        cameraOrbit.SetControlsEnabled(originalCameraControlsEnabled);

        playerController.canMove = true;
        playerController.freezeGravity = false;

        Destroy(currentPhantom);
        currentPhantom = null;
        phantomController = null;
        phantomInteraction = null;

        isPhantomActive = false;
        isTimeStopped = false;
        isRecording = false;
        recordedTicks.Clear();
        savedTicks.Clear();
        hasSavedSequence = false;

        DisablePlayerInput(player, false);

        ResumeTraceClone();
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
