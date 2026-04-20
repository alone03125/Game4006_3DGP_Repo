using UnityEngine;

/// <summary>
/// 玩家动画控制器 - 独立模块，挂载在物理根对象上。
/// 自动查找子对象中的 Animator 并驱动其参数。
/// </summary>
public class PlayerAnimation : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;        // 可手动拖入子对象的 Animator
    [SerializeField] private PlayerController playerController;

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

    private void Awake()
    {
        if (playerController == null)
            playerController = GetComponent<PlayerController>();

        // 若未手动赋值，自动从子物体中查找 Animator
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (animator == null)
            Debug.LogWarning($"[PlayerAnimation] No Animator found in children of {gameObject.name}");
    }

    private void Update()
    {
        if (animator == null || playerController == null)
            return;

        // 确保 Animator 已配置 Controller
        if (animator.runtimeAnimatorController == null)
        {
            // 仅在首次出现时警告，避免刷屏
            if (!_warnedMissingController)
            {
                Debug.LogWarning($"[PlayerAnimation] Animator on {animator.gameObject.name} has no Controller assigned.");
                _warnedMissingController = true;
            }
            return;
        }
        _warnedMissingController = false;

        // 获取基础状态
        bool isGrounded = playerController.Controller.isGrounded;
        float verticalSpeed = playerController.GetCurrentVerticalSpeed();
        Vector2 moveInput = playerController.GetEffectiveMoveInput();
        float inputMagnitude = moveInput.magnitude;
        float currentHorizontalSpeed = playerController.GetCurrentHorizontalSpeed();

        // 计算局部移动方向（相对于角色自身坐标系）
        targetLocalDirection = Vector2.zero;
        if (inputMagnitude > moveInputDeadzone)
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                Vector3 camForward = cam.transform.forward;
                Vector3 camRight = cam.transform.right;
                camForward.y = 0f; camForward.Normalize();
                camRight.y = 0f; camRight.Normalize();

                // 世界空间移动方向
                Vector3 worldMoveDir = camForward * moveInput.y + camRight * moveInput.x;
                worldMoveDir.Normalize();

                // 获取角色的前方和右方（忽略俯仰）
                Vector3 characterForward = transform.forward;
                Vector3 characterRight = transform.right;
                characterForward.y = 0f; characterForward.Normalize();
                characterRight.y = 0f; characterRight.Normalize();

                // 将世界方向投影到角色局部轴，得到 -1..1 的值
                float localX = Vector3.Dot(worldMoveDir, characterRight);
                float localY = Vector3.Dot(worldMoveDir, characterForward);

                targetLocalDirection = new Vector2(localX, localY);
            }
        }

        // 平滑方向变化
        currentLocalDirection = Vector2.SmoothDamp(currentLocalDirection, targetLocalDirection,
            ref directionVelocity, directionSmoothTime);

        // 写入 Animator 参数
        animator.SetBool(IsGroundedHash, isGrounded);
        animator.SetFloat(VerticalSpeedHash, verticalSpeed);
        animator.SetFloat(MoveSpeedHash, currentHorizontalSpeed);
        animator.SetFloat(InputMagnitudeHash, inputMagnitude);
        animator.SetFloat(HorizontalHash, currentLocalDirection.x);
        animator.SetFloat(VerticalHash, currentLocalDirection.y);
    }

    private bool _warnedMissingController = false;
}