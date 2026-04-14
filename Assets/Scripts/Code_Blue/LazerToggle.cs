using UnityEngine;

public class LazerToggle : MonoBehaviour
{
    [SerializeField] private GameObject laserRoot;

    private void Awake()
    {
        if (laserRoot == null)
            laserRoot = gameObject;
    }

    public void TurnOff()
    {
        if (laserRoot != null)
            laserRoot.SetActive(false);
    }

    public void TurnOn()
    {
        if (laserRoot != null)
            laserRoot.SetActive(true);
    }
}