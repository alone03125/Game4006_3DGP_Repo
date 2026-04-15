using UnityEngine;

public class DoorCloseTrigger : MonoBehaviour
{
    [SerializeField] private PuzzleDoor targetDoor;
    [SerializeField] private bool oneShot = true;
    private bool used;

    private void OnTriggerEnter(Collider other)
    {
        if (used && oneShot) return;

        // Resolve the top-level object that entered the trigger.
        Transform root = other.transform.root;

        // Only the real player can trigger this door close.
        if (!root.CompareTag("Player")) return;

        targetDoor?.CloseDoor();

        if (oneShot)
            used = true;
    }
}