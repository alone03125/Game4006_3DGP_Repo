using UnityEngine;

public class LazerRotate : MonoBehaviour
{
    public enum LocalAxis
    {
        X,
        Y,
        Z
    }

    [Header("Sweep rotation (local Euler, relative to Start pose)")]
    [SerializeField] private LocalAxis axis = LocalAxis.Y;
    [SerializeField] private float minAngle = -30f;
    [SerializeField] private float maxAngle = 30f;
    [SerializeField] private float speed = 1f;

    private Vector3 _startLocalEuler;

    void Start()
    {
        _startLocalEuler = transform.localEulerAngles;
    }

    void Update()
    {
        float delta = Mathf.Lerp(minAngle, maxAngle, Mathf.PingPong(Time.time * speed, 1f));
        Vector3 e = _startLocalEuler;
        switch (axis)
        {
            case LocalAxis.X:
                e.x = _startLocalEuler.x + delta;
                break;
            case LocalAxis.Y:
                e.y = _startLocalEuler.y + delta;
                break;
            case LocalAxis.Z:
                e.z = _startLocalEuler.z + delta;
                break;
        }

        transform.localEulerAngles = e;
    }
}