using UnityEngine;

public class ElevatorController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform platform;
    [SerializeField] private Transform topPoint;
    [SerializeField] private Transform bottomPoint;

    [Header("Movement")]
    [SerializeField] private float speed = 2.5f;

    private enum MoveDirection
    {
        Stop = 0,
        Up = 1,
        Down = -1
    }

    private MoveDirection currentDirection = MoveDirection.Stop;

    public void MoveUp()
    {
        currentDirection = MoveDirection.Up;
    }

    public void MoveDown()
    {
        currentDirection = MoveDirection.Down;
    }

    public void StopMove()
    {
        currentDirection = MoveDirection.Stop;
    }

    private void Update()
    {
        if (platform == null || topPoint == null || bottomPoint == null) return;

        if (currentDirection == MoveDirection.Up)
        {
            Vector3 target = topPoint.position;
            platform.position = Vector3.MoveTowards(platform.position, target, speed * Time.deltaTime);

            // 到頂自動停
            if (Vector3.Distance(platform.position, target) < 0.001f)
                currentDirection = MoveDirection.Stop;
        }
        else if (currentDirection == MoveDirection.Down)
        {
            Vector3 target = bottomPoint.position;
            platform.position = Vector3.MoveTowards(platform.position, target, speed * Time.deltaTime);

            // 到底自動停
            if (Vector3.Distance(platform.position, target) < 0.001f)
                currentDirection = MoveDirection.Stop;
        }
    }
}