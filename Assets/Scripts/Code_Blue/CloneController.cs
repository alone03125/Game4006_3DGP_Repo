using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class CloneTimeStopMover : MonoBehaviour
{
    [Header("Only effective when time is stopped")]
    public float moveSpeed = 4f;
    public float gravity = 20f;

    private CharacterController _cc;

    void Awake()
    {
        _cc = GetComponent<CharacterController>();
        if (!CompareTag("Clone"))
            Debug.LogWarning($"Object {name} should be tagged as Clone", this);
    }

    void Update()
    {
        // If time is not stopped will not move
        if (Time.timeScale > 0f)
            return;

        float dt = Time.unscaledDeltaTime;

        Vector2 input = Vector2.zero;
        if (Keyboard.current != null)
        {
            if (Keyboard.current.iKey.isPressed) input.y += 1f;
            if (Keyboard.current.kKey.isPressed) input.y -= 1f;
            if (Keyboard.current.jKey.isPressed) input.x -= 1f;
            if (Keyboard.current.lKey.isPressed) input.x += 1f;
        }

        Camera cam = Camera.main;
        if (cam == null)
            return;

        Vector3 camForward = cam.transform.forward;
        Vector3 camRight = cam.transform.right;
        camForward.y = 0f;
        camRight.y = 0f;
        camForward.Normalize();
        camRight.Normalize();

        Vector3 moveDir = camForward * input.y + camRight * input.x;
        if (moveDir.sqrMagnitude > 1f)
            moveDir.Normalize();

        Vector3 motion = moveDir * moveSpeed;
        if (!_cc.isGrounded)
            motion.y = -gravity;

        _cc.Move(motion * dt);
    }
}