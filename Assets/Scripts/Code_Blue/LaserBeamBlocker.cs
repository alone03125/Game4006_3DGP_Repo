using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class LaserBeamBlocker : MonoBehaviour
{
    [Header("Pushable")]
    [SerializeField] private bool addRigidbodyIfMissing = true;
    [SerializeField] private bool useGravity = false;
    [SerializeField] private bool freezeRotation = true;
    [SerializeField] private float mass = 20f;

    [Header("Layer Check (Optional)")]
    [SerializeField] private string expectedLayerName = "LaserBlock";

    private void Reset()
    {
        SetupCollider();
        SetupRigidbody();
    }

    private void Awake()
    {
        SetupCollider();
        SetupRigidbody();
        CheckLayer();
    }

    private void SetupCollider()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            // 擋雷射需要實心碰撞，不是 Trigger
            col.isTrigger = false;
        }
    }

    private void SetupRigidbody()
    {
        Rigidbody rb = GetComponent<Rigidbody>();

        if (rb == null && addRigidbodyIfMissing)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }

        if (rb == null) return;

        rb.mass = Mathf.Max(0.1f, mass);
        rb.useGravity = useGravity;
        rb.isKinematic = false;

        if (freezeRotation)
        {
            rb.constraints = RigidbodyConstraints.FreezeRotation;
        }
    }

    private void CheckLayer()
    {
        if (string.IsNullOrWhiteSpace(expectedLayerName)) return;

        int expected = LayerMask.NameToLayer(expectedLayerName);
        if (expected < 0) return; // 專案沒建立這個 layer 就略過

        if (gameObject.layer != expected)
        {
            Debug.LogWarning(
                $"{name}: Suggest to set Layer to {expectedLayerName} for Laser Raycast to block correctly.",
                this
            );
        }
    }
}