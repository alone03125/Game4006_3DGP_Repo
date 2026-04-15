using UnityEngine;
using UnityEngine.Events;

public class XnorGatePlate : MonoBehaviour
{
    [Header("Output Events")]
    [SerializeField] private UnityEvent onXnorTrue;   // Fires when A == B
    [SerializeField] private UnityEvent onXnorFalse;  // Fires when A != B

    [Header("Options")]
    [SerializeField] private bool invokeInitialState = true;
    [SerializeField] private bool requireAtLeastOnePressed = false;
    // If true, state (A=false, B=false) will be treated as false.

    private bool plateAActive;
    private bool plateBActive;

    private bool lastOutput;
    private bool hasOutput;

    private void Start()
    {
        if (invokeInitialState)
            EvaluateState(forceInvoke: true);
    }

    public void SetPlateAState(bool active)
    {
        plateAActive = active;
        EvaluateState();
    }

    public void SetPlateBState(bool active)
    {
        plateBActive = active;
        EvaluateState();
    }

    private void EvaluateState(bool forceInvoke = false)
    {
        // XNOR is true when both inputs are equal.
        bool xnor = (plateAActive == plateBActive);

        // Optional filter to ignore the both-false case.
        if (requireAtLeastOnePressed && !plateAActive && !plateBActive)
            xnor = false;

        // Invoke only on state change (or forced).
        if (!forceInvoke && hasOutput && xnor == lastOutput)
            return;

        hasOutput = true;
        lastOutput = xnor;

        if (xnor) onXnorTrue?.Invoke();
        else onXnorFalse?.Invoke();
    }
}