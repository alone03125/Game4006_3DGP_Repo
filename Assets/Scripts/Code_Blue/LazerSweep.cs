using UnityEngine;

public class LazerSweep : MonoBehaviour
{
    [Header("Translate")]
    [SerializeField] private Vector3 localAxis = Vector3.right;
    [SerializeField] private float distance = 3f;
    [SerializeField] private float speed = 1f;

    private Vector3 _startLocalPos;

    void Start()
    {
        _startLocalPos = transform.localPosition;
    }

    void Update()
    {
        float offset = Mathf.PingPong(Time.time * speed, distance);
        Vector3 dir = localAxis.sqrMagnitude > 0f ? localAxis.normalized : Vector3.right;
        transform.localPosition = _startLocalPos + dir * offset;
    }
}