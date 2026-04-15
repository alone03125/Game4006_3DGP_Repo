using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class LevelReset
{
    public static IEnumerator ReloadCurrentLevel(float delaySeconds)
    {
        
        Time.timeScale = 1f;

        if (delaySeconds > 0f)
            yield return new WaitForSecondsRealtime(delaySeconds);
        else
            yield return null;

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}