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
        Debug.Log($"[Plate] Enter: {other.name}, root={other.transform.root.name}");
        
        bool valid = IsValidActor(other);
        
        Debug.Log($"[Plate] IsValidActor={valid}");
        if (!valid) return;
        if (!activeColliders.Add(other))
        {
            Debug.Log("[Plate] Collider already tracked.");
            return;
        }
        
        Debug.Log($"[Plate] Added collider. Count={activeColliders.Count}");
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
        Debug.Log($"[Plate] RefreshState nowPressed={nowPressed}, isPressed={isPressed}, count={activeColliders.Count}");
        if (nowPressed == isPressed) return;
        isPressed = nowPressed;
        if (isPressed)
        {
            Debug.Log("[Plate] Invoke onPressed");
            onPressed?.Invoke();
        }
        else
        {
            Debug.Log("[Plate] Invoke onReleased");
            onReleased?.Invoke();
        }
    }

    private static bool IsValidActor(Collider other)
    {
        var actor = other.GetComponentInParent<InteractionActor>();
        var player = other.GetComponentInParent<PlayerInteraction>();
        Debug.Log($"[Plate] actor={(actor != null)}, canAffect={(actor != null && actor.CanAffectWorldMechanisms)}, player={(player != null)}");
        if (actor == null || !actor.CanAffectWorldMechanisms) return false;
        return player != null;
    }
}