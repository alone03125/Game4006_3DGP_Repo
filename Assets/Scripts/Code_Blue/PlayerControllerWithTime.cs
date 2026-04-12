using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerControllerWithTime : MonoBehaviour
{
    private CharacterController _controller;

    [Header("Movement Parameters")]
    public float moveSpeed = 5.0f;
    public float sprintMultiplier = 1.5f;
    public float forwardFactor = 1.0f;
    public float strafeFactor = 0.7f;
    public float backFactor = 0.5f;
    public float rotateSpeed = 10.0f;

    [Header("Air Movement Parameters")]
    public float airAcceleration = 8f;
    public float airDrag = 0.2f;
    public float airTurnSpeed = 5f;
    public float maxAirSpeed = 8f;

    [Header("Jump Parameters")]
    public float jumpForce = 8.0f;
    public float gravity = 9.8f;
    public float jumpBufferTime = 0.2f;
    public float coyoteTime = 0.15f;

    [Header("State Control")]
    public bool canMove = true;
    public bool freezeGravity = false;

    [Header("Puzzle Interaction")]
    [SerializeField] private float interactDistance = 3f;
    [SerializeField] private LayerMask interactMask;
    [SerializeField] private Transform rayOrigin;

    private bool isInPuzzleZone = false;

    private Vector2 _moveInput;
    private bool _jumpPressed;
    private bool _jumpHeld;

    public bool IsSprinting { get; private set; }
    public float CurrentMaxSpeed => moveSpeed * (IsSprinting ? sprintMultiplier : 1f);

    public bool IsAirborne { get; private set; }

    private Vector3 _jumpVelocity;
    private Vector3 _airHorizontalVelocity;

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

    void OnPress(InputValue value)
    {
        if (!value.isPressed) return;
        TryPuzzleInteract();
    }

    void Update()
    {
        if (freezeGravity) return;

       // If time is stopped, do not execute player logic
        if (Time.timeScale <= 0f) return;

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

        if (IsAirborne && _controller.isGrounded)
            IsAirborne = false;

       Transform origin = rayOrigin != null ? rayOrigin : (Camera.main != null ? Camera.main.transform : null);
    if (origin != null)
    {
        Color rayColor = isInPuzzleZone ? Color.green : Color.red;
        Debug.DrawRay(origin.position, origin.forward * interactDistance, rayColor);
    }     
    }

    private Vector3 CalculateDesiredHorizontalVelocity()
    {
        if (!canMove || _moveInput == Vector2.zero) return Vector3.zero;

        Camera cam = Camera.main;
        if (cam == null) return Vector3.zero;

        Vector3 camForward = cam.transform.forward;
        Vector3 camRight = cam.transform.right;
        camForward.y = 0;
        camForward.Normalize();
        camRight.y = 0;
        camRight.Normalize();

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
        return CalculateDesiredHorizontalVelocity().magnitude;
    }

    public float GetCurrentVerticalSpeed()
    {
        return _jumpVelocity.y;
    }

    public void SetPuzzleZoneState(bool inZone)
    {
        isInPuzzleZone = inZone;
    }
    private void TryPuzzleInteract()
    {
        if (!isInPuzzleZone) return;
        Transform origin = rayOrigin != null ? rayOrigin : Camera.main.transform;
        Ray ray = new Ray(origin.position, origin.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance, interactMask))
        {
            PuzzleCube cube = hit.collider.GetComponent<PuzzleCube>();
            if (cube != null)
            {
                cube.Interact();
            }
        }
    }
}