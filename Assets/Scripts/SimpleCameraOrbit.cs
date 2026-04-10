using UnityEngine;
using UnityEngine.InputSystem;

public class SimpleCameraOrbit : MonoBehaviour
{
    public Transform target;
    public Vector3 headOffset = new Vector3(0, 1.2f, 0);
    public float lookSensitivity = 8f;

    // 抖动参数
    public bool enableShake = true;
    public float horizontalShakeAmplitude = 0.25f;
    public float verticalShakeAmplitude = 0.15f;
    public float maxShakeFrequency = 5f;
    public float shakeSmoothTime = 0.2f;

    // 跳跃视角偏移参数
    public bool enableJumpPitch = true;
    public float maxJumpPitchOffset = 15f;
    public float pitchSmoothTime = 0.2f;
    public float landReboundStrength = 0.5f;
    public float landReboundDuration = 0.2f;
    public float airTransitionSmoothTime = 0.1f;

    // 控制状态
    public bool controlsEnabled = true;

    // ========== 新增：相机锁定扩展 ==========
    private bool isCameraLocked = false;
    private Quaternion lockedRotation;
    private Vector3 lockedPosition;       // 锁定时相机跟随的位置（本体位置）
    private bool followTargetWhenLocked = true; // 锁定时是否跟随目标移动

    // 原有私有变量
    private float yaw = 0f;
    private float pitch = 0f;
    private float pitchOffsetRaw = 0f;
    private float pitchOffsetVelocity = 0f;
    private float landReboundTimer = 0f;
    private float landReboundOffset = 0f;
    private float airTransitionWeight = 0f;
    private float airTransitionVelocity = 0f;

    private PlayerController playerController;
    private float shakePhase = 0f;
    private float currentShakeIntensity = 0f;
    private bool wasGrounded = true;

    private Vector3 defaultHeadOffset;

    void Start()
    {
        if (target == null)
            target = GameObject.Find("Player")?.transform;

        playerController = target?.GetComponent<PlayerController>();
        ApplyControlsState();

        defaultHeadOffset = headOffset;
    }
    public void SetControlsEnabled(bool enabled)
    {
        controlsEnabled = enabled;
        ApplyControlsState();
    }

    public void SetCameraOffset(Vector3 newOffset)
    {
        headOffset = newOffset;
    }

    public void ResetCameraOffset()
    {
        headOffset = defaultHeadOffset;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            controlsEnabled = !controlsEnabled;
            ApplyControlsState();
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        // 鼠标视角旋转（仅当相机未锁定时生效）
        if (controlsEnabled && Mouse.current != null && !isCameraLocked)
        {
            Vector2 delta = Mouse.current.delta.ReadValue() * lookSensitivity * 0.01f;
            yaw += delta.x;
            pitch -= delta.y;
            pitch = Mathf.Clamp(pitch, -80f, 80f);
        }

        bool isGrounded = playerController != null && playerController.Controller.isGrounded;

        // 更新过渡权重（用于俯仰偏移混合）
        float targetWeight = isGrounded ? 0f : 1f;
        airTransitionWeight = Mathf.SmoothDamp(airTransitionWeight, targetWeight, ref airTransitionVelocity, airTransitionSmoothTime);

        // 1. 抖动强度平滑
        if (enableShake && playerController != null)
        {
            float targetIntensity = 0f;
            if (isGrounded)
            {
                float speed = playerController.GetCurrentHorizontalSpeed();
                float maxSpeed = playerController.CurrentMaxSpeed;
                targetIntensity = Mathf.Clamp01(speed / maxSpeed);
            }
            currentShakeIntensity = Mathf.Lerp(currentShakeIntensity, targetIntensity, Time.deltaTime / shakeSmoothTime);
        }
        else
        {
            currentShakeIntensity = 0f;
        }

        // 2. 计算抖动偏移
        Vector3 shakeOffset = Vector3.zero;
        if (controlsEnabled && currentShakeIntensity > 0.01f)
        {
            float freq = Mathf.Lerp(0.5f, maxShakeFrequency, currentShakeIntensity);
            float hAmp = horizontalShakeAmplitude * currentShakeIntensity;
            float vAmp = verticalShakeAmplitude * currentShakeIntensity;
            shakePhase += Time.deltaTime * freq;
            float xShake = Mathf.Sin(shakePhase) * hAmp;
            float yShake = Mathf.Sin(shakePhase * 2f) * vAmp;
            shakeOffset = transform.right * xShake + transform.up * yShake;
        }
        else
        {
            shakePhase = 0f;
        }

        // 3. 原始俯仰偏移
        UpdatePitchOffsetRaw(isGrounded);

        // 4. 最终俯仰偏移
        float finalPitchOffset = pitchOffsetRaw * airTransitionWeight;

        // 5. 最终旋转和位置
        Quaternion targetRot;
        if (isCameraLocked)
            targetRot = lockedRotation;
        else
            targetRot = Quaternion.Euler(pitch + finalPitchOffset + landReboundOffset, yaw, 0);

        transform.rotation = targetRot;

        // 位置：锁定时可能强制跟随目标（本体），否则正常跟随target+偏移
        if (isCameraLocked && followTargetWhenLocked && target != null)
            transform.position = target.position + headOffset + shakeOffset;
        else if (!isCameraLocked)
            transform.position = target.position + headOffset + shakeOffset;

        // 6. 落地回弹逻辑
        if (!wasGrounded && isGrounded)
        {
            float verticalSpeed = playerController?.GetCurrentVerticalSpeed() ?? 0f;
            if (verticalSpeed < -2f)
            {
                float rebound = Mathf.Abs(verticalSpeed) / 15f * landReboundStrength;
                rebound = Mathf.Clamp(rebound, 0f, maxJumpPitchOffset * 0.5f);
                landReboundOffset = rebound;
                landReboundTimer = landReboundDuration;
            }
        }
        wasGrounded = isGrounded;

        if (landReboundTimer > 0f)
        {
            landReboundTimer -= Time.deltaTime;
            if (landReboundTimer <= 0f)
                landReboundOffset = 0f;
            else
                landReboundOffset = Mathf.Lerp(0f, landReboundOffset, landReboundTimer / landReboundDuration);
        }
    }

    private void UpdatePitchOffsetRaw(bool isGrounded)
    {
        if (!enableJumpPitch || playerController == null)
        {
            pitchOffsetRaw = 0f;
            return;
        }

        float verticalSpeed = playerController.GetCurrentVerticalSpeed();
        float targetOffset = 0f;
        if (!isGrounded)
        {
            float speedRatio = Mathf.Clamp01(Mathf.Abs(verticalSpeed) / 15f);
            if (verticalSpeed > 0f)
                targetOffset = -maxJumpPitchOffset * speedRatio;
            else if (verticalSpeed < 0f)
                targetOffset = maxJumpPitchOffset * speedRatio;
        }
        pitchOffsetRaw = Mathf.SmoothDamp(pitchOffsetRaw, targetOffset, ref pitchOffsetVelocity, pitchSmoothTime);
    }

    private void ApplyControlsState()
    {
        if (controlsEnabled)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (playerController != null)
            playerController.canMove = controlsEnabled;
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        controlsEnabled = hasFocus;
        ApplyControlsState();
    }

    // ========== 新增公共方法（供 CloneManager 调用） ==========
    /// <summary>
    /// 锁定相机旋转和位置跟随模式
    /// </summary>
    /// <param name="locked">是否锁定</param>
    /// <param name="fixedRotation">锁定的旋转</param>
    /// <param name="followTarget">锁定时是否继续跟随目标位置（时停期间应跟随本体）</param>
    public void SetCameraLock(bool locked, Quaternion fixedRotation, bool followTarget = true)
    {
        isCameraLocked = locked;
        if (locked)
        {
            lockedRotation = fixedRotation;
            followTargetWhenLocked = followTarget;
        }
    }

    /// <summary>
    /// 强制设置相机的偏航角和俯仰角（用于继承分身朝向）
    /// </summary>
    public void SetYawPitch(float newYaw, float newPitch)
    {
        yaw = newYaw;
        pitch = Mathf.Clamp(newPitch, -80f, 80f);
    }

    /// <summary>
    /// 获取当前俯仰角
    /// </summary>
    public float GetPitch() => pitch;

    /// <summary>
    /// 获取当前偏航角
    /// </summary>
    public float GetYaw() => yaw;
}