using UnityEngine;
using UnityEngine.InputSystem;

public class SimpleCameraOrbit : MonoBehaviour
{
    public Transform target;
    public Vector3 headOffset = new Vector3(0, 1.5f, 0);
    public float lookSensitivity = 2f;

    // 抖动参数
    public bool enableShake = true;
    public float horizontalShakeAmplitude = 0.05f;
    public float verticalShakeAmplitude = 0.025f;
    public float maxShakeFrequency = 15f;
    public float shakeSmoothTime = 0.2f;          // 抖动强度变化平滑时间

    // 跳跃视角偏移参数
    public bool enableJumpPitch = true;
    public float maxJumpPitchOffset = 15f;
    public float pitchSmoothTime = 0.1f;
    public float landReboundStrength = 0.5f;
    public float landReboundDuration = 0.2f;
    public float airTransitionSmoothTime = 0.2f;  // 地面-空中过渡平滑时间（用于混合）

    // 控制状态
    public bool controlsEnabled = true;

    private float yaw = 0f;
    private float pitch = 0f;
    private float pitchOffsetRaw = 0f;      // 原始俯仰偏移（未混合）
    private float pitchOffsetVelocity = 0f;
    private float landReboundTimer = 0f;
    private float landReboundOffset = 0f;

    private float airTransitionWeight = 0f;   // 0=完全地面, 1=完全空中
    private float airTransitionVelocity = 0f;

    private PlayerController playerController;
    private float shakePhase = 0f;
    private float currentShakeIntensity = 0f; // 当前抖动强度（平滑后）

    private bool wasGrounded = true;

    void Start()
    {
        if (target == null)
            target = GameObject.Find("Player")?.transform;

        playerController = target?.GetComponent<PlayerController>();
        ApplyControlsState();
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

        // 鼠标视角旋转
        if (controlsEnabled && Mouse.current != null)
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

        // 1. 抖动强度平滑（无论是否接地，目标值会变化，但平滑过渡）
        if (enableShake && playerController != null)
        {
            float targetIntensity = 0f;
            if (isGrounded)  // 接地时根据速度计算目标强度
            {
                float speed = playerController.GetCurrentHorizontalSpeed();
                float maxSpeed = playerController.CurrentMaxSpeed;
                targetIntensity = Mathf.Clamp01(speed / maxSpeed);
            }
            // 空中时目标强度为0，自然衰减
            currentShakeIntensity = Mathf.Lerp(currentShakeIntensity, targetIntensity, Time.deltaTime / shakeSmoothTime);
        }
        else
        {
            currentShakeIntensity = 0f;
        }

        // 2. 计算抖动偏移（基于平滑后的强度）
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

        // 3. 原始俯仰偏移（根据垂直速度，仅在空中计算目标值，但平滑过渡）
        UpdatePitchOffsetRaw(isGrounded);

        // 4. 最终俯仰偏移 = 原始偏移 * 过渡权重（地面时权重为0，空中逐渐变为1）
        float finalPitchOffset = pitchOffsetRaw * airTransitionWeight;

        // 5. 最终旋转和位置
        transform.rotation = Quaternion.Euler(pitch + finalPitchOffset + landReboundOffset, yaw, 0);
        transform.position = target.position + headOffset + shakeOffset;

        // 6. 落地回弹逻辑（独立）
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
            {
                landReboundOffset = 0f;
            }
            else
            {
                landReboundOffset = Mathf.Lerp(0f, landReboundOffset, landReboundTimer / landReboundDuration);
            }
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

        // 目标偏移：空中根据速度，地面为0
        float targetOffset = 0f;
        if (!isGrounded)
        {
            float speedRatio = Mathf.Clamp01(Mathf.Abs(verticalSpeed) / 15f);
            if (verticalSpeed > 0f)
                targetOffset = -maxJumpPitchOffset * speedRatio;
            else if (verticalSpeed < 0f)
                targetOffset = maxJumpPitchOffset * speedRatio;
        }

        // 平滑过渡（地面时也会平滑归零，避免突变）
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
}