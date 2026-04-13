using UnityEngine;
using UnityEngine.Events;

public class Lever3StateSwitch : MonoBehaviour, IInteractable
{
    public enum LeverState
    {
        Forward = 0,
        Middle = 1,
        Backward = 2
    }

    [Header("State")]
    [SerializeField] private LeverState currentState = LeverState.Middle;
    [SerializeField] private float cooldown = 0.2f;

    [Header("Optional Visual Pivot")]
    [SerializeField] private Transform leverVisual;

    [Tooltip("Relative to Inspector initial angle offset")]
    [SerializeField] private Vector3 forwardEulerOffset = new Vector3(-25f, 0f, 0f);
    [SerializeField] private Vector3 middleEulerOffset = Vector3.zero;
    [SerializeField] private Vector3 backwardEulerOffset = new Vector3(25f, 0f, 0f);

    [Header("Events")]
    [SerializeField] private UnityEvent onForward;
    [SerializeField] private UnityEvent onMiddle;
    [SerializeField] private UnityEvent onBackward;

    private float lastUseTime = -999f;
    private int stepDirection = 1;
    private Quaternion baseLocalRotation; // Inspector 起始角度

    private void Awake()
    {
        if (leverVisual == null) leverVisual = transform;
        baseLocalRotation = leverVisual.localRotation;
    }

    private void Start()
    {
        ApplyState(currentState, true);
    }

    public void Interact()
    {
        if (Time.time - lastUseTime < cooldown) return;
        lastUseTime = Time.time;

        int next = (int)currentState + stepDirection;

        if (next >= 2)
        {
            next = 2;
            stepDirection = -1;
        }
        else if (next <= 0)
        {
            next = 0;
            stepDirection = 1;
        }

        ApplyState((LeverState)next, false);
    }

    private void ApplyState(LeverState newState, bool isInit)
    {
        currentState = newState;

        if (leverVisual != null)
        {
            Vector3 offset = middleEulerOffset;
            if (currentState == LeverState.Forward) offset = forwardEulerOffset;
            else if (currentState == LeverState.Backward) offset = backwardEulerOffset;

            leverVisual.localRotation = baseLocalRotation * Quaternion.Euler(offset);
        }

        if (currentState == LeverState.Forward) onForward?.Invoke();
        else if (currentState == LeverState.Middle) onMiddle?.Invoke();
        else onBackward?.Invoke();
    }
}