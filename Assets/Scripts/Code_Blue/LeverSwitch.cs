using UnityEngine;
using UnityEngine.Events;

public class LeverSwitch : MonoBehaviour, IInteractable, IHoldInteractable
{
    [Header("Lever Visual")]
    [SerializeField] private Transform leverVisual;

   
    [SerializeField] private Vector3 topEulerOffset = Vector3.zero;


    [SerializeField] private Vector3 downEulerOffset = new Vector3(30f, 0f, 0f);

    [SerializeField] private float moveSpeed = 120f; // degrees per second

    [Header("Events")]
    [SerializeField] private UnityEvent onHoldStart;
    [SerializeField] private UnityEvent onHoldEnd;

    private bool isHolding;
    private Quaternion baseLocalRotation; // Inspector 起始角度

    private void Awake()
    {
        if (leverVisual == null) leverVisual = transform;
        baseLocalRotation = leverVisual.localRotation;
    }

    private void Update()
    {
        Vector3 offset = isHolding ? downEulerOffset : topEulerOffset;
        Quaternion target = baseLocalRotation * Quaternion.Euler(offset);

        leverVisual.localRotation = Quaternion.RotateTowards(
            leverVisual.localRotation,
            target,
            moveSpeed * Time.deltaTime
        );
    }

    public void Interact()
    {
        BeginHold();
    }

    public void BeginHold()
    {
        if (isHolding) return;
        isHolding = true;
        onHoldStart?.Invoke();
    }

    public void EndHold()
    {
        if (!isHolding) return;
        isHolding = false;
        onHoldEnd?.Invoke();
    }
}