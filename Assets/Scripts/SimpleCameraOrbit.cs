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
    public float shakeSmoothTime = 0.2f;

    // 跳跃视角偏移参数
    public bool enableJumpPitch = true;
    public float maxJumpPitchOffset = 15f;
    public float pitchSmoothTime = 0.1f;
    public float landReboundStrength = 0.5f;
    public float landReboundDuration = 0.2f;

    // 控制状态
    public bool controlsEnabled = true;

    private float yaw = 0f;
    private float pitch = 0f;
    private float pitchOffset = 0f;
    private float pitchOffsetVelocity = 0f;
    private float landReboundTimer = 0f;
    private float landReboundOffset = 0f;

    private PlayerController playerController;
    private float shakePhase = 0f;
    private float currentShakeIntensity = 0f;

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

        // 计算跳跃俯仰偏移
        UpdatePitchOffset();

        // 应用最终旋转
        transform.rotation = Quaternion.Euler(pitch + pitchOffset + landReboundOffset, yaw, 0);

        // 基础位置（头部跟随）
        Vector3 basePos = target.position + headOffset;

        // 抖动偏移 - 仅在玩家接地时计算
        Vector3 shakeOffset = Vector3.zero;
        bool isGrounded = playerController != null && playerController.Controller.isGrounded;

        if (controlsEnabled && enableShake && playerController != null && isGrounded)
        {
            float speed = playerController.GetCurrentHorizontalSpeed();
            float maxSpeed = playerController.CurrentMaxSpeed;
            float targetIntensity = Mathf.Clamp01(speed / maxSpeed);

            currentShakeIntensity = Mathf.Lerp(currentShakeIntensity, targetIntensity, Time.deltaTime / shakeSmoothTime);

            if (currentShakeIntensity > 0.01f)
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
        }

        transform.position = basePos + shakeOffset;

        // 落地回弹逻辑
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

    private void UpdatePitchOffset()
    {
        if (!enableJumpPitch || playerController == null)
        {
            pitchOffset = 0f;
            return;
        }

        float verticalSpeed = playerController.GetCurrentVerticalSpeed();
        bool isGrounded = playerController.Controller.isGrounded;

        float targetOffset = 0f;
        if (!isGrounded)
        {
            float speedRatio = Mathf.Clamp01(Mathf.Abs(verticalSpeed) / 15f);
            if (verticalSpeed > 0f)
                targetOffset = -maxJumpPitchOffset * speedRatio;
            else if (verticalSpeed < 0f)
                targetOffset = maxJumpPitchOffset * speedRatio;
        }

        pitchOffset = Mathf.SmoothDamp(pitchOffset, targetOffset, ref pitchOffsetVelocity, pitchSmoothTime);
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