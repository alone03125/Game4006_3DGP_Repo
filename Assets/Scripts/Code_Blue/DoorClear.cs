using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class DoorClear : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private string level00SceneName = "Level-00";

    [Header("Filter")]
    [SerializeField] private string playerTag = "Player";

    [Header("SFX")]
    [Tooltip("audio source, if empty, will automatically create one on this GameObject")]
    [SerializeField] private AudioSource audioSource;

    [Tooltip("when this object is enabled (SetActive=true), play, such as the door clear appears")]
    [SerializeField] private AudioClip activateSfx;

    [Tooltip("player enter, prepare to load scene")]
    [SerializeField] private AudioClip enterSfx;

    [Range(0f, 1f)]
    [SerializeField] private float sfxVolume = 1f;

    [Tooltip("wait for sfx to play before loading scene (seconds). 0 = no wait, load directly.")]
    [SerializeField] private float delayBeforeLoad = 0f;

    private bool triggered;

    private void Awake()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
            }
        }
    }

    private void OnEnable()
    {
        // this object is activated (SetActive(true))
        PlaySfx(activateSfx);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (triggered) return;

        Transform root = other.transform.root;
        if (root == null || !root.CompareTag(playerTag)) return;

        if (!Application.CanStreamedLevelBeLoaded(level00SceneName))
        {
            Debug.LogError($"Scene '{level00SceneName}' is not in Build Settings.");
            return;
        }

        triggered = true;
        PlaySfx(enterSfx);

        if (delayBeforeLoad > 0f)
            StartCoroutine(LoadAfterDelay(level00SceneName, delayBeforeLoad));
        else
            SceneManager.LoadScene(level00SceneName);
    }

    private IEnumerator LoadAfterDelay(string sceneName, float delay)
    {
        yield return new WaitForSeconds(delay);
        SceneManager.LoadScene(sceneName);
    }

    private void PlaySfx(AudioClip clip)
    {
        if (clip == null || audioSource == null) return;
        audioSource.PlayOneShot(clip, sfxVolume);
    }
}