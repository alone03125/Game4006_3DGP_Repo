using UnityEngine;
using UnityEngine.Events;

public class AndGatePlate : MonoBehaviour
{
    [Header("Output Events")]
    [SerializeField] private UnityEvent onBothPressed;
    [SerializeField] private UnityEvent onAnyReleased;

    private bool plateAActive;
    private bool plateBActive;
    private bool bothWasPressed;

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

    private void EvaluateState()
    {
        bool bothPressed = plateAActive && plateBActive;

        if (bothPressed && !bothWasPressed)
        {
            bothWasPressed = true;
            onBothPressed?.Invoke();
        }
        else if (!bothPressed && bothWasPressed)
        {
            bothWasPressed = false;
            onAnyReleased?.Invoke();
        }
    }
}