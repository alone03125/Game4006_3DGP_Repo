using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using System.Collections.Generic;
using UnityEngine.UI;

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
    public GameObject swapVFXPrefab;              // 传送特效（自动销毁，跟随摄像机）
    public GameObject solidCloneVFXPrefab;        // 固化实体标记特效（跟随实体）

    [Header("Detection")]
    public float separationDistance = 0.1f;
    public float proximityRadius = 2.5f;
    public LayerMask occlusionMask = -1;
    public LayerMask transparentLayers;           // 透明层（分身可穿越且不阻挡视线）

<<<<<<< HEAD
    [Header("Disappear Warning")]
    public Renderer warningRenderer;              // 可选：3D Renderer 预警（不推荐）
    public Image warningUIImage;                  // 推荐：UI Image 全屏预警
    public string warningMaterialProperty = "_Alpha";
    public AnimationCurve warningIntensityCurve = AnimationCurve.Linear(0, 0, 1, 1);
    [Range(0, 1)] public float warningEdgeThreshold = 0.2f;

    private const float SWAP_VFX_DURATION = 2.5f;
    private const float DISAPPEAR_VFX_DURATION = 2.0f;
=======
    private const float SWAP_VFX_DURATION = 2.5f; // 传送特效持续时间
>>>>>>> parent of 393d76b (shader test v1.0)

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
            // 非时停下，检测固化实体是否应消失
            if (currentSolidClone != null)
            {
                if (!IsCloneInSight(currentSolidClone) || IsCloneOccluded(currentSolidClone))
                {
                    Debug.Log("固化实体离开视野或被完全遮挡，销毁");
                    Destroy(currentSolidClone);
                    currentSolidClone = null;
                }
            }
            return;
        }

        // 时停期间相机锁定
        if (cameraLocked && player != null)
        {
            Camera.main.transform.position = player.transform.position + cameraOrbit.headOffset;
            Camera.main.transform.rotation = lockedCameraRotation;
        }

        // 视界分身的视野/遮挡检测（生成保护期内跳过）
        if (isCloneActive && currentClone != null && spawnProtectionTimer <= 0f)
        {
            float distToPlayer = Vector3.Distance(player.transform.position, currentClone.transform.position);
            bool isInProximity = distToPlayer <= proximityRadius;

            if (!isInProximity && (!IsCloneInSight(currentClone) || IsCloneOccluded(currentClone)))
            {
                Debug.Log($"分身距离本体 {distToPlayer:F2} > {proximityRadius} 且丢失视野或被完全遮挡，强制退出时停");
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
<<<<<<< HEAD
        float targetAlpha = 0f;

        // 仅当视界分身激活且不在生成保护期内才计算预警
        if (isCloneActive && currentClone != null && spawnProtectionTimer <= 0f)
        {
            float occlusionRatio = GetOcclusionRatio(currentClone);
            float edgeDistanceRatio = GetEdgeDistanceRatio(currentClone);
            float warningFactor = Mathf.Max(occlusionRatio, edgeDistanceRatio);
            targetAlpha = warningIntensityCurve.Evaluate(warningFactor);
        }

        currentWarningAlpha = Mathf.Lerp(currentWarningAlpha, targetAlpha, Time.deltaTime * 5f);

        // 应用到材质或 UI Image
        if (warningMaterialInstance != null)
        {
            warningMaterialInstance.SetFloat(warningMaterialProperty, currentWarningAlpha);
        }
        if (warningUIImage != null)
        {
            Color c = warningUIImage.color;
            c.a = currentWarningAlpha;
            warningUIImage.color = c;
        }
    }

    private float GetOcclusionRatio(GameObject target)
    {
        Camera cam = Camera.main;
        if (cam == null) return 0f;

        List<Vector3> samplePoints = GenerateSamplePoints(target);
        int occludedCount = 0;

        foreach (Vector3 point in samplePoints)
        {
            Vector3 dir = point - cam.transform.position;
            float distance = dir.magnitude;
            if (distance < 0.2f) continue;

            if (Physics.Raycast(cam.transform.position, dir, out RaycastHit hit, distance, occlusionMask))
            {
                if (IsHitConsideredOcclusion(hit, target))
                    occludedCount++;
            }
        }

        return samplePoints.Count > 0 ? (float)occludedCount / samplePoints.Count : 0f;
    }

    private float GetEdgeDistanceRatio(GameObject target)
    {
        Camera cam = Camera.main;
        if (cam == null) return 0f;

        Vector3 viewportPos = cam.WorldToViewportPoint(target.transform.position);
        if (viewportPos.z <= 0) return 1f;

        float distToLeft = viewportPos.x;
        float distToRight = 1f - viewportPos.x;
        float distToBottom = viewportPos.y;
        float distToTop = 1f - viewportPos.y;

        float minDist = Mathf.Min(distToLeft, distToRight, distToBottom, distToTop);
        float edgeFactor = Mathf.Clamp01(1f - (minDist / warningEdgeThreshold));
        return edgeFactor;
    }

    private List<Vector3> GenerateSamplePoints(GameObject target)
    {
        List<Vector3> points = new List<Vector3>();
        float height = 2.0f;
        float radius = 0.5f;
        Vector3 center = target.transform.position;

        float[] yOffsets = { -0.5f, 0f, 0.5f };
        int anglesCount = 4;

        foreach (float yOff in yOffsets)
        {
            Vector3 layerCenter = center + Vector3.up * yOff;
            for (int i = 0; i < anglesCount; i++)
            {
                float angle = i * Mathf.PI * 2f / anglesCount;
                Vector3 offset = new Vector3(Mathf.Sin(angle) * radius, 0, Mathf.Cos(angle) * radius);
                points.Add(layerCenter + offset);
            }
        }

        points.Add(center);
        points.Add(center + Vector3.up * height * 0.5f);
        points.Add(center + Vector3.down * height * 0.5f);

        return points;
    }

    private bool IsCloneInSight(GameObject target)
    {
        Camera cam = Camera.main;
        if (cam == null) return false;

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

    private bool IsCloneOccluded(GameObject target)
    {
        Camera cam = Camera.main;
        if (cam == null) return false;

        List<Vector3> samplePoints = GenerateSamplePoints(target);

        foreach (Vector3 point in samplePoints)
        {
            Vector3 dir = point - cam.transform.position;
            float distance = dir.magnitude;
            if (distance < 0.2f) continue;

            if (!Physics.Raycast(cam.transform.position, dir, out RaycastHit hit, distance, occlusionMask))
                return false;

            if (!IsHitConsideredOcclusion(hit, target))
                return false;
        }
        return true;
    }

    private bool IsHitConsideredOcclusion(RaycastHit hit, GameObject target)
    {
        Transform hitRoot = hit.transform.root;
        if (hitRoot == player.transform || hitRoot == target.transform)
            return false;

        if (((1 << hit.transform.gameObject.layer) & transparentLayers) != 0)
            return false;

        return true;
    }

    private void SpawnDisappearVFX()
    {
        if (disappearVFXPrefab == null) return;
        Camera cam = Camera.main;
        if (cam == null) return;

        GameObject vfx = Instantiate(disappearVFXPrefab, cam.transform);
        vfx.transform.localPosition = new Vector3(0, 0, 1.5f);
        vfx.transform.localRotation = Quaternion.identity;
        Destroy(vfx, DISAPPEAR_VFX_DURATION);
    }

    // ---------- 输入回调 ----------
    public void OnVisionActivate(InputValue value)
    {
        if (!value.isPressed) return;
        Debug.Log($"[CloneManager] VisionActivate (Q) pressed, isTimeStopped={isTimeStopped}");

=======
>>>>>>> parent of 393d76b (shader test v1.0)
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
            // 时停内按下Tab：传送本体并固化原位置，然后退出时停
            ExitTimeStop(true, swapOnExit: true);
        }
        else if (!isTimeStopped && currentSolidClone != null)
        {
            // 非时停下与固化实体交换位置
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

        // 忽略与玩家的碰撞
        IgnoreCollisionBetween(player, currentClone, true);

        // 忽略与透明层物体的碰撞
        IgnoreCollisionWithTransparentLayers(currentClone, true);

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

                    // 传送特效：生成在摄像机前并跟随摄像机
                    SpawnSwapVFX();

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

        // 传送特效
        SpawnSwapVFX();
    }

    private void SpawnSwapVFX()
    {
        if (swapVFXPrefab == null) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        GameObject vfx = Instantiate(swapVFXPrefab, cam.transform);
        vfx.transform.localPosition = new Vector3(0, 0, 1.5f); // 摄像机前方1.5单位
        vfx.transform.localRotation = Quaternion.identity;
        Destroy(vfx, SWAP_VFX_DURATION);
    }

    private void CreateSolidClone(Vector3 position, Quaternion rotation)
    {
        if (currentSolidClone != null) Destroy(currentSolidClone);

        currentSolidClone = Instantiate(playerPrefab, position, rotation);
        currentSolidClone.tag = "SolidClone";
        currentSolidClone.name = "SolidClone";
        ReplaceMaterials(currentSolidClone, solidCloneMaterial);


        //Disable input and interaction
        var solidInput = currentSolidClone.GetComponent<PlayerInput>(); //B
        if (solidInput != null) //B
        {
            solidInput.enabled = false;
        }

        var solidInteraction = currentSolidClone.GetComponent<PlayerInteraction>();
        if (solidInteraction != null) //B
        {
            solidInteraction.enabled = false;
        }

        var controller = currentSolidClone.GetComponent<PlayerController>();
        if (controller != null) controller.enabled = false;
        var charController = currentSolidClone.GetComponent<CharacterController>();
        if (charController != null) charController.enabled = false;

        var rb = currentSolidClone.GetComponent<Rigidbody>();
        if (rb == null) rb = currentSolidClone.AddComponent<Rigidbody>();
        rb.isKinematic = true;

        // 固化实体标记特效（跟随实体）
        if (solidCloneVFXPrefab != null)
        {
            GameObject vfx = Instantiate(solidCloneVFXPrefab, currentSolidClone.transform);
            vfx.transform.localPosition = Vector3.zero;
        }

        Debug.Log($"生成固化实体于 {position}");
    }

    // ---------- 视野与遮挡检测 ----------
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

    private bool IsCloneOccluded(GameObject target)
    {
        Camera cam = Camera.main;
        if (cam == null) return false;

        List<Vector3> samplePoints = new List<Vector3> { target.transform.position };

        Renderer rend = target.GetComponentInChildren<Renderer>();
        if (rend != null)
        {
            Bounds bounds = rend.bounds;
            Vector3 center = bounds.center;
            Vector3 ext = bounds.extents;
            samplePoints.Add(center + new Vector3(ext.x, ext.y, ext.z));
            samplePoints.Add(center + new Vector3(ext.x, ext.y, -ext.z));
            samplePoints.Add(center + new Vector3(ext.x, -ext.y, ext.z));
            samplePoints.Add(center + new Vector3(ext.x, -ext.y, -ext.z));
            samplePoints.Add(center + new Vector3(-ext.x, ext.y, ext.z));
            samplePoints.Add(center + new Vector3(-ext.x, ext.y, -ext.z));
            samplePoints.Add(center + new Vector3(-ext.x, -ext.y, ext.z));
            samplePoints.Add(center + new Vector3(-ext.x, -ext.y, -ext.z));
        }
        else
        {
            samplePoints.Add(target.transform.position + Vector3.up * 0.5f);
            samplePoints.Add(target.transform.position + Vector3.down * 0.5f);
            samplePoints.Add(target.transform.position + Vector3.left * 0.5f);
            samplePoints.Add(target.transform.position + Vector3.right * 0.5f);
            samplePoints.Add(target.transform.position + Vector3.forward * 0.5f);
            samplePoints.Add(target.transform.position + Vector3.back * 0.5f);
        }

        foreach (Vector3 point in samplePoints)
        {
            Vector3 dir = point - cam.transform.position;
            float distance = dir.magnitude;
            if (distance < 0.2f) continue;

            if (!Physics.Raycast(cam.transform.position, dir, out RaycastHit hit, distance, occlusionMask))
                return false;

            if (!IsHitConsideredOcclusion(hit, target))
                return false;
        }

        return true;
    }

    private bool IsHitConsideredOcclusion(RaycastHit hit, GameObject target)
    {
        Transform hitRoot = hit.transform.root;
        if (hitRoot == player.transform || hitRoot == target.transform)
            return false;

        // 使用配置的透明层判断
        if (((1 << hit.transform.gameObject.layer) & transparentLayers) != 0)
            return false;

        return true;
    }

    private void IgnoreCollisionWithTransparentLayers(GameObject obj, bool ignore)
    {
        if (transparentLayers == 0) return;

        // 获取所有透明层上的碰撞体
        // 注意：此方法在运行时查找场景中所有游戏对象，若透明物体较多可优化为通过物理设置 IgnoreLayerCollision
        GameObject[] allObjects = FindObjectsOfType<GameObject>();
        foreach (GameObject other in allObjects)
        {
            if (((1 << other.layer) & transparentLayers) != 0)
            {
                IgnoreCollisionBetween(obj, other, ignore);
            }
        }
    }

    private bool IsCloneInSight() => currentClone != null && IsCloneInSight(currentClone);
    private bool IsCloneOccluded() => currentClone != null && IsCloneOccluded(currentClone);

    // ---------- 辅助方法 ----------
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