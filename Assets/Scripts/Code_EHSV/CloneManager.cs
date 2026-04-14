using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class CloneManager : MonoBehaviour
{
    [Header("References")]
    public GameObject playerPrefab;
    public Material visionCloneMaterial;
    public Material solidCloneMaterial;

    [Header("UI (Optional)")]
    public GameObject triangleUIPrefab;
    public Color unselectedColor = Color.yellow;

    [Header("VFX")]
    public GameObject swapVFXPrefab;
    public GameObject solidCloneVFXPrefab;

    [Header("Detection")]
    public float separationDistance = 0.1f;
    public float proximityRadius = 2.5f;
    public LayerMask occlusionMask = -1;

    [Header("Transparent Layer")]
    public LayerMask transparentLayer; // 分身可穿越的层，且不阻挡视线

    // 状态
    private bool isTimeStopped = false;
    private bool cameraLocked = false;
    private bool isCloneActive = false;
    private bool hasSeparated = false;

    private GameObject currentClone;
    private GameObject currentSolidClone;

    private GameObject triangleUI;
    private TMP_Text triangleText;

    private PlayerController playerController;
    private SimpleCameraOrbit cameraOrbit;
    private GameObject player;
    private CharacterController playerCharController;

    private Quaternion lockedCameraRotation;
    private InputAction qAction;
    private InputAction tabAction;
    private InputAction eAction;
    private TraceCloneManager traceCloneManager;

    private float spawnProtectionTimer = 0f;
    private const float SPAWN_PROTECTION_DURATION = 0.5f;

    private void Awake()
    {
        player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) Debug.LogError("未找到Player标签的游戏对象");
        playerController = player.GetComponent<PlayerController>();
        playerCharController = player.GetComponent<CharacterController>();
        cameraOrbit = Camera.main.GetComponent<SimpleCameraOrbit>();
        traceCloneManager = GetComponent<TraceCloneManager>();

        var inputActions = new InputActionMap();
        qAction = inputActions.AddAction("Q", binding: "<Keyboard>/q");
        tabAction = inputActions.AddAction("Tab", binding: "<Keyboard>/tab");
        eAction = inputActions.AddAction("E", binding: "<Keyboard>/e");
        qAction.performed += OnQPerformed;
        tabAction.performed += OnTabPerformed;
        eAction.performed += OnEPerformed;
        inputActions.Enable();
    }

    private void Update()
    {
        if (!isTimeStopped)
        {
            if (currentSolidClone != null)
            {
                if (!IsCloneInSight(currentSolidClone) || IsCloneOccluded(currentSolidClone))
                {
                    Debug.Log("固化实体离开视野或被遮挡，销毁");
                    Destroy(currentSolidClone);
                    currentSolidClone = null;
                }
            }
            return;
        }

        if (cameraLocked && player != null)
        {
            Camera.main.transform.position = player.transform.position + cameraOrbit.headOffset;
            Camera.main.transform.rotation = lockedCameraRotation;
        }

        if (isCloneActive && currentClone != null && spawnProtectionTimer <= 0f)
        {
            float distToPlayer = Vector3.Distance(player.transform.position, currentClone.transform.position);
            bool isInProximity = distToPlayer <= proximityRadius;

            if (!isInProximity && (!IsCloneInSight(currentClone) || IsCloneOccluded(currentClone)))
            {
                Debug.Log($"分身距离本体 {distToPlayer:F2} > {proximityRadius} 且丢失视野或被遮挡，强制退出时停");
                ExitTimeStop(false, swapOnExit: false);
            }
        }
        else if (spawnProtectionTimer > 0f)
        {
            spawnProtectionTimer -= Time.deltaTime;
        }
    }

    private void OnQPerformed(InputAction.CallbackContext ctx)
    {
        if (!isTimeStopped)
        {
            if (traceCloneManager != null && traceCloneManager.IsPhantomActive())
            {
                traceCloneManager.HandleQDuringPhantom();
                return;
            }
            ActivateVisionClone();
        }
        else
        {
            ExitTimeStop(true, swapOnExit: false);
        }
    }

    private void OnTabPerformed(InputAction.CallbackContext ctx)
    {
        if (isTimeStopped && isCloneActive)
        {
            ExitTimeStop(true, swapOnExit: true);
        }
        else if (!isTimeStopped && currentSolidClone != null)
        {
            SwapWithSolidClone();
        }
    }

    private void OnEPerformed(InputAction.CallbackContext ctx)
    {
        if (isTimeStopped && isCloneActive)
            SwitchToTracePhantom();
    }

    private void SwitchToTracePhantom()
    {
        if (!isTimeStopped || !isCloneActive) return;

        Debug.Log(">>> [CloneManager] 从视界分身切换到循迹幻影");

        cameraLocked = false;
        cameraOrbit.SetCameraLock(false, Quaternion.identity);

        playerController.canMove = true;
        playerController.freezeGravity = false;

        if (currentClone != null) Destroy(currentClone);
        if (triangleUI != null) Destroy(triangleUI);
        DisablePlayerInput(player, false);

        currentClone = null;
        isCloneActive = false;
        isTimeStopped = false;
        hasSeparated = false;
        spawnProtectionTimer = 0f;

        if (traceCloneManager != null)
            traceCloneManager.ActivateTracePhantom();
    }

    private void ActivateVisionClone()
    {
        if (isCloneActive) return;

        if (currentSolidClone != null) Destroy(currentSolidClone);

        if (traceCloneManager != null)
            traceCloneManager.PauseTraceClone();

        Debug.Log(">>> 进入时停（视界分身）");
        cameraLocked = true;
        lockedCameraRotation = Camera.main.transform.rotation;
        cameraOrbit.SetCameraLock(true, lockedCameraRotation, true);

        playerController.canMove = false;
        playerController.freezeGravity = true;

        Vector3 spawnPos = player.transform.position;
        Quaternion spawnRot = player.transform.rotation;
        currentClone = Instantiate(playerPrefab, spawnPos, spawnRot);
        currentClone.tag = "VisionClone";
        currentClone.name = "VisionClone";

        ReplaceMaterials(currentClone, visionCloneMaterial);

        var cloneController = currentClone.GetComponent<PlayerController>();
        cloneController.faceMovementDirection = true;
        cloneController.canMove = true;
        cloneController.freezeGravity = false;
        cloneController.enabled = true;

        var cloneCamera = currentClone.GetComponentInChildren<Camera>();
        if (cloneCamera != null) cloneCamera.enabled = false;

        currentClone.GetComponent<CharacterController>().enabled = true;

        IgnoreCollisionBetween(player, currentClone, true);

        // 忽略分身与透明层物体的碰撞
        IgnoreCollisionWithLayer(currentClone, transparentLayer, true);

        hasSeparated = false;

        cloneController.OnPositionChanged += (oldPos, newPos) => {
            OnCloneMoved(oldPos, newPos);
        };

        CreateTriangleUI();

        isCloneActive = true;
        isTimeStopped = true;

        DisablePlayerInput(player, true);
        EnablePlayerInput(currentClone, true);

        spawnProtectionTimer = SPAWN_PROTECTION_DURATION;
        Debug.Log("视界分身已生成");
    }

    private void ExitTimeStop(bool shouldGenerateSolid, bool swapOnExit, bool handleTraceClone = true)
    {
        if (!isTimeStopped) return;

        Debug.Log(">>> 退出时停");

        cameraLocked = false;
        cameraOrbit.SetCameraLock(false, Quaternion.identity);

        playerController.canMove = true;
        playerController.freezeGravity = false;

        if (shouldGenerateSolid && currentClone != null)
        {
            bool isSeparated = hasSeparated || Vector3.Distance(player.transform.position, currentClone.transform.position) > separationDistance;

            if (isSeparated)
            {
                if (swapOnExit)
                {
                    Vector3 originalPos = player.transform.position;
                    Quaternion originalRot = player.transform.rotation;

                    playerCharController.enabled = false;
                    player.transform.position = currentClone.transform.position;
                    player.transform.rotation = currentClone.transform.rotation;
                    playerCharController.enabled = true;
                    playerCharController.Move(Vector3.zero);

                    float newYaw = currentClone.transform.eulerAngles.y;
                    float currentPitch = cameraOrbit.GetPitch();
                    cameraOrbit.SetYawPitch(newYaw, currentPitch);

                    CreateSolidClone(originalPos, originalRot);

                    if (swapVFXPrefab != null)
                        Instantiate(swapVFXPrefab, player.transform.position, Quaternion.identity);

                    Debug.Log($"传送本体至 {currentClone.transform.position}，原位置固化");
                }
                else
                {
                    CreateSolidClone(currentClone.transform.position, currentClone.transform.rotation);
                    Debug.Log("分身固化于最终位置");
                }

                Destroy(currentClone);
            }
            else
            {
                Debug.Log("分身未分离，不生成固化实体");
                Destroy(currentClone);
            }
        }
        else if (currentClone != null)
        {
            Destroy(currentClone);
        }

        if (triangleUI != null) Destroy(triangleUI);
        DisablePlayerInput(player, false);
        if (currentClone != null) EnablePlayerInput(currentClone, false);

        currentClone = null;
        isCloneActive = false;
        isTimeStopped = false;
        hasSeparated = false;
        spawnProtectionTimer = 0f;

        if (handleTraceClone && traceCloneManager != null)
        {
            if (traceCloneManager.HasSavedSequence() && shouldGenerateSolid)
            {
                traceCloneManager.SpawnTraceCloneFromSaved();
            }
            else
            {
                traceCloneManager.ClearSavedSequence();
                traceCloneManager.ResumeTraceClone();
            }
        }
    }

    private void SwapWithSolidClone()
    {
        if (currentSolidClone == null) return;

        Debug.Log(">>> 与固化实体交换位置");

        playerCharController.enabled = false;
        Vector3 clonePos = currentSolidClone.transform.position;
        Quaternion cloneRot = currentSolidClone.transform.rotation;

        currentSolidClone.transform.position = player.transform.position;
        currentSolidClone.transform.rotation = player.transform.rotation;

        player.transform.position = clonePos;
        player.transform.rotation = cloneRot;

        playerCharController.enabled = true;
        playerCharController.Move(Vector3.zero);

        float newYaw = cloneRot.eulerAngles.y;
        float currentPitch = cameraOrbit.GetPitch();
        cameraOrbit.SetYawPitch(newYaw, currentPitch);

        if (swapVFXPrefab != null)
            Instantiate(swapVFXPrefab, player.transform.position, Quaternion.identity);
    }

    private void CreateSolidClone(Vector3 position, Quaternion rotation)
    {
        if (currentSolidClone != null) Destroy(currentSolidClone);

        currentSolidClone = Instantiate(playerPrefab, position, rotation);
        currentSolidClone.tag = "SolidClone";
        currentSolidClone.name = "SolidClone";
        ReplaceMaterials(currentSolidClone, solidCloneMaterial);

        var controller = currentSolidClone.GetComponent<PlayerController>();
        if (controller != null) controller.enabled = false;
        var charController = currentSolidClone.GetComponent<CharacterController>();
        if (charController != null) charController.enabled = false;

        var rb = currentSolidClone.GetComponent<Rigidbody>();
        if (rb == null) rb = currentSolidClone.AddComponent<Rigidbody>();
        rb.isKinematic = true;

        if (solidCloneVFXPrefab != null)
        {
            GameObject vfx = Instantiate(solidCloneVFXPrefab, currentSolidClone.transform);
            vfx.transform.localPosition = Vector3.zero;
        }

        Debug.Log($"生成固化实体于 {position}");
    }

    private bool IsCloneInSight(GameObject target)
    {
        Camera cam = Camera.main;
        Vector3 toTarget = target.transform.position - cam.transform.position;
        float distance = toTarget.magnitude;
        const float maxSightDistance = 25f;
        if (distance > maxSightDistance) return false;

        float horizontalHalfAngle = 80f;
        float verticalHalfAngle = 60f;

        Vector3 forward = cam.transform.forward;
        Vector3 direction = toTarget.normalized;
        float angleVer = Vector3.Angle(forward, direction);
        if (angleVer > 90f) return false;

        Vector3 forwardXZ = new Vector3(forward.x, 0, forward.z).normalized;
        Vector3 dirXZ = new Vector3(direction.x, 0, direction.z).normalized;
        float angleHor = Vector3.Angle(forwardXZ, dirXZ);

        if (angleHor <= horizontalHalfAngle && angleVer <= verticalHalfAngle)
            return true;

        Vector3 viewportPos = cam.WorldToViewportPoint(target.transform.position);
        return viewportPos.z > 0 && viewportPos.x >= -0.2f && viewportPos.x <= 1.2f && viewportPos.y >= -0.2f && viewportPos.y <= 1.2f;
    }

    /// <summary>
    /// 检测目标是否被完全遮挡（即所有关键采样点的射线均被非透明障碍物阻挡）
    /// </summary>
    private bool IsCloneOccluded(GameObject target)
    {
        Camera cam = Camera.main;
        if (cam == null) return false;

        // 收集采样点：包围盒中心 + 八个角点
        List<Vector3> samplePoints = new List<Vector3>();

        // 中心点
        samplePoints.Add(target.transform.position);

        // 尝试获取包围盒角点（若目标有 Renderer）
        Renderer rend = target.GetComponentInChildren<Renderer>();
        if (rend != null)
        {
            Bounds bounds = rend.bounds;
            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;

            // 八个角点
            samplePoints.Add(center + new Vector3(extents.x, extents.y, extents.z));
            samplePoints.Add(center + new Vector3(extents.x, extents.y, -extents.z));
            samplePoints.Add(center + new Vector3(extents.x, -extents.y, extents.z));
            samplePoints.Add(center + new Vector3(extents.x, -extents.y, -extents.z));
            samplePoints.Add(center + new Vector3(-extents.x, extents.y, extents.z));
            samplePoints.Add(center + new Vector3(-extents.x, extents.y, -extents.z));
            samplePoints.Add(center + new Vector3(-extents.x, -extents.y, extents.z));
            samplePoints.Add(center + new Vector3(-extents.x, -extents.y, -extents.z));
        }
        else
        {
            // 若无渲染器，简单添加目标位置周围几个点
            samplePoints.Add(target.transform.position + Vector3.up * 0.5f);
            samplePoints.Add(target.transform.position + Vector3.down * 0.5f);
            samplePoints.Add(target.transform.position + Vector3.left * 0.5f);
            samplePoints.Add(target.transform.position + Vector3.right * 0.5f);
            samplePoints.Add(target.transform.position + Vector3.forward * 0.5f);
            samplePoints.Add(target.transform.position + Vector3.back * 0.5f);
        }

        // 检查每个采样点：只要有一个点未被阻挡，就认为未完全遮挡
        foreach (Vector3 point in samplePoints)
        {
            Vector3 dir = point - cam.transform.position;
            float distance = dir.magnitude;
            if (distance < 0.2f) continue; // 太近忽略

            if (!Physics.Raycast(cam.transform.position, dir, out RaycastHit hit, distance, occlusionMask))
            {
                // 没有击中任何东西 -> 可见
                return false;
            }
            else
            {
                // 击中了物体，检查是否属于豁免对象
                if (!IsHitConsideredOcclusion(hit, target))
                    return false; // 击中的是玩家/分身/透明层，视为未遮挡
            }
        }

        // 所有采样点都被阻挡
        return true;
    }

    /// <summary>
    /// 判断射线击中的物体是否应被视为遮挡（非豁免对象）
    /// </summary>
    private bool IsHitConsideredOcclusion(RaycastHit hit, GameObject target)
    {
        Transform hitRoot = hit.transform.root;
        // 玩家本体不阻挡
        if (hitRoot == player.transform) return false;
        // 分身自身不阻挡
        if (hitRoot == target.transform) return false;
        // 透明层物体不阻挡
        if (hit.transform.gameObject.layer == LayerMask.NameToLayer("Transparent")) return false;

        return true; // 其他物体视为遮挡
    }

    private bool IsCloneInSight() => currentClone != null && IsCloneInSight(currentClone);
    private bool IsCloneOccluded() => currentClone != null && IsCloneOccluded(currentClone);

    private void ReplaceMaterials(GameObject obj, Material newMat)
    {
        var renderers = obj.GetComponentsInChildren<Renderer>();
        foreach (var rend in renderers)
            rend.material = newMat;
    }

    private void IgnoreCollisionBetween(GameObject a, GameObject b, bool ignore)
    {
        var collidersA = a.GetComponents<Collider>();
        var collidersB = b.GetComponents<Collider>();
        foreach (var ca in collidersA)
            foreach (var cb in collidersB)
                Physics.IgnoreCollision(ca, cb, ignore);
    }

    private void IgnoreCollisionWithLayer(GameObject obj, LayerMask layerMask, bool ignore)
    {
        Collider[] objColliders = obj.GetComponentsInChildren<Collider>();
        // 查找场景中所有位于指定层的物体
        var allObjects = FindObjectsOfType<GameObject>();
        foreach (var other in allObjects)
        {
            if ((layerMask.value & (1 << other.layer)) != 0)
            {
                Collider[] otherColliders = other.GetComponentsInChildren<Collider>();
                foreach (var ca in objColliders)
                {
                    foreach (var cb in otherColliders)
                    {
                        Physics.IgnoreCollision(ca, cb, ignore);
                    }
                }
            }
        }
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

    private void CreateTriangleUI()
    {
        if (triangleUIPrefab == null) return;

        triangleUI = Instantiate(triangleUIPrefab);
        triangleText = triangleUI.GetComponentInChildren<TMP_Text>();
        if (triangleText != null)
            triangleText.color = unselectedColor;

        triangleUI.transform.SetParent(currentClone.transform);
        triangleUI.transform.localPosition = new Vector3(0, 1.5f, 0);
        triangleUI.transform.localScale = Vector3.one * 0.2f;
    }

    public void OnCloneMoved(Vector3 oldPos, Vector3 newPos)
    {
        if (!isCloneActive || hasSeparated) return;
        if (Vector3.Distance(oldPos, newPos) > separationDistance)
        {
            hasSeparated = true;
            IgnoreCollisionBetween(player, currentClone, false);
            Debug.Log("分身已分离");
        }
    }

    public bool IsTimeStopped() => isTimeStopped;

    private void OnDestroy()
    {
        if (currentClone != null) Destroy(currentClone);
        if (currentSolidClone != null) Destroy(currentSolidClone);
    }

    public void ForceExitTimeStop(bool shouldGenerateSolid, bool handleTraceClone = true)
    {
        if (isTimeStopped)
            ExitTimeStop(shouldGenerateSolid, swapOnExit: false, handleTraceClone: handleTraceClone);
    }

    public void ActivateVisionCloneFromTrace()
    {
        ActivateVisionClone();
    }
}