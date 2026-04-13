using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

public class PressurePlate : MonoBehaviour
{
    [Header("Events")]
    [SerializeField] private UnityEvent onPressed;
    [SerializeField] private UnityEvent onReleased;

    private int overlapCount;
    private readonly HashSet<Collider> activeColliders = new();
    private bool isPressed;

    private void OnTriggerEnter(Collider other)
    {
        // Debug.Log("PressurePlate Enter");
        // Debug.Log("Overlap: " + overlapCount);
        // Debug.Log("IsValidActor: " + IsValidActor(other));
        // Debug.Log("PlayerInteraction: " + other.GetComponentInParent<PlayerInteraction>());
        // Debug.Log("InteractionActor: " + other.GetComponentInParent<InteractionActor>());
        // Debug.Log("--------------------------------");

        if (!IsValidActor(other)) return;
        if (!activeColliders.Add(other)) return;
        RefreshState();
       
    }

    private void OnTriggerExit(Collider other)
    {
        Debug.Log("PressurePlate Exit");
        if (!activeColliders.Remove(other)) return; // Only process previously entered
        RefreshState();

   
    }


    private void FixedUpdate()
    {
        // When clone is destroyed, the Unity object reference will become null
        if (activeColliders.RemoveWhere(c => c == null) > 0)
            RefreshState();
    }

    private void RefreshState()
    {
        bool nowPressed = activeColliders.Count > 0;
        if (nowPressed == isPressed) return;
        isPressed = nowPressed;
        if (isPressed) onPressed?.Invoke();
        else onReleased?.Invoke();
    }

    private static bool IsValidActor(Collider other)
    {
        var actor = other.GetComponentInParent<InteractionActor>();
        if (actor == null || !actor.CanAffectWorldMechanisms) return false;

        return other.GetComponentInParent<PlayerInteraction>() != null;
    }
}