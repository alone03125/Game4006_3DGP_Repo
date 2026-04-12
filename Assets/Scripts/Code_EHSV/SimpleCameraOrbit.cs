using UnityEngine;
using UnityEngine.InputSystem;

public class SimpleCameraOrbit : MonoBehaviour
{
    public Transform target;
    public Vector3 headOffset = new Vector3(0, 1.2f, 0);
    public float lookSensitivity = 8f;
    public bool invertY = false;

    // ��������
    public bool enableShake = true;
    public float shakeIntensityMultiplier = 1f;
    public float horizontalShakeAmplitude = 0.25f;
    public float verticalShakeAmplitude = 0.15f;
    public float maxShakeFrequency = 5f;
    public float shakeSmoothTime = 0.2f;

    // ��Ծ�ӽ�ƫ�Ʋ���
    public bool enableJumpPitch = true;
    public float maxJumpPitchOffset = 15f;
    public float pitchSmoothTime = 0.2f;
    public float landReboundStrength = 0.5f;
    public float landReboundDuration = 0.2f;
    public float airTransitionSmoothTime = 0.1f;

    // ����״̬
    public bool controlsEnabled = true;

    // ���������չ
    private bool isCameraLocked = false;
    private Quaternion lockedRotation;
    private bool followTargetWhenLocked = true;

    // 自由第三人称模式（用于循迹分身）
    private bool freeLookMode = false;
    private float freeLookDistance = 5f;

    // 视角切换平滑过渡
    [Header("视角过渡")]
    public float viewTransitionDuration = 0.4f;
    public AnimationCurve viewTransitionCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 3f),
        new Keyframe(1f, 1f, 0f, 0f)
    );
    [Tooltip("How quickly camera shake blends during view transitions (0=instant, 1=slow)")]
    public float shakeTransitionBlendSpeed = 4f;
    private bool isTransitioning = false;
    private float transitionTimer = 0f;
    private float transitionDuration = 0.4f;
    private Vector3 transitionStartPos;
    private Quaternion transitionStartRot;
    private bool transitionToFreeLook = false;
    private float transitionShakeWeight = 1f; // blends shake during transitions

    // �ڲ�����
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
        // ESC handling moved to PauseManager
    }

    void LateUpdate()
    {
        if (target == null) return;

        // ===== 过渡动画 =====
        if (isTransitioning)
        {
            transitionTimer += Time.unscaledDeltaTime;
            float rawT = Mathf.Clamp01(transitionTimer / transitionDuration);
            // 使用Inspector可编辑的动画曲线
            float t = viewTransitionCurve.Evaluate(rawT);

            // Blend shake weight: fade out when transitioning to free look, fade in when returning
            float targetShakeWeight = transitionToFreeLook ? 0f : 1f;
            transitionShakeWeight = Mathf.MoveTowards(transitionShakeWeight, targetShakeWeight,
                Time.unscaledDeltaTime * shakeTransitionBlendSpeed);

            Vector3 targetPos;
            Quaternion targetRot1;

            if (transitionToFreeLook)
            {
                // 计算目标第三人称位置
                Quaternion rotation = Quaternion.Euler(pitch, yaw, 0);
                Vector3 offset = rotation * new Vector3(0, 0, -freeLookDistance);
                targetPos = target.position + Vector3.up * 1.5f + offset;
                targetRot1 = Quaternion.LookRotation((target.position + Vector3.up * 1.5f) - targetPos);
            }
            else
            {
                // 计算目标第一人称位置
                targetPos = target.position + headOffset;
                targetRot1 = Quaternion.Euler(pitch, yaw, 0);
            }

            // Compute shake offset with transition weight
            Vector3 shakeOff = ComputeShakeOffset() * transitionShakeWeight;

            transform.position = Vector3.Lerp(transitionStartPos, targetPos, t) + shakeOff;
            transform.rotation = Quaternion.Slerp(transitionStartRot, targetRot1, t);

            // 过渡期间仍允许鼠标输入
            if (controlsEnabled && Mouse.current != null)
            {
                Vector2 delta = Mouse.current.delta.ReadValue() * lookSensitivity * 0.01f;
                yaw += delta.x;
                pitch += (invertY ? delta.y : -delta.y);
                pitch = Mathf.Clamp(pitch, transitionToFreeLook ? -30f : -80f, 80f);
            }

            if (rawT >= 1f)
            {
                isTransitioning = false;
                transitionShakeWeight = transitionToFreeLook ? 0f : 1f;
            }
            return;
        }

        // ===== 自由第三人称模式（用于循迹分身） =====
        if (freeLookMode)
        {
            if (controlsEnabled && Mouse.current != null)
            {
                Vector2 delta = Mouse.current.delta.ReadValue() * lookSensitivity * 0.01f;
                yaw += delta.x;
                pitch += (invertY ? delta.y : -delta.y);
                pitch = Mathf.Clamp(pitch, -30f, 80f);
            }

            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0);
            Vector3 offset = rotation * new Vector3(0, 0, -freeLookDistance);
            transform.position = target.position + Vector3.up * 1.5f + offset;
            transform.LookAt(target.position + Vector3.up * 1.5f);
            return;
        }

        // ===== ԭ�и����߼�������/�ӽ������ =====
        if (controlsEnabled && Mouse.current != null && !isCameraLocked)
        {
            Vector2 delta = Mouse.current.delta.ReadValue() * lookSensitivity * 0.01f;
            yaw += delta.x;
            pitch += (invertY ? delta.y : -delta.y);
            pitch = Mathf.Clamp(pitch, -80f, 80f);
        }

        bool isGrounded = playerController != null && playerController.Controller.isGrounded;

        // ���¹���Ȩ��
        float targetWeight = isGrounded ? 0f : 1f;
        airTransitionWeight = Mathf.SmoothDamp(airTransitionWeight, targetWeight, ref airTransitionVelocity, airTransitionSmoothTime);

        // ����ǿ��
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

        // ����ƫ��
        Vector3 shakeOffset = ComputeShakeOffset();
        // Only reset phase when shake is genuinely disabled, not at zero-crossings
        // (resetting at zero-crossings breaks the figure-8 Lissajous pattern)
        if (!enableShake || currentShakeIntensity <= 0.01f)
        {
            shakePhase = 0f;
        }

        // ����ƫ��
        UpdatePitchOffsetRaw(isGrounded);
        float finalPitchOffset = pitchOffsetRaw * airTransitionWeight;

        // ������ת
        Quaternion targetRot;
        if (isCameraLocked)
            targetRot = lockedRotation;
        else
            targetRot = Quaternion.Euler(pitch + finalPitchOffset + landReboundOffset, yaw, 0);

        transform.rotation = targetRot;

        // λ��
        if (isCameraLocked && followTargetWhenLocked && target != null)
            transform.position = target.position + headOffset + shakeOffset;
        else if (!isCameraLocked)
            transform.position = target.position + headOffset + shakeOffset;

        // ��ػص��߼�
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

    /// <summary>
    /// Computes camera shake offset based on current state. Can be called from
    /// both the normal path and transition path to support weighted blending.
    /// </summary>
    private Vector3 ComputeShakeOffset()
    {
        if (!controlsEnabled || !enableShake || currentShakeIntensity <= 0.01f)
            return Vector3.zero;

        float freq = Mathf.Lerp(0.5f, maxShakeFrequency, currentShakeIntensity);
        float hAmp = horizontalShakeAmplitude * currentShakeIntensity * shakeIntensityMultiplier;
        float vAmp = verticalShakeAmplitude * currentShakeIntensity * shakeIntensityMultiplier;
        shakePhase += Time.deltaTime * freq;
        float xShake = Mathf.Sin(shakePhase) * hAmp;
        float yShake = Mathf.Sin(shakePhase * 2f) * vAmp;
        return transform.right * xShake + transform.up * yShake;
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
        // Don't re-enable controls if the game is paused
        if (PauseManager.Instance != null && PauseManager.Instance.IsPaused)
        {
            controlsEnabled = false;
            return;
        }
        controlsEnabled = hasFocus;
        ApplyControlsState();
    }

    // ========== �������� ==========
    public void SetCameraLock(bool locked, Quaternion fixedRotation, bool followTarget = true)
    {
        isCameraLocked = locked;
        if (locked)
        {
            lockedRotation = fixedRotation;
            followTargetWhenLocked = followTarget;
        }
    }

    public void SetControlsEnabled(bool enabled)
    {
        controlsEnabled = enabled;
        ApplyControlsState();
    }

    /// <summary>
    /// ����/�������ɵ����˳�ģʽ������ѭ��������
    /// </summary>
    public void SetFreeLookMode(bool enabled, float distance = 5f)
    {
        bool wasFreeLook = freeLookMode;
        freeLookMode = enabled;
        freeLookDistance = distance;

        // 启动过渡动画
        if (wasFreeLook != enabled)
        {
            isTransitioning = true;
            transitionTimer = 0f;
            transitionDuration = viewTransitionDuration;
            transitionStartPos = transform.position;
            transitionStartRot = transform.rotation;
            transitionToFreeLook = enabled;
        }

        if (enabled)
        {
            controlsEnabled = true;
            isCameraLocked = false;
            ApplyControlsState();
        }
    }

    public void SetYawPitch(float newYaw, float newPitch)
    {
        yaw = newYaw;
        pitch = Mathf.Clamp(newPitch, -80f, 80f);
    }

    public float GetPitch() => pitch;
    public float GetYaw() => yaw;
}