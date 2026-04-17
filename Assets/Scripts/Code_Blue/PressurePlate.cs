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
    private Collider plateTrigger;
    private readonly List<Collider> staleBuffer = new();

    private void Awake()
    {
        plateTrigger = GetComponent<Collider>();
        if (plateTrigger == null || !plateTrigger.isTrigger)
        {
            Debug.LogError("[Plate] Missing trigger collider on PressurePlate.", this);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Debug.Log($"[Plate] Enter: {other.name}, root={other.transform.root.name}");
        
        bool valid = IsValidActor(other);
        
        // Debug.Log($"[Plate] IsValidActor={valid}");
        if (!valid) return;
        if (!activeColliders.Add(other))
        {
            // Debug.Log("[Plate] Collider already tracked.");
            return;
        }
        
        // Debug.Log($"[Plate] Added collider. Count={activeColliders.Count}");
        RefreshState();
       
    }

    private void OnTriggerExit(Collider other)
    {
        // Debug.Log("PressurePlate Exit");
        if (!activeColliders.Remove(other)) return; // Only process previously entered
        RefreshState();

   
    }


    private void FixedUpdate()
    {
        if (plateTrigger == null) return;

        staleBuffer.Clear();

        foreach (var c in activeColliders)
        {
            if (c == null)
            {
                Debug.Log("[Plate] Cleanup null collider.");
                staleBuffer.Add(c);
                continue;
            }

            if (!IsStillOverlapping(c))
            {
                Debug.Log($"[Plate] Cleanup stale collider: {c.name}, root={c.transform.root.name}");
                staleBuffer.Add(c);
            }
        }

        if (staleBuffer.Count > 0)
        {
            foreach (var c in staleBuffer)
                activeColliders.Remove(c);

            Debug.Log($"[Plate] Removed {staleBuffer.Count} stale colliders. Count={activeColliders.Count}");
            RefreshState();
        }
    }

   private void RefreshState()
    {
        bool nowPressed = activeColliders.Count > 0;
        // Debug.Log($"[Plate] RefreshState nowPressed={nowPressed}, isPressed={isPressed}, count={activeColliders.Count}");
        if (nowPressed == isPressed) return;
        isPressed = nowPressed;
        if (isPressed)
        {
            // Debug.Log("[Plate] Invoke onPressed");
            onPressed?.Invoke();
        }
        else
        {
            // Debug.Log("[Plate] Invoke onReleased");
            onReleased?.Invoke();
        }
    }

    private bool IsStillOverlapping(Collider other)
    {
        if (other == null) return false;
        if (!other.enabled || !other.gameObject.activeInHierarchy) return false;
        if (plateTrigger == null) return false;

        bool overlaps = Physics.ComputePenetration(
            plateTrigger, plateTrigger.transform.position, plateTrigger.transform.rotation,
            other, other.transform.position, other.transform.rotation,
            out _, out _);

        if (!overlaps)
        {
            Debug.Log($"[Plate] Not overlapping anymore: {other.name}, root={other.transform.root.name}");
        }

        return overlaps;
    }

    private static bool IsValidActor(Collider other)
    {
        var actor = other.GetComponentInParent<InteractionActor>();
        var player = other.GetComponentInParent<PlayerInteraction>();
        // Debug.Log($"[Plate] actor={(actor != null)}, canAffect={(actor != null && actor.CanAffectWorldMechanisms)}, player={(player != null)}");
        if (actor == null || !actor.CanAffectWorldMechanisms) return false;
        return player != null;
    }
}