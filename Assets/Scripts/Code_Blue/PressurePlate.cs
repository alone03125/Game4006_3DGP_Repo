using UnityEngine;
using UnityEngine.Events;

public class PressurePlate : MonoBehaviour
{
    [Header("Events")]
    [SerializeField] private UnityEvent onPressed;
    [SerializeField] private UnityEvent onReleased;

    private int overlapCount;

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("PressurePlate Enter");
        if (!IsValidActor(other)) return;

        overlapCount++;
        if (overlapCount == 1)
            onPressed?.Invoke();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsValidActor(other)) return;

        overlapCount = Mathf.Max(0, overlapCount - 1);
        if (overlapCount == 0)
            onReleased?.Invoke();
    }

    private static bool IsValidActor(Collider other)
    {
        return other.GetComponent<PlayerInteraction>() != null;
    }
}