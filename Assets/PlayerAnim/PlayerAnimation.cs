using UnityEngine;

/// <summary>
/// 玩家动画控制器 - 独立模块，挂载在物理根对象上。
/// 自动查找子对象中的 Animator 并驱动其参数。
/// - 支持前后左右四向独立动画（通过 Blend Tree 混合）
/// - 空中播放下落动画（无起跳前摇）
/// - 时停期间暂停本体的动画更新
/// </summary>
public class PlayerAnimation : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private CloneManager cloneManager;

    [Header("Movement Settings")]
    [SerializeField] private float moveInputDeadzone = 0.1f;
    [SerializeField] private float directionSmoothTime = 0.1f;

    // Animator 参数哈希
    private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int VerticalSpeedHash = Animator.StringToHash("VerticalSpeed");
    private static readonly int MoveSpeedHash = Animator.StringToHash("MoveSpeed");
    private static readonly int InputMagnitudeHash = Animator.StringToHash("InputMagnitude");
    private static readonly int HorizontalHash = Animator.StringToHash("Horizontal");
    private static readonly int VerticalHash = Animator.StringToHash("Vertical");

    // 平滑方向
    private Vector2 currentLocalDirection;
    private Vector2 targetLocalDirection;
    private Vector2 directionVelocity;

    private bool _warnedMissingController = false;
    private bool _warnedMissingCamera = false;
    private bool _warnedMissingControllerRef = false;

    // 是否应该暂停动画（由外部设置）
    private bool shouldPauseAnimation = false;

    private void Awake()
    {
        if (playerController == null)
        {
            playerController = GetComponent<PlayerController>();
            if (playerController == null)
                Debug.LogError($"[PlayerAnimation] PlayerController not found on {gameObject.name}.");
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
            if (animator == null)
                Debug.LogError($"[PlayerAnimation] No Animator found in children of {gameObject.name}.");
        }

        if (cloneManager == null)
        {
            cloneManager = GetComponent<CloneManager>();
        }
    }

    private void Start()
    {
        if (animator != null)
        {
            animator.Rebind();
            animator.Update(0.01f);
        }
    }

    private void Update()
    {
        if (animator == null || playerController == null)
            return;

        if (animator.runtimeAnimatorController == null)
        {
            if (!_warnedMissingController)
            {
                Debug.LogWarning($"[PlayerAnimation] Animator on {animator.gameObject.name} has no Controller assigned.");
                _warnedMissingController = true;
            }
            return;
        }
        _warnedMissingController = false;

        if (playerController.Controller == null)
        {
            if (!_warnedMissingControllerRef)
            {
                Debug.LogWarning($"[PlayerAnimation] PlayerController.Controller is null.");
                _warnedMissingControllerRef = true;
            }
            return;
        }
        _warnedMissingControllerRef = false;

        // ========== 动画暂停检测 ==========
        // 只有本体在时停期间才暂停动画，循迹分身正常播放
        if (shouldPauseAnimation)
        {
            animator.speed = 0f;
            return;
        }
        else
        {
            animator.speed = 1f;
        }

        Camera cam = Camera.main;
        if (cam == null)
        {
            if (!_warnedMissingCamera)
            {
                Debug.LogWarning("[PlayerAnimation] No main camera found in scene.");
                _warnedMissingCamera = true;
            }
            return;
        }
        _warnedMissingCamera = false;

        // ========== 获取玩家状态 ==========
        bool isGrounded = playerController.Controller.isGrounded;
        float verticalSpeed = playerController.GetCurrentVerticalSpeed();
        Vector2 moveInput = playerController.GetEffectiveMoveInput();
        float inputMagnitude = moveInput.magnitude;
        float currentHorizontalSpeed = playerController.GetCurrentHorizontalSpeed();

        // ========== 计算局部移动方向 ==========
        targetLocalDirection = Vector2.zero;
        if (inputMagnitude > moveInputDeadzone)
        {
            Vector3 camForward = cam.transform.forward;
            Vector3 camRight = cam.transform.right;
            camForward.y = 0f; camForward.Normalize();
            camRight.y = 0f; camRight.Normalize();

            Vector3 worldMoveDir = camForward * moveInput.y + camRight * moveInput.x;
            worldMoveDir.Normalize();

            Vector3 characterForward = transform.forward;
            Vector3 characterRight = transform.right;
            characterForward.y = 0f; characterForward.Normalize();
            characterRight.y = 0f; characterRight.Normalize();

            float localX = Vector3.Dot(worldMoveDir, characterRight);
            float localY = Vector3.Dot(worldMoveDir, characterForward);

            targetLocalDirection = new Vector2(localX, localY);
        }

        currentLocalDirection = Vector2.SmoothDamp(currentLocalDirection, targetLocalDirection,
            ref directionVelocity, directionSmoothTime);

        // ========== 写入 Animator 参数 ==========
        animator.SetBool(IsGroundedHash, isGrounded);
        animator.SetFloat(VerticalSpeedHash, verticalSpeed);
        animator.SetFloat(MoveSpeedHash, currentHorizontalSpeed);
        animator.SetFloat(InputMagnitudeHash, inputMagnitude);
        animator.SetFloat(HorizontalHash, currentLocalDirection.x);
        animator.SetFloat(VerticalHash, currentLocalDirection.y);
    }

    /// <summary>
    /// 设置动画是否暂停
    /// </summary>
    public void SetPauseAnimation(bool pause)
    {
        shouldPauseAnimation = pause;
    }

    /// <summary>
    /// 立即停止动画并将 Animator 禁用（用于固化分身）
    /// </summary>
    public void StopAnimationImmediately()
    {
        if (animator != null)
        {
            animator.enabled = false;
        }
    }

    /// <summary>
    /// 手动触发 Animator 触发器
    /// </summary>
    public void SetAnimatorTrigger(string triggerName)
    {
        if (animator != null)
            animator.SetTrigger(triggerName);
    }

    /// <summary>
    /// 手动设置 Animator Bool 参数
    /// </summary>
    public void SetAnimatorBool(string paramName, bool value)
    {
        if (animator != null)
            animator.SetBool(paramName, value);
    }

    /// <summary>
    /// 手动设置 Animator Float 参数
    /// </summary>
    public void SetAnimatorFloat(string paramName, float value)
    {
        if (animator != null)
            animator.SetFloat(paramName, value);
    }

    /// <summary>
    /// 获取当前局部移动方向（仅供调试）
    /// </summary>
    public Vector2 GetCurrentLocalDirection() => currentLocalDirection;
}