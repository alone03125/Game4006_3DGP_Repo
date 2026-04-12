using UnityEngine;
using UnityEngine.Events;

public class LeverSwitch : MonoBehaviour, IInteractable, IHoldInteractable
{
    [Header("Lever Visual")]
    [SerializeField] private Transform leverVisual;
    [SerializeField] private Vector3 topEuler = new Vector3(0f, 0f, 0f);
    [SerializeField] private Vector3 downEuler = new Vector3(30f, 0f, 0f);
    [SerializeField] private float moveSpeed = 120f; // degrees per second

    [Header("Events")]
    [SerializeField] private UnityEvent onHoldStart; // 開始長按（例如 Elevator.MoveDown）
    [SerializeField] private UnityEvent onHoldEnd;   // 放開（例如 Elevator.StopMove）

    private bool isHolding;

    private void Awake()
    {
        if (leverVisual == null) leverVisual = transform;
        leverVisual.localRotation = Quaternion.Euler(topEuler);
    }

    private void Update()
    {
        Quaternion target = Quaternion.Euler(isHolding ? downEuler : topEuler);
        leverVisual.localRotation = Quaternion.RotateTowards(
            leverVisual.localRotation,
            target,
            moveSpeed * Time.deltaTime
        );
    }

    // 保留一般互動相容性：按一下視為開始長按
    public void Interact()
    {
        BeginHold();
    }

    public void BeginHold()
    {
        if (isHolding) return;
        isHolding = true;
        Debug.Log("[LeverSwitch] Hold Start");
        onHoldStart?.Invoke();
    }

    public void EndHold()
    {
        if (!isHolding) return;
        isHolding = false;
        Debug.Log("[LeverSwitch] Hold End");
        onHoldEnd?.Invoke();
    }
}