using UnityEngine;

public class TrapdoorRotate : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform trapdoorPlaceholder; // The object that should rotate
    [SerializeField] private Transform anchor;              // Rotation pivot (hinge point)

    [Header("Rotation")]
    [SerializeField] private float openAngle = 90f;         // Z +90 when pressed
    [SerializeField] private float rotateSpeed = 180f;      // Degrees per second

    private Vector3 closedPosition;
    private Quaternion closedRotation;
    private float currentAngle;
    private float targetAngle;

    private void Awake()
    {
        if (trapdoorPlaceholder == null)
            trapdoorPlaceholder = transform;

        if (anchor == null)
            anchor = transform;

        // Cache closed state as the initial state
        closedPosition = trapdoorPlaceholder.position;
        closedRotation = trapdoorPlaceholder.rotation;
        currentAngle = 0f;
        targetAngle = 0f;

        ApplyRotation(0f);
    }

    private void Update()
    {
        // Smoothly move current angle toward target angle
        currentAngle = Mathf.MoveTowards(currentAngle, targetAngle, rotateSpeed * Time.deltaTime);
        ApplyRotation(currentAngle);
    }

    // Hook this to PressurePlate.onPressed
    public void OnPressed()
    {
        targetAngle = openAngle;
    }

    // Hook this to PressurePlate.onReleased
    public void OnReleased()
    {
        targetAngle = 0f;
    }

    // Optional helper if you want a bool-based event in the future
    public void SetPressed(bool pressed)
    {
        targetAngle = pressed ? openAngle : 0f;
    }

    private void ApplyRotation(float angle)
    {
        // Rotate around anchor's local Z axis in world space
        Vector3 worldAxis = anchor.TransformDirection(Vector3.forward);
        Quaternion delta = Quaternion.AngleAxis(angle, worldAxis);

        Vector3 offsetFromAnchor = closedPosition - anchor.position;
        trapdoorPlaceholder.position = anchor.position + delta * offsetFromAnchor;
        trapdoorPlaceholder.rotation = delta * closedRotation;
    }
}