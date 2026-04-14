using UnityEngine;

public class DeadZone : MonoBehaviour
{
    [Header("Filter")]
    [SerializeField] private string playerTag = "Player";

    [Header("Respawn")]
    [SerializeField] private Transform respawnPoint;

    [Header("Safety")]
    [SerializeField] private float triggerCooldown = 0.15f;
    private float nextAllowedTime = 0f;

    private void OnTriggerEnter(Collider other)
    {
        if (Time.time < nextAllowedTime) return;
        nextAllowedTime = Time.time + triggerCooldown;

        Transform root = other.transform.root;
        if (root == null || !root.CompareTag(playerTag)) return;

        Debug.Log("You are dead");

        TeleportToRespawn(root);
    }

    private void TeleportToRespawn(Transform targetRoot)
    {
        if (respawnPoint == null)
        {
            Debug.LogWarning("DeadZone respawnPoint is not assigned.");
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