using UnityEngine;

public class PuzzleDoor : MonoBehaviour
{
    [SerializeField] private float dropDistance = 3f;   
    [SerializeField] private float moveSpeed = 2f;   
    [SerializeField] private bool autoClose = true;   
    [SerializeField] private float openDuration = 2f;   


    private Vector3 closedPosition;
    private Vector3 openedPosition;
    private float timer = 0f;

    private enum DoorState
    {
        Closed,
        Opening,
        Open,
        Closing
    }

    private DoorState state = DoorState.Closed;

    private void Awake()
    {
        closedPosition = transform.position;
        openedPosition = closedPosition + Vector3.down * dropDistance;
    }

    public void OpenDoor()
    {
        // If already open or opening, reset timer and return.
        if (state == DoorState.Open || state == DoorState.Opening)
        {
            if (autoClose)
            {
                timer = openDuration;
            }
            return;
        }

        state = DoorState.Opening;
    }

    public void CloseDoor()
    {
        // Ignore if already fully closed or currently closing.
        if (state == DoorState.Closed || state == DoorState.Closing)
            return;
        // Force immediate close from current position.
        timer = 0f;
        state = DoorState.Closing;
    }


    private void Update()
    {
        switch (state)
        {
            case DoorState.Opening:
                transform.position = Vector3.MoveTowards(
                    transform.position,
                    openedPosition,
                    moveSpeed * Time.deltaTime
                );

              if (Vector3.Distance(transform.position, openedPosition) < 0.01f)
                {
                    transform.position = openedPosition;
                    state = DoorState.Open;
                    if (autoClose) timer = openDuration;
                }
                break;

            case DoorState.Open:
                if (!autoClose) break; // Keep door open until CloseDoor() is called.
                timer -= Time.deltaTime;

                if (timer <= 0f)
                    state = DoorState.Closing;
                break;

            case DoorState.Closing:
                transform.position = Vector3.MoveTowards(
                    transform.position,
                    closedPosition,
                    moveSpeed * Time.deltaTime
                );

                if (Vector3.Distance(transform.position, closedPosition) < 0.01f)
                {
                    transform.position = closedPosition;
                    state = DoorState.Closed;
                }
                break;
        }
    }
}