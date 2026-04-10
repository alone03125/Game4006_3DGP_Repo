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

    private bool isTimeStopped = false;
    private bool isPhantomActive = false;
    private GameObject currentPhantom;
    private GameObject currentTraceClone;
    private PlayerController phantomController;
    private PlayerController playerController;
    private SimpleCameraOrbit cameraOrbit;
    private GameObject player;

    // 保存原始状态
    private Transform originalCameraTarget;
    private Vector3 originalHeadOffset;
    private bool originalCameraControlsEnabled;

    private List<RecordedFrame> recordedFrames = new List<RecordedFrame>();
    private float recordStartTime;
    private float lastRecordTime;
    private bool isRecording = false;

    private InputAction eAction;
    private CloneManager cloneManager;

    [System.Serializable]
    public struct RecordedFrame
    {
        public float time;
        public Vector2 moveInput;
        public bool jump;
        public bool sprint;
    }

    private void Awake()
    {
        player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) Debug.LogError("未找到Player");
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
        // 录制输入帧
        if (isTimeStopped && isPhantomActive && isRecording)
        {
            if (Time.time - lastRecordTime >= recordFixedDeltaTime)
            {
                RecordCurrentFrame();
                lastRecordTime = Time.time;
            }
        }

        // Q键切换到视界分身
        if (isTimeStopped && isPhantomActive && Keyboard.current.qKey.wasPressedThisFrame)
        {
            SwitchToVisionClone();
        }
    }

    private void OnEPerformed(InputAction.CallbackContext ctx)
    {
        if (!isTimeStopped)
            ActivateTracePhantom();
        else
            ExitTracePhantomAndSpawnClone();
    }

    private void ActivateTracePhantom()
    {
        if (isPhantomActive) return;

        // 如果视界分身激活，先退出
        if (cloneManager != null && cloneManager.IsTimeStopped())
        {
            cloneManager.ForceExitTimeStop(false);
        }

        if (currentTraceClone != null) Destroy(currentTraceClone);

        Debug.Log(">>> 进入循迹之影状态（时停）");

        // 保存原始状态
        originalCameraTarget = cameraOrbit.target;
        originalHeadOffset = cameraOrbit.headOffset;
        originalCameraControlsEnabled = cameraOrbit.controlsEnabled;

        // 设置为第三人称后上方跟随（可自由旋转）
        cameraOrbit.headOffset = new Vector3(0, 2.5f, -6f);
        // 不锁定相机，启用鼠标控制
        cameraOrbit.SetCameraLock(false, Quaternion.identity, true);
        cameraOrbit.SetControlsEnabled(true);

        // 禁用本体控制
        playerController.canMove = false;
        playerController.freezeGravity = true;

        // 生成幻影分身
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

        // 相机跟随幻影
        cameraOrbit.target = currentPhantom.transform;

        // 初始化录制
        recordedFrames.Clear();
        recordStartTime = Time.time;
        lastRecordTime = Time.time;
        isRecording = true;

        isPhantomActive = true;
        isTimeStopped = true;

        DisablePlayerInput(player, true);
        EnablePlayerInput(currentPhantom, true);
    }

    private void RecordCurrentFrame()
    {
        if (phantomController == null) return;
        float elapsed = Time.time - recordStartTime;
        RecordedFrame frame = new RecordedFrame
        {
            time = elapsed,
            moveInput = phantomController.GetRawMoveInput(),
            jump = phantomController.GetRawJumpPressed(),
            sprint = phantomController.IsSprinting
        };
        recordedFrames.Add(frame);
    }

    private void ExitTracePhantomAndSpawnClone()
    {
        if (!isTimeStopped || !isPhantomActive) return;

        RecordCurrentFrame(); // 确保最后一帧录入

        Debug.Log(">>> 退出循迹之影状态，生成复刻分身");

        // 恢复相机目标与偏移
        cameraOrbit.target = originalCameraTarget;
        cameraOrbit.headOffset = originalHeadOffset;
        cameraOrbit.SetCameraLock(false, Quaternion.identity);
        cameraOrbit.SetControlsEnabled(originalCameraControlsEnabled);

        // 恢复本体控制
        playerController.canMove = true;
        playerController.freezeGravity = false;

        // 生成复刻分身
        if (currentTraceClone != null) Destroy(currentTraceClone);
        Vector3 spawnPos = player.transform.position;
        Quaternion spawnRot = player.transform.rotation;
        currentTraceClone = Instantiate(playerPrefab, spawnPos, spawnRot);
        currentTraceClone.tag = "TraceClone";
        currentTraceClone.name = "TraceClone";
        ReplaceMaterials(currentTraceClone, traceCloneMaterial);

        IgnoreCollisionBetween(player, currentTraceClone, true);

        var traceController = currentTraceClone.GetComponent<PlayerController>();
        traceController.enabled = true;
        traceController.canMove = true;                 // 允许移动
        traceController.useExternalInput = true;
        traceController.faceMovementDirection = false;
        traceController.freezeGravity = false;

        var replay = currentTraceClone.AddComponent<TraceCloneReplay>();
        replay.Initialize(recordedFrames, recordFixedDeltaTime, traceController);

        // 清理幻影
        Destroy(currentPhantom);
        currentPhantom = null;
        phantomController = null;

        isPhantomActive = false;
        isTimeStopped = false;
        isRecording = false;
        recordedFrames.Clear();

        DisablePlayerInput(player, false);
    }

    private void SwitchToVisionClone()
    {
        if (!isTimeStopped || !isPhantomActive) return;

        RecordCurrentFrame();

        // 恢复相机
        cameraOrbit.target = originalCameraTarget;
        cameraOrbit.headOffset = originalHeadOffset;
        cameraOrbit.SetCameraLock(false, Quaternion.identity);
        cameraOrbit.SetControlsEnabled(originalCameraControlsEnabled);

        // 恢复本体控制
        playerController.canMove = true;
        playerController.freezeGravity = false;

        Destroy(currentPhantom);
        currentPhantom = null;
        phantomController = null;

        isPhantomActive = false;
        isTimeStopped = false;
        isRecording = false;

        DisablePlayerInput(player, false);

        if (cloneManager != null)
            cloneManager.ActivateVisionCloneFromTrace();
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

    public void ForceExitAndRecord()
    {
        if (isPhantomActive)
        {
            RecordCurrentFrame();
            cameraOrbit.target = originalCameraTarget;
            cameraOrbit.headOffset = originalHeadOffset;
            cameraOrbit.SetCameraLock(false, Quaternion.identity);
            cameraOrbit.SetControlsEnabled(originalCameraControlsEnabled);
            playerController.canMove = true;
            playerController.freezeGravity = false;
            Destroy(currentPhantom);
            isPhantomActive = false;
            isTimeStopped = false;
            isRecording = false;
            DisablePlayerInput(player, false);
        }
    }

    public bool IsTimeStopped() => isTimeStopped;
}