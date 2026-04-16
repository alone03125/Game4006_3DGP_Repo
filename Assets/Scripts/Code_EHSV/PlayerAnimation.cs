using UnityEngine;

/// <summary>
/// 玩家动画控制器 - 独立模块，不依赖其他系统的修改。
/// 根据 PlayerController 的状态驱动 Animator 参数：
/// - 前进时播放跑步动画
/// - 其他方向移动时播放潜行动画
/// - 空中播放下落动画（无起跳前摇）
/// </summary>
public class PlayerAnimation : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerController playerController;

    [Header("Settings")]
    [Tooltip("移动输入与角色前方的点积阈值，大于此值视为前进")]
    [SerializeField] private float forwardDotThreshold = 0.7f;
    [Tooltip("移动输入的最小幅值，低于此值视为静止")]
    [SerializeField] private float moveInputDeadzone = 0.1f;

    // Animator 参数哈希（性能优化）
    private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int VerticalSpeedHash = Animator.StringToHash("VerticalSpeed");
    private static readonly int MoveSpeedHash = Animator.StringToHash("MoveSpeed");
    private static readonly int InputMagnitudeHash = Animator.StringToHash("InputMagnitude");
    private static readonly int IsRunningHash = Animator.StringToHash("IsRunning");
    private static readonly int IsCrouchingHash = Animator.StringToHash("IsCrouching");

    private void Awake()
    {
        // 自动获取组件（若未手动指定）
        if (animator == null)
            animator = GetComponent<Animator>();
        if (playerController == null)
            playerController = GetComponent<PlayerController>();

        // 可选：确保 Animator 存在
        if (animator == null)
            Debug.LogWarning($"[PlayerAnimation] Animator not found on {gameObject.name}");
        if (playerController == null)
            Debug.LogWarning($"[PlayerAnimation] PlayerController not found on {gameObject.name}");
    }

    private void Update()
    {
        if (animator == null || playerController == null)
            return;

        // 获取基础状态
        bool isGrounded = playerController.Controller.isGrounded;
        float verticalSpeed = playerController.GetCurrentVerticalSpeed();
        Vector2 moveInput = playerController.GetEffectiveMoveInput();
        float inputMagnitude = moveInput.magnitude;
        float currentHorizontalSpeed = playerController.GetCurrentHorizontalSpeed();

        // 判断是否在移动
        bool isMoving = inputMagnitude > moveInputDeadzone;

        // 判断是否前进：基于移动方向与角色正前方的点积
        bool isForward = false;
        if (isMoving)
        {
            // 注意：PlayerController 中 faceMovementDirection 控制角色朝向，此处直接用 transform.forward
            Vector3 moveDir = (transform.forward * moveInput.y + transform.right * moveInput.x).normalized;
            float dot = Vector3.Dot(moveDir, transform.forward);
            isForward = dot >= forwardDotThreshold;
        }

        // 确定动画状态：前进且接地 -> 跑步；其他方向移动且接地 -> 潜行
        bool isRunning = isMoving && isForward && isGrounded;
        bool isCrouching = isMoving && !isForward && isGrounded;

        // 将状态写入 Animator
        animator.SetBool(IsGroundedHash, isGrounded);
        animator.SetFloat(VerticalSpeedHash, verticalSpeed);
        animator.SetFloat(MoveSpeedHash, currentHorizontalSpeed);
        animator.SetFloat(InputMagnitudeHash, inputMagnitude);
        animator.SetBool(IsRunningHash, isRunning);
        animator.SetBool(IsCrouchingHash, isCrouching);
    }

    // 可选：提供公共方法供外部强制触发动画（例如特殊交互）
    public void SetAnimatorTrigger(string triggerName)
    {
        if (animator != null)
            animator.SetTrigger(triggerName);
    }

    public void SetAnimatorBool(string paramName, bool value)
    {
        if (animator != null)
            animator.SetBool(paramName, value);
    }
}