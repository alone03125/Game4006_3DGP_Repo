using UnityEngine;
using UnityEngine.Events;

public class Lever3StateSwitch : MonoBehaviour, IInteractable
{
    public enum LeverState
    {
        Forward = 0, // 前段 => 上升
        Middle = 1,  // 中段 => 停止
        Backward = 2 // 後段 => 下降
    }

    [Header("State")]
    [SerializeField] private LeverState currentState = LeverState.Middle;
    [SerializeField] private float cooldown = 0.2f;

    [Header("Optional Visual Pivot")]
    [SerializeField] private Transform leverVisual;
    [SerializeField] private Vector3 forwardEuler = new Vector3(-25f, 0f, 0f);
    [SerializeField] private Vector3 middleEuler  = new Vector3(0f, 0f, 0f);
    [SerializeField] private Vector3 backwardEuler= new Vector3(25f, 0f, 0f);

    [Header("Events")]
    [SerializeField] private UnityEvent onForward;  // Elevator MoveUp
    [SerializeField] private UnityEvent onMiddle;   // Elevator StopMove
    [SerializeField] private UnityEvent onBackward; // Elevator MoveDown

    private float lastUseTime = -999f;
    private int stepDirection = 1;  // 1: 往後段走(前->中->後), -1: 往前段走(後->中->前)

    private void Start()
    {
        ApplyState(currentState, true);
    }

    public void Interact()
    {
        if (Time.time - lastUseTime < cooldown) return;
        lastUseTime = Time.time;
        // Forward=0, Middle=1, Backward=2
        int next = (int)currentState + stepDirection;
        // 撞到邊界就反轉方向
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
        LeverState oldState = currentState;
        currentState = newState;

        Debug.Log($"[Lever3StateSwitch] State changed: {oldState} -> {currentState} (isInit={isInit})");

        // 更新視覺角度（可選）
        if (leverVisual != null)
        {
            Vector3 euler = middleEuler;
            if (currentState == LeverState.Forward) euler = forwardEuler;
            else if (currentState == LeverState.Backward) euler = backwardEuler;

            leverVisual.localRotation = Quaternion.Euler(euler);
        }

        // 觸發對應事件
        if (currentState == LeverState.Forward) onForward?.Invoke();
        else if (currentState == LeverState.Middle) onMiddle?.Invoke();
        else onBackward?.Invoke();
    }
}