using UnityEngine;

public class Lazer : MonoBehaviour
{
    [Header("Player Respawn")]
    [SerializeField] private Transform respawnPoint;

    [Header("Safety")]
    [SerializeField] private float hitCooldown = 0.1f;

    private float _nextHitTime;
    private TraceCloneManager _traceMgr;
    private CloneManager _cloneMgr;

    private void Awake()
    {
        _traceMgr = FindAnyObjectByType<TraceCloneManager>();
        _cloneMgr = FindAnyObjectByType<CloneManager>();
            }

    private void OnTriggerEnter(Collider other)
    {
        if (Time.time < _nextHitTime) return;
        _nextHitTime = Time.time + hitCooldown;

        Transform root = other.transform.root;
        if (root == null) return;

        // 1) TracePhantom: Discard recording
        if (root.CompareTag("TracePhantom"))
        {
            if (_traceMgr != null) _traceMgr.ForceExitClean();
            else Destroy(root.gameObject); // 保底
            Debug.Log("Lazer hit TracePhantom -> discard recording");
            return;
        }

        // 2) Stop Time VisionClone
        if (root.CompareTag("VisionClone"))
        {
            if (_cloneMgr != null) _cloneMgr.ForceExitTimeStop(false);
            else Destroy(root.gameObject); // 保底
            Debug.Log("Lazer hit VisionClone -> removed");
            return;
        }

        // 3) Delete TraceClone
        if (root.CompareTag("TraceClone"))
        {
            Destroy(root.gameObject);
            Debug.Log("Lazer hit TraceClone -> removed");
            return;
        }

        // 4) Teleport Player to Respawn Point
        if (root.CompareTag("Player"))
        {
            TeleportToRespawn(root);
            Debug.Log("Lazer hit Player -> respawn");
        }
    }

    private void TeleportToRespawn(Transform targetRoot)
    {
        if (respawnPoint == null)
        {
            Debug.LogWarning("Respawn point is not assigned on Lazer.");
            return;
        }

        CharacterController cc = targetRoot.GetComponent<CharacterController>();
        Rigidbody rb = targetRoot.GetComponent<Rigidbody>();

        if (cc != null) cc.enabled = false;

        targetRoot.position = respawnPoint.position;
        targetRoot.rotation = respawnPoint.rotation;

        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (cc != null)
        {
            cc.enabled = true;
            cc.Move(Vector3.zero);
        }
    }
}