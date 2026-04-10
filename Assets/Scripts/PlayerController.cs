using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    private CharacterController _controller;

    [Header("移动参数")]
    public float moveSpeed = 5.0f;
    public float sprintMultiplier = 1.5f;
    public float forwardFactor = 1.0f;
    public float strafeFactor = 0.7f;
    public float backFactor = 0.5f;
    public float rotateSpeed = 10.0f;

    [Header("空中移动参数")]
    public float airAcceleration = 8f;
    public float airDrag = 0.2f;
    public float airTurnSpeed = 5f;
    public float maxAirSpeed = 8f;

    [Header("跳跃参数")]
    public float jumpForce = 2.2f;
    public float gravity = 12f;
    public float jumpBufferTime = 0.15f;
    public float coyoteTime = 0.1f;

    [Header("状态控制")]
    public bool canMove = true;
    public bool freezeGravity = false;

    [Header("分身专用设置")]
    public bool faceMovementDirection = false;   // true=分身模式（恒速），false=本体模式（各向差速）

    // 输入缓存
    private Vector2 _moveInput;
    private bool _jumpPressed;
    private bool _jumpHeld;

    // 冲刺状态
    public bool IsSprinting { get; private set; }
    public float CurrentMaxSpeed => moveSpeed * (IsSprinting ? sprintMultiplier : 1f);

    // 空中状态
    public bool IsAirborne { get; private set; }

    // 运动学变量
    private Vector3 _jumpVelocity;
    private Vector3 _airHorizontalVelocity;

    // 跳跃缓冲和土狼跳计时器
    private float _jumpBufferTimer = 0f;
    private float _coyoteTimer = 0f;

    // 位置变化事件（用于克隆体分离检测）
    public System.Action<Vector3, Vector3> OnPositionChanged;
    private Vector3 _lastFramePosition;

    public CharacterController Controller => _controller;

    void Start()
    {
        _controller = GetComponent<CharacterController>();
        _lastFramePosition = transform.position;
    }

    void OnMove(InputValue value) => _moveInput = value.Get<Vector2>();
    void OnJump(InputValue value)
    {
        if (value.isPressed)
        {
            _jumpPressed = true;
            _jumpBufferTimer = jumpBufferTime;
        }
        else
        {
            _jumpPressed = false;
        }
        _jumpHeld = value.isPressed;
    }
    void OnSprint(InputValue value) => IsSprinting = value.isPressed;

    void Update()
    {
        if (freezeGravity) return;

        // 更新计时器
        if (_jumpBufferTimer > 0)
            _jumpBufferTimer -= Time.deltaTime;
        if (_coyoteTimer > 0)
            _coyoteTimer -= Time.deltaTime;

        bool grounded = _controller.isGrounded;

        if (!grounded && !IsAirborne)
            _coyoteTimer = coyoteTime;

        if (grounded)
        {
            _coyoteTimer = 0f;
            if (_jumpVelocity.y < 0)
                _jumpVelocity.y = -2f;
        }

        bool canJump = _jumpBufferTimer > 0 && (grounded || _coyoteTimer > 0);
        if (canJump)
        {
            _airHorizontalVelocity = CalculateDesiredHorizontalVelocity();
            IsAirborne = true;
            _jumpVelocity.y = Mathf.Sqrt(jumpForce * 2f * gravity);
            _jumpBufferTimer = 0f;
            _coyoteTimer = 0f;
        }

        _jumpVelocity.y -= gravity * Time.deltaTime;

        // 水平移动计算
        Vector3 horizontalMotion = Vector3.zero;

        if (IsAirborne)
        {
            Vector3 desiredVel = CalculateDesiredHorizontalVelocity();
            if (desiredVel.magnitude > maxAirSpeed)
                desiredVel = desiredVel.normalized * maxAirSpeed;

            if (desiredVel.magnitude > 0.01f)
            {
                _airHorizontalVelocity = Vector3.MoveTowards(
                    _airHorizontalVelocity,
                    desiredVel,
                    airAcceleration * Time.deltaTime
                );
            }
            else
            {
                _airHorizontalVelocity *= (1f - airDrag * Time.deltaTime);
                if (_airHorizontalVelocity.magnitude < 0.01f)
                    _airHorizontalVelocity = Vector3.zero;
            }

            if (_airHorizontalVelocity.magnitude > maxAirSpeed)
                _airHorizontalVelocity = _airHorizontalVelocity.normalized * maxAirSpeed;

            horizontalMotion = _airHorizontalVelocity;
        }
        else
        {
            horizontalMotion = CalculateDesiredHorizontalVelocity();
        }

        Vector3 motion = horizontalMotion + _jumpVelocity;
        _controller.Move(motion * Time.deltaTime);

        // 角色朝向
        if (canMove && !freezeGravity)
        {
            if (faceMovementDirection)
            {
                // 分身模式：仅在移动时面向移动方向
                if (_moveInput != Vector2.zero)
                {
                    Camera cam = Camera.main;
                    if (cam != null)
                    {
                        Vector3 camForward = cam.transform.forward;
                        Vector3 camRight = cam.transform.right;
                        camForward.y = 0; camForward.Normalize();
                        camRight.y = 0; camRight.Normalize();

                        Vector3 moveDir = camForward * _moveInput.y + camRight * _moveInput.x;
                        if (moveDir != Vector3.zero)
                        {
                            Quaternion targetRot = Quaternion.LookRotation(moveDir);
                            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotateSpeed * Time.deltaTime);
                        }
                    }
                }
            }
            else
            {
                // 本体模式：始终面向相机前方（即使静止）
                Camera cam = Camera.main;
                if (cam != null)
                {
                    Vector3 camForward = cam.transform.forward;
                    camForward.y = 0;
                    if (camForward != Vector3.zero)
                    {
                        Quaternion targetRotation = Quaternion.LookRotation(camForward);
                        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotateSpeed * Time.deltaTime);
                    }
                }
            }
        }

        // 落地检测
        if (IsAirborne && _controller.isGrounded)
        {
            IsAirborne = false;
        }

        // 触发位置变化事件
        Vector3 newPos = transform.position;
        if (OnPositionChanged != null && newPos != _lastFramePosition)
        {
            OnPositionChanged(_lastFramePosition, newPos);
        }
        _lastFramePosition = newPos;
    }

    private Vector3 CalculateDesiredHorizontalVelocity()
    {
        if (!canMove || _moveInput == Vector2.zero) return Vector3.zero;

        Camera cam = Camera.main;
        if (cam == null) return Vector3.zero;

        Vector3 camForward = cam.transform.forward;
        Vector3 camRight = cam.transform.right;
        camForward.y = 0; camForward.Normalize();
        camRight.y = 0; camRight.Normalize();

        Vector3 desiredDir = camForward * _moveInput.y + camRight * _moveInput.x;
        desiredDir.Normalize();

        float finalSpeed;
        if (faceMovementDirection)
        {
            // 分身模式：始终全速移动，忽略方向因子（前/侧/后速度一致）
            finalSpeed = moveSpeed;
            if (IsSprinting && !IsAirborne) finalSpeed *= sprintMultiplier;
        }
        else
        {
            // 本体模式：使用各向差速
            float dot = Vector3.Dot(desiredDir, camForward);
            float directionFactor;
            if (dot > 0.7f) directionFactor = forwardFactor;
            else if (dot < -0.7f) directionFactor = backFactor;
            else directionFactor = strafeFactor;

            finalSpeed = moveSpeed * directionFactor;
            if (IsSprinting && !IsAirborne) finalSpeed *= sprintMultiplier;
        }

        return desiredDir * finalSpeed;
    }

    public float GetCurrentHorizontalSpeed()
    {
        if (IsAirborne)
            return _airHorizontalVelocity.magnitude;
        else
            return CalculateDesiredHorizontalVelocity().magnitude;
    }

    public float GetCurrentVerticalSpeed()
    {
        return _jumpVelocity.y;
    }
}