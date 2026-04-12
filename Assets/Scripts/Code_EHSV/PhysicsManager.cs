using UnityEngine;

public class PhysicsManager : MonoBehaviour
{
    public static PhysicsManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void SetScriptSimulation(bool manual)
    {
        Physics.simulationMode = manual ? SimulationMode.Script : SimulationMode.FixedUpdate;
    }
}