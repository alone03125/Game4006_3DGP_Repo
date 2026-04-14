using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelPortal : MonoBehaviour
{
    [Serializable]
    public struct LevelEntry
    {
        public int levelId;        // 1~10
        public string sceneName;   // 例如 Level1
    }

    [Header("Level Mapping")]
    [SerializeField] private List<LevelEntry> levels = new();

    [Header("Safety")]
    [SerializeField] private float triggerCooldown = 0.5f;

    private float nextAllowedTime;

    public void LoadLevelById(int levelId)
    {
        if (Time.time < nextAllowedTime) return;
        nextAllowedTime = Time.time + triggerCooldown;

        for (int i = 0; i < levels.Count; i++)
        {
            if (levels[i].levelId != levelId) continue;

            if (string.IsNullOrWhiteSpace(levels[i].sceneName))
            {
                Debug.LogWarning($"Level {levelId} sceneName is empty.");
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(levels[i].sceneName))
            {
                Debug.LogError($"Scene '{levels[i].sceneName}' not in Build Settings.");
                return;
            }

            SceneManager.LoadScene(levels[i].sceneName);
            return;
        }

        Debug.LogWarning($"No scene mapping for levelId: {levelId}");
    }


    public void LoadLevel1() => LoadLevelById(1);
    public void LoadLevel2() => LoadLevelById(2);
    public void LoadLevel3() => LoadLevelById(3);
    public void LoadLevel4() => LoadLevelById(4);
    public void LoadLevel5() => LoadLevelById(5);
    public void LoadLevel6() => LoadLevelById(6);
    public void LoadLevel7() => LoadLevelById(7);
    public void LoadLevel8() => LoadLevelById(8);
    public void LoadLevel9() => LoadLevelById(9);
    public void LoadLevel10() => LoadLevelById(10);
}