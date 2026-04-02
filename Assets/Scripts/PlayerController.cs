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
    public float jumpForce = 8.0f;        // 跳跃初速度（可调节）
    public float gravity = 9.8f;          // 重力加速度（可调节）
    public float jumpBufferTime = 0.2f;   // 跳跃预输入窗口
    public float coyoteTime = 0.15f;      // 土狼跳时间

    [Header("状态控制")]
    public bool canMove = true;
    public bool freezeGravity = false;

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

    public CharacterController Controller => _controller;

    void Start()
    {
        _controller = GetComponent<CharacterController>();
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

        // 土狼跳计时器：仅在离开地面的瞬间启动
        if (!grounded && !IsAirborne)
        {
            _coyoteTimer = coyoteTime;
        }

        if (grounded)
        {
            _coyoteTimer = 0f;
            if (_jumpVelocity.y < 0)
                _jumpVelocity.y = -2f;
        }

        // 跳跃触发条件：有缓冲计时器 且 (在地面 或 土狼计时器有效)
        bool canJump = _jumpBufferTimer > 0 && (grounded || _coyoteTimer > 0);
        if (canJump)
        {
            _airHorizontalVelocity = CalculateDesiredHorizontalVelocity();
            IsAirborne = true;
            _jumpVelocity.y = Mathf.Sqrt(jumpForce * 2f * gravity); // 使用可调重力
            _jumpBufferTimer = 0f;
            _coyoteTimer = 0f;
        }

        _jumpVelocity.y -= gravity * Time.deltaTime; // 使用可调重力

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

        // 角色朝向：基于相机水平方向（视角方向）
        if (canMove && !freezeGravity)
        {
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

        // 落地检测
        if (IsAirborne && _controller.isGrounded)
        {
            IsAirborne = false;
        }
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

        float dot = Vector3.Dot(desiredDir, camForward);
        float directionFactor;
        if (dot > 0.7f) directionFactor = forwardFactor;
        else if (dot < -0.7f) directionFactor = backFactor;
        else directionFactor = strafeFactor;

        float finalSpeed = moveSpeed * directionFactor;
        if (IsSprinting && !IsAirborne) finalSpeed *= sprintMultiplier;

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