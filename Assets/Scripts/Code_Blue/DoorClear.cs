using UnityEngine;
using UnityEngine.SceneManagement;

public class DoorClear: MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private string level00SceneName = "Level-00";

    [Header("Filter")]
    [SerializeField] private string playerTag = "Player";

    private void OnTriggerEnter(Collider other)
    {
        Transform root = other.transform.root;
        if (root == null || !root.CompareTag(playerTag)) return;

        if (!Application.CanStreamedLevelBeLoaded(level00SceneName))
        {
            Debug.LogError($"Scene '{level00SceneName}' is not in Build Settings.");
            return;
        }

        SceneManager.LoadScene(level00SceneName);
    }
}