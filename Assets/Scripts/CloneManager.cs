using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class CloneManager : MonoBehaviour
{
    [Header("References")]
    public GameObject playerPrefab;
    public Material visionCloneMaterial;
    public Material solidCloneMaterial;

    [Header("UI")]
    public GameObject triangleUIPrefab;
    public Color selectedColor = Color.green;
    public Color unselectedColor = Color.yellow;

    [Header("Detection")]
    public float separationDistance = 0.1f;      // 碰撞恢复阈值
    public float proximityRadius = 2.5f;          // 安全半径：分身在此距离内永不销毁
    public LayerMask occlusionMask = -1;

    // 状态
    private bool isTimeStopped = false;
    private bool cameraLocked = false;
    private bool isCloneActive = false;
    private bool hasSeparated = false;

    private GameObject currentClone;
    private GameObject currentSolidClone;
    private GameObject selectedCarrier;
    private bool isCloneSelected = false;

    private GameObject triangleUI;
    private TMP_Text triangleText;

    private PlayerController playerController;
    private SimpleCameraOrbit cameraOrbit;
    private GameObject player;
    private CharacterController playerCharController;

    private Quaternion lockedCameraRotation;
    private InputAction qAction;
    private InputAction tabAction;

    // 保护期（防止生成瞬间被误销毁）
    private float spawnProtectionTimer = 0f;
    private const float SPAWN_PROTECTION_DURATION = 0.5f;

    private void Awake()
    {
        player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) Debug.LogError("未找到Player标签的游戏对象");
        playerController = player.GetComponent<PlayerController>();
        playerCharController = player.GetComponent<CharacterController>();
        cameraOrbit = Camera.main.GetComponent<SimpleCameraOrbit>();

        var inputActions = new InputActionMap();
        qAction = inputActions.AddAction("Q", binding: "<Keyboard>/q");
        tabAction = inputActions.AddAction("Tab", binding: "<Keyboard>/tab");
        qAction.performed += OnQPerformed;
        tabAction.performed += OnTabPerformed;
        inputActions.Enable();
    }

    private void Update()
    {
        if (!isTimeStopped) return;

        // 相机锁定跟随本体
        if (cameraLocked && player != null)
        {
            Camera.main.transform.position = player.transform.position + cameraOrbit.headOffset;
            Camera.main.transform.rotation = lockedCameraRotation;
        }

        // 视野/遮挡检测：仅在保护期过后执行
        if (isCloneActive && currentClone != null && spawnProtectionTimer <= 0f)
        {
            float distToPlayer = Vector3.Distance(player.transform.position, currentClone.transform.position);
            bool isInProximity = distToPlayer <= proximityRadius;

            if (!isInProximity && (!IsCloneInSight() || IsCloneOccluded()))
            {
                Debug.Log($"分身距离本体 {distToPlayer:F2} > {proximityRadius} 且丢失视野或被遮挡，强制退出时停");
                ExitTimeStop(false);
            }
        }
        else if (spawnProtectionTimer > 0f)
        {
            spawnProtectionTimer -= Time.deltaTime;
            if (spawnProtectionTimer <= 0f)
                Debug.Log("分身保护期结束，开始检测视野/遮挡（安全半径内除外）");
        }
    }

    private void OnQPerformed(InputAction.CallbackContext ctx)
    {
        if (!isTimeStopped)
            ActivateVisionClone();
        else
            ExitTimeStop(true);
    }

    private void OnTabPerformed(InputAction.CallbackContext ctx)
    {
        if (isTimeStopped && isCloneActive)
            ToggleCarrierSelection();
    }

    private void ActivateVisionClone()
    {
        if (isCloneActive) return;

        if (currentSolidClone != null) Destroy(currentSolidClone);

        Debug.Log(">>> 进入时停（调试）");
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
        hasSeparated = false;

        cloneController.OnPositionChanged += (oldPos, newPos) => {
            OnCloneMoved(oldPos, newPos);
        };

        CreateTriangleUI();
        isCloneSelected = false;
        selectedCarrier = player;
        UpdateTriangleUIColor();

        isCloneActive = true;
        isTimeStopped = true;

        DisablePlayerInput(player, true);
        EnablePlayerInput(currentClone, true);

        spawnProtectionTimer = SPAWN_PROTECTION_DURATION;
        Debug.Log("分身已生成，保护期内不检测视野");
    }

    private void ExitTimeStop(bool shouldGenerateSolid)
    {
        if (!isTimeStopped) return;

        Debug.Log(">>> 退出时停（调试）");
        cameraLocked = false;
        cameraOrbit.SetCameraLock(false, Quaternion.identity);

        playerController.canMove = true;
        playerController.freezeGravity = false;

        if (shouldGenerateSolid && currentClone != null)
        {
            bool isSeparated = hasSeparated || Vector3.Distance(player.transform.position, currentClone.transform.position) > separationDistance;

            if (isSeparated)
            {
                if (!isCloneSelected) // 保持本体为载体
                {
                    CreateSolidClone(currentClone.transform.position, currentClone.transform.rotation);
                    Destroy(currentClone);
                    Debug.Log("保持本体，在分身位置生成不动实体");
                }
                else // 选择分身为新载体：传送本体到分身位置
                {
                    playerCharController.enabled = false;
                    Vector3 originalPos = player.transform.position;
                    Quaternion originalRot = player.transform.rotation;

                    player.transform.position = currentClone.transform.position;
                    player.transform.rotation = currentClone.transform.rotation;

                    playerCharController.enabled = true;
                    playerCharController.Move(Vector3.zero);

                    float newYaw = currentClone.transform.eulerAngles.y;
                    float currentPitch = cameraOrbit.GetPitch();
                    cameraOrbit.SetYawPitch(newYaw, currentPitch);

                    CreateSolidClone(originalPos, originalRot);

                    Destroy(currentClone);
                    Debug.Log($"选择分身，本体从 {originalPos} 传送至 {player.transform.position}");
                }
            }
            else
            {
                Debug.Log("分身未与本体脱离，不生成实体，直接销毁分身");
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
        selectedCarrier = null;
        spawnProtectionTimer = 0f;
    }

    private void ToggleCarrierSelection()
    {
        isCloneSelected = !isCloneSelected;
        selectedCarrier = isCloneSelected ? currentClone : player;
        UpdateTriangleUIColor();
        Debug.Log($"载体切换：{(isCloneSelected ? "分身" : "本体")}");
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

        Debug.Log($"生成不动实体于 {position}");
    }

    private bool IsCloneInSight()
    {
        Camera cam = Camera.main;
        Vector3 toClone = currentClone.transform.position - cam.transform.position;
        float distance = toClone.magnitude;
        const float maxSightDistance = 25f;
        if (distance > maxSightDistance) return false;

        float horizontalHalfAngle = 80f;
        float verticalHalfAngle = 60f;

        Vector3 forward = cam.transform.forward;
        Vector3 direction = toClone.normalized;
        float angleVer = Vector3.Angle(forward, direction);
        if (angleVer > 90f) return false;

        Vector3 forwardXZ = new Vector3(forward.x, 0, forward.z).normalized;
        Vector3 dirXZ = new Vector3(direction.x, 0, direction.z).normalized;
        float angleHor = Vector3.Angle(forwardXZ, dirXZ);

        if (angleHor <= horizontalHalfAngle && angleVer <= verticalHalfAngle)
            return true;

        Vector3 viewportPos = cam.WorldToViewportPoint(currentClone.transform.position);
        if (viewportPos.z > 0 && viewportPos.x >= -0.2f && viewportPos.x <= 1.2f && viewportPos.y >= -0.2f && viewportPos.y <= 1.2f)
            return true;

        return false;
    }

    private bool IsCloneOccluded()
    {
        Camera cam = Camera.main;
        Vector3 origin = cam.transform.position;
        Vector3 dir = currentClone.transform.position - origin;
        float distance = dir.magnitude;
        if (distance < 0.2f) return false;

        if (Physics.Raycast(origin, dir, out RaycastHit hit, distance, occlusionMask))
        {
            Transform hitRoot = hit.transform.root;
            if (hitRoot != player.transform && hitRoot != currentClone.transform)
                return true;
        }
        return false;
    }

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
        if (triangleUIPrefab == null)
        {
            Debug.LogWarning("未指定倒三角UI预制体，使用默认创建");
            GameObject canvasObj = new GameObject("TriangleUI");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            RectTransform rect = canvasObj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0.5f, 0.5f);
            triangleUI = canvasObj;

            var textObj = new GameObject("Text");
            textObj.transform.SetParent(canvasObj.transform);
            triangleText = textObj.AddComponent<TextMeshProUGUI>();
            triangleText.text = "▼";
            triangleText.fontSize = 4;
            triangleText.alignment = TextAlignmentOptions.Center;
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.sizeDelta = new Vector2(1, 1);
        }
        else
        {
            triangleUI = Instantiate(triangleUIPrefab);
            triangleText = triangleUI.GetComponentInChildren<TMP_Text>();
        }

        triangleUI.transform.SetParent(currentClone.transform);
        triangleUI.transform.localPosition = new Vector3(0, 1.5f, 0);
        triangleUI.transform.localScale = Vector3.one * 0.2f;
        UpdateTriangleUIColor();
    }

    private void UpdateTriangleUIColor()
    {
        if (triangleText != null)
            triangleText.color = isCloneSelected ? selectedColor : unselectedColor;
    }

    public void OnCloneMoved(Vector3 oldPos, Vector3 newPos)
    {
        if (!isCloneActive || hasSeparated) return;
        if (Vector3.Distance(oldPos, newPos) > separationDistance)
        {
            hasSeparated = true;
            IgnoreCollisionBetween(player, currentClone, false);
            Debug.Log("分身已脱离本体，碰撞恢复");
        }
    }

    public bool IsTimeStopped() => isTimeStopped;

    private void OnDestroy()
    {
        if (currentClone != null) Destroy(currentClone);
        if (currentSolidClone != null) Destroy(currentSolidClone);
    }
}