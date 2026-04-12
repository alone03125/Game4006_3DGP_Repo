using UnityEngine;

public class TimeStopManager : MonoBehaviour
{
    public static TimeStopManager Instance { get; private set; }

    [Header("Trigger Time Stop")]
    public KeyCode toggleKey = KeyCode.F;

    [SerializeField] private bool _isTimeStopped;

    public bool IsTimeStopped => _isTimeStopped;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            Time.timeScale = 1f;
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            Toggle();
    }

    public void Toggle()
    {
        if (_isTimeStopped)
            Resume();
        else
            StopTime();
    }

    public void StopTime()
    {
        _isTimeStopped = true;
        Time.timeScale = 0f;
        Debug.Log("Time is stopped");
        Debug.Log("Global time scale: " + Time.timeScale);
    }

    public void Resume()
    {
        _isTimeStopped = false;
        Time.timeScale = 1f;
        Debug.Log("Time is resumed");
        Debug.Log("Global time scale: " + Time.timeScale);
    }
}