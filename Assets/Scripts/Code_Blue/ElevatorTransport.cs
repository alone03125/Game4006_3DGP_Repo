using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class ElevatorTransport : MonoBehaviour
{
    [Header("Ground Check")]
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float checkDistance = 0.25f;
    [SerializeField] private float sphereRadiusOffset = 0.02f;

    [Header("Filter (Optional)")]
    [Tooltip("If empty, all platforms will be used")]
    [SerializeField] private string requiredPlatformTag = "";

    private CharacterController controller;
    private Transform currentPlatform;
    private Vector3 lastPlatformPos;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    private void LateUpdate()
    {
        if (TryGetGroundPlatform(out Transform platform))
        {
            if (platform != currentPlatform)
            {
                currentPlatform = platform;
                lastPlatformPos = currentPlatform.position;
                return; // Record the first frame to avoid instant movement
            }

            Vector3 platformDelta = currentPlatform.position - lastPlatformPos;
            if (platformDelta.sqrMagnitude > 0f)
            {
                controller.Move(platformDelta);
            }

            lastPlatformPos = currentPlatform.position;
        }
        else
        {
            currentPlatform = null;
        }
    }

    private bool TryGetGroundPlatform(out Transform platform)
    {
        platform = null;

        // Cast from the character's feet
        Vector3 centerWorld = transform.TransformPoint(controller.center);
        float castStartToBottom = (controller.height * 0.5f) - controller.radius + sphereRadiusOffset;
        Vector3 origin = centerWorld - Vector3.up * castStartToBottom;

        if (!Physics.SphereCast(origin, controller.radius * 0.95f, Vector3.down, out RaycastHit hit, checkDistance, groundMask, QueryTriggerInteraction.Ignore))
            return false;

        Transform hitTf = hit.collider.transform;

        if (!string.IsNullOrEmpty(requiredPlatformTag) && !hitTf.CompareTag(requiredPlatformTag))
            return false;

        platform = hitTf;
        return true;
    }
}