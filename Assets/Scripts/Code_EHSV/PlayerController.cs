using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    private CharacterController _controller;

    [Header("Movement")]
    public float moveSpeed = 5.0f;
    public float sprintMultiplier = 1.5f;
    public float forwardFactor = 1.0f;
    public float strafeFactor = 0.7f;
    public float backFactor = 0.5f;
    public float rotateSpeed = 10.0f;

    [Header("Air Control")]
    public float airAcceleration = 8f;
    public float airDrag = 3f;
    public float airTurnSpeed = 5f;
    public float maxAirSpeed = 8f;

    [Header("Jump")]
    public float jumpForce = 2.2f;
    public float gravity = 12f;
    public float jumpBufferTime = 0.15f;
    public float coyoteTime = 0.1f;

    [Header("State")]
    public bool canMove = true;
    public bool freezeGravity = false;

    [Header("Rotation")]
    public bool faceMovementDirection = false;

    private Vector2 _moveInput;
    private bool _jumpPressed;
    private bool _jumpHeld;
    private bool _jumpTriggerFlag;

    private bool _isSprinting;
    public bool IsSprinting => useExternalInput ? externalSprint : _isSprinting;

    [HideInInspector] public bool useExternalInput = false;
    private Vector2 externalMoveInput;
    private bool externalJump;
    private bool externalSprint;
    private bool prevExternalJump;

    [HideInInspector] public bool useCameraOverride = false;
    [HideInInspector] public float overrideCameraYaw = 0f;

    public float CurrentMaxSpeed => moveSpeed * (IsSprinting ? sprintMultiplier : 1f);
    public bool IsAirborne { get; private set; }

    private Vector3 _jumpVelocity;
    private Vector3 _airHorizontalVelocity;
    private float _jumpBufferTimer = 0f;
    private float _coyoteTimer = 0f;

    public System.Action<Vector3, Vector3> OnPositionChanged;
    private Vector3 _lastFramePosition;

    public CharacterController Controller => _controller;

    public Vector2 GetEffectiveMoveInput() => useExternalInput ? externalMoveInput : _moveInput;
    public bool GetEffectiveJump() => useExternalInput ? externalJump : _jumpPressed;
    public bool GetEffectiveSprint() => useExternalInput ? externalSprint : _isSprinting;

    public Vector2 GetRawMoveInput() => _moveInput;
    public bool GetRawJumpPressed() => _jumpPressed;

    public bool ConsumeJumpTrigger()
    {
        bool v = _jumpTriggerFlag;
        _jumpTriggerFlag = false;
        return v;
    }

    public void SetExternalInput(Vector2 move, bool jump, bool sprint)
    {
        externalMoveInput = move;
        externalJump = jump;
        externalSprint = sprint;
    }

    [Header("External Jump Buffer")]
    public float externalJumpBufferTime = 0.3f;

    public void TriggerJumpBuffer()
    {
        _jumpBufferTimer = externalJumpBufferTime;
    }

    [HideInInspector] public bool useFixedUpdateMode = false;

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
            _jumpTriggerFlag = true;
        }
        else
        {
            _jumpPressed = false;
        }
        _jumpHeld = value.isPressed;
    }
    void OnSprint(InputValue value) => _isSprinting = value.isPressed;

    void Update()
    {
        if (freezeGravity) return;
        if (useFixedUpdateMode) return;

        if (useExternalInput)
        {
            if (externalJump && !prevExternalJump)
                _jumpBufferTimer = externalJumpBufferTime;
            prevExternalJump = externalJump;
        }

        DoMovementTick(Time.deltaTime);
    }

    void FixedUpdate()
    {
        if (!useFixedUpdateMode || freezeGravity) return;

        if (useExternalInput)
        {
            if (externalJump && !prevExternalJump)
                _jumpBufferTimer = externalJumpBufferTime;
            prevExternalJump = externalJump;
        }

        DoMovementTick(Time.fixedDeltaTime);
    }

    private void DoMovementTick(float dt)
    {
        if (_jumpBufferTimer > 0)
            _jumpBufferTimer -= dt;
        if (_coyoteTimer > 0)
            _coyoteTimer -= dt;

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

            // 播放跳跃音效
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayJumpSound();
            }
        }

        _jumpVelocity.y -= gravity * dt;

        Vector3 horizontalMotion = Vector3.zero;
        Vector3 desiredVel = CalculateDesiredHorizontalVelocity();

        if (IsAirborne)
        {
            if (desiredVel.magnitude > maxAirSpeed)
                desiredVel = desiredVel.normalized * maxAirSpeed;

            if (desiredVel.magnitude > 0.01f)
            {
                _airHorizontalVelocity = Vector3.MoveTowards(
                    _airHorizontalVelocity,
                    desiredVel,
                    airAcceleration * dt
                );
            }
            else
            {
                _airHorizontalVelocity *= (1f - airDrag * dt);
                if (_airHorizontalVelocity.magnitude < 0.01f)
                    _airHorizontalVelocity = Vector3.zero;
            }

            if (_airHorizontalVelocity.magnitude > maxAirSpeed)
                _airHorizontalVelocity = _airHorizontalVelocity.normalized * maxAirSpeed;

            horizontalMotion = _airHorizontalVelocity;
        }
        else
        {
            horizontalMotion = desiredVel;
        }

        Vector3 motion = horizontalMotion + _jumpVelocity;
        _controller.Move(motion * dt);

        if (canMove && !freezeGravity)
        {
            if (faceMovementDirection)
            {
                Vector2 move = GetEffectiveMoveInput();
                if (move != Vector2.zero)
                {
                    Camera cam = Camera.main;
                    if (cam != null)
                    {
                        Vector3 camForward = cam.transform.forward;
                        Vector3 camRight = cam.transform.right;
                        camForward.y = 0; camForward.Normalize();
                        camRight.y = 0; camRight.Normalize();

                        Vector3 moveDir = camForward * move.y + camRight * move.x;
                        if (moveDir != Vector3.zero)
                        {
                            Quaternion targetRot = Quaternion.LookRotation(moveDir);
                            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotateSpeed * dt);
                        }
                    }
                }
            }
            else
            {
                Vector3 camForward;
                if (useCameraOverride)
                {
                    Quaternion rot = Quaternion.Euler(0, overrideCameraYaw, 0);
                    camForward = rot * Vector3.forward;
                }
                else
                {
                    Camera cam = Camera.main;
                    camForward = (cam != null) ? cam.transform.forward : Vector3.zero;
                }
                camForward.y = 0;
                if (camForward != Vector3.zero)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(camForward);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotateSpeed * dt);
                }
            }
        }

        if (IsAirborne && _controller.isGrounded)
        {
            IsAirborne = false;

            // 播放落地音效
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayLandingSound(_jumpVelocity.y);
            }
        }

        Vector3 newPos = transform.position;
        if (OnPositionChanged != null && newPos != _lastFramePosition)
        {
            OnPositionChanged(_lastFramePosition, newPos);
        }
        _lastFramePosition = newPos;
    }

    private Vector3 CalculateDesiredHorizontalVelocity()
    {
        Vector2 move = GetEffectiveMoveInput();
        if (!canMove || move == Vector2.zero) return Vector3.zero;

        Vector3 camForward, camRight;
        if (useCameraOverride)
        {
            Quaternion rot = Quaternion.Euler(0, overrideCameraYaw, 0);
            camForward = rot * Vector3.forward;
            camRight = rot * Vector3.right;
        }
        else
        {
            Camera cam = Camera.main;
            if (cam == null) return Vector3.zero;
            camForward = cam.transform.forward;
            camRight = cam.transform.right;
        }
        camForward.y = 0; camForward.Normalize();
        camRight.y = 0; camRight.Normalize();

        Vector3 desiredDir = camForward * move.y + camRight * move.x;
        desiredDir.Normalize();

        float finalSpeed;
        if (faceMovementDirection)
        {
            finalSpeed = moveSpeed;
            if (IsSprinting && !IsAirborne) finalSpeed *= sprintMultiplier;
        }
        else
        {
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