using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public class PlayerPush : MonoBehaviour
{
    [Header("Push Feel")]
    [SerializeField] private float pushForce = 120f;                 
    [SerializeField] private ForceMode forceMode = ForceMode.Force;   
    [SerializeField] private float maxPushSpeed = 1.8f;               
    [SerializeField] private float minMoveDir = 0.08f;               
    [SerializeField] private bool onlyPushWithHorizontalMove = true;

    [Header("Filter")]
    [Tooltip("If not empty, only push Rigidbody objects in these Layers.")]
    [SerializeField] private LayerMask pushableMask = ~0;

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {

        Rigidbody body = hit.collider.attachedRigidbody;
        if (body == null || body.isKinematic) return;
        if (((1 << body.gameObject.layer) & pushableMask) == 0) return;

         if (CompareTag("TracePhantom"))
        {
            if (hit.collider.GetComponentInParent<LaserBeamBlocker>() != null)
                return;
        }

        // 避免站在物件上時往下壓也觸發推動
        if (hit.moveDirection.y < -0.3f) return;

        Vector3 pushDir = hit.moveDirection;
        pushDir.y = 0f;

        float mag = pushDir.magnitude;
        if (onlyPushWithHorizontalMove && mag < minMoveDir) return;

        pushDir /= Mathf.Max(mag, 0.0001f);

        // 水平限速：越接近上限，推力越小
        Vector3 v = body.velocity;
        Vector3 horizontalVel = new Vector3(v.x, 0f, v.z);
        float speed = horizontalVel.magnitude;
        if (speed >= maxPushSpeed) return;

        float speedFactor = 1f - Mathf.Clamp01(speed / maxPushSpeed);
        Vector3 force = pushDir * (pushForce * speedFactor);

        body.AddForce(force, forceMode);
    }
}