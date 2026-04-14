using UnityEngine;

public class PuzzleDoor : MonoBehaviour
{
    [SerializeField] private float dropDistance = 3f;   // 向下移動幾公尺
    [SerializeField] private float dropSpeed = 2f;      // 每秒移動速度
    [SerializeField] private bool disableAfterDrop = true;

    private bool opening = false;
    private Vector3 targetPosition;

    public void OpenDoor()
    {
        if (opening) return;
        opening = true;
        targetPosition = transform.position + Vector3.down * dropDistance;
    }

    private void Update()
    {
        if (!opening) return;

        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPosition,
            dropSpeed * Time.deltaTime
        );

        if (Vector3.Distance(transform.position, targetPosition) < 0.01f)
        {
            transform.position = targetPosition;
            opening = false;

            if (disableAfterDrop)
                gameObject.SetActive(false); // 或改成 Destroy(gameObject);
        }
    }
}