using UnityEngine;

public class PuzzleDoor : MonoBehaviour
{
    [SerializeField] private float openAngle = 90f;
    [SerializeField] private float openSpeed = 120f; // degrees/sec

    private bool opening = false;
    private Quaternion targetRotation;

    public void OpenDoor()
    {
        if (opening) return;
        opening = true;
        targetRotation = transform.rotation * Quaternion.Euler(0f, openAngle, 0f);
    }

    private void Update()
    {
        if (!opening) return;

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            openSpeed * Time.deltaTime
        );

        if (Quaternion.Angle(transform.rotation, targetRotation) < 0.1f)
        {
            transform.rotation = targetRotation;
            opening = false;
        }
    }
}