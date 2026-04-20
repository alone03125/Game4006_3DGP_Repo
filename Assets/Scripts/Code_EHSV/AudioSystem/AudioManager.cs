using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 全局音效管理器 - 单例模式
/// 管理所有游戏音效的播放，支持音效为空的情况
/// </summary>
public class AudioManager : MonoBehaviour
{
    #region Singleton
    public static AudioManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        InitializeAudioSources();
    }
    #endregion

    [Header("Audio Sources")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource footstepSource;
    [SerializeField] private AudioSource voiceSource;
    [SerializeField] private AudioSource ambientSource;

    [Header("Player Movement Sounds")]
    [SerializeField] private FootstepSounds[] footstepSounds;
    [SerializeField] private AudioClip defaultFootstepClip;
    [SerializeField] private AudioClip landingClip;
    [SerializeField] private AudioClip jumpClip;
    [Range(0, 1)] public float footstepVolume = 0.7f;
    [Range(0.5f, 2f)] public float footstepPitchVariation = 0.1f;

    [Header("Clone Ability Sounds")]
    [SerializeField] private AudioClip timeStopEnterClip;
    [SerializeField] private AudioClip timeStopExitClip;
    [SerializeField] private AudioClip timeStopForceExitClip;
    [SerializeField] private AudioClip cloneSolidifyClip;
    [SerializeField] private AudioClip cloneSwapClip;
    [SerializeField] private AudioClip cloneDisappearClip;
    [Range(0, 1)] public float cloneSoundVolume = 0.8f;

    [Header("Trace Clone Sounds")]
    [SerializeField] private AudioClip traceCloneFootstepClip;
    [SerializeField] private AudioClip traceCloneLandingClip;
    [SerializeField] private AudioReverbPreset traceReverbPreset = AudioReverbPreset.Hallway;
    [Range(0, 1)] public float traceCloneVolume = 0.6f;

    [Header("Interaction Sounds (Reserved)")]
    [SerializeField] private AudioClip interactSuccessClip;
    [SerializeField] private AudioClip interactFailClip;
    [SerializeField] private AudioClip deathClip;
    [SerializeField] private AudioClip levelCompleteClip;
    [SerializeField] private AudioClip respawnClip;
    [Range(0, 1)] public float interactionVolume = 0.8f;

    [Header("Settings")]
    [SerializeField] private float defaultPitch = 1f;
    [SerializeField] private float forceExitPitch = 0.6f;

    private RaycastHit groundHit;
    private string currentGroundTag = "Untagged";
    private PlayerController playerController;

    private float footstepTimer = 0f;
    private const float WALK_FOOTSTEP_INTERVAL = 0.5f;
    private const float SPRINT_FOOTSTEP_INTERVAL = 0.35f;
    private const float CROUCH_FOOTSTEP_INTERVAL = 0.7f;

    private List<AudioSource> audioSourcePool = new List<AudioSource>();
    private const int POOL_SIZE = 8;

    private void InitializeAudioSources()
    {
        for (int i = 0; i < POOL_SIZE; i++)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            audioSourcePool.Add(source);
        }

        if (sfxSource == null)
            sfxSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();

        if (footstepSource == null)
            footstepSource = gameObject.AddComponent<AudioSource>();
    }

    private void Start()
    {
        playerController = FindObjectOfType<PlayerController>();
    }

    private void Update()
    {
        HandleFootsteps();
    }

    #region Footstep System

    private void HandleFootsteps()
    {
        if (playerController == null) return;
        if (!playerController.Controller.isGrounded) return;
        if (playerController.freezeGravity) return;

        float speed = playerController.GetCurrentHorizontalSpeed();
        float maxSpeed = playerController.CurrentMaxSpeed;

        if (speed < 0.1f) return;

        float speedRatio = speed / maxSpeed;
        float interval = Mathf.Lerp(WALK_FOOTSTEP_INTERVAL, SPRINT_FOOTSTEP_INTERVAL, speedRatio);

        footstepTimer -= Time.deltaTime;
        if (footstepTimer <= 0f)
        {
            PlayFootstep();
            footstepTimer = interval;
        }
    }

    public void PlayFootstep()
    {
        if (footstepSource == null) return;

        AudioClip clip = GetFootstepClipForCurrentGround();
        if (clip == null) clip = defaultFootstepClip;
        if (clip == null) return;

        footstepSource.pitch = defaultPitch + Random.Range(-footstepPitchVariation, footstepPitchVariation);
        footstepSource.volume = footstepVolume;
        footstepSource.PlayOneShot(clip);
    }

    private AudioClip GetFootstepClipForCurrentGround()
    {
        if (playerController == null) return defaultFootstepClip;

        if (Physics.Raycast(playerController.transform.position, Vector3.down, out groundHit, 2f))
        {
            currentGroundTag = groundHit.collider.tag;

            foreach (var fs in footstepSounds)
            {
                if (fs.groundTag == currentGroundTag)
                    return fs.GetRandomClip();
            }
        }
        return defaultFootstepClip;
    }

    public void PlayLandingSound(float velocity)
    {
        if (landingClip == null) return;

        float volume = Mathf.Clamp01(Mathf.Abs(velocity) / 15f) * footstepVolume;
        sfxSource.pitch = defaultPitch;
        sfxSource.PlayOneShot(landingClip, volume);
    }

    public void PlayJumpSound()
    {
        if (jumpClip == null) return;
        sfxSource.PlayOneShot(jumpClip, footstepVolume);
    }

    #endregion

    #region Clone Ability Sounds

    public void PlayTimeStopEnter()
    {
        if (timeStopEnterClip == null) return;
        PlayOnSFXSource(timeStopEnterClip, cloneSoundVolume);
    }

    public void PlayTimeStopExit()
    {
        if (timeStopExitClip == null) return;
        PlayOnSFXSource(timeStopExitClip, cloneSoundVolume);
    }

    public void PlayTimeStopForceExit()
    {
        AudioClip clip = timeStopForceExitClip ?? timeStopExitClip;
        if (clip == null) return;

        AudioSource source = GetAvailableAudioSource();
        source.pitch = forceExitPitch;
        source.volume = cloneSoundVolume;
        source.PlayOneShot(clip);
    }

    public void PlayCloneSolidify()
    {
        if (cloneSolidifyClip == null) return;
        PlayOnSFXSource(cloneSolidifyClip, cloneSoundVolume);
    }

    public void PlayCloneSwap()
    {
        if (cloneSwapClip == null) return;
        PlayOnSFXSource(cloneSwapClip, cloneSoundVolume);
    }

    public void PlayCloneDisappear()
    {
        if (cloneDisappearClip == null) return;
        PlayOnSFXSource(cloneDisappearClip, cloneSoundVolume);
    }

    #endregion

    #region Trace Clone Sounds

    public void PlayTraceCloneFootstep(Vector3 position)
    {
        AudioClip clip = traceCloneFootstepClip ?? defaultFootstepClip;
        if (clip == null) return;

        PlaySoundAtPosition(clip, position, traceCloneVolume, traceReverbPreset);
    }

    public void PlayTraceCloneLanding(Vector3 position, float velocity)
    {
        AudioClip clip = traceCloneLandingClip ?? landingClip;
        if (clip == null) return;

        float volume = Mathf.Clamp01(Mathf.Abs(velocity) / 15f) * traceCloneVolume;
        PlaySoundAtPosition(clip, position, volume, traceReverbPreset);
    }

    #endregion

    #region Interaction Sounds (Reserved Interfaces)

    public void PlayInteractSuccess()
    {
        if (interactSuccessClip == null) return;
        PlayOnSFXSource(interactSuccessClip, interactionVolume);
    }

    public void PlayInteractFail()
    {
        if (interactFailClip == null) return;
        PlayOnSFXSource(interactFailClip, interactionVolume);
    }

    public void PlayDeathSound()
    {
        if (deathClip == null) return;
        PlayOnSFXSource(deathClip, 1f);
    }

    public void PlayRespawnSound()
    {
        if (respawnClip == null) return;
        PlayOnSFXSource(respawnClip, 1f);
    }

    public void PlayLevelComplete()
    {
        if (levelCompleteClip == null) return;
        PlayOnSFXSource(levelCompleteClip, 1f);
    }

    #endregion

    #region Utility Methods

    private void PlayOnSFXSource(AudioClip clip, float volume)
    {
        if (sfxSource == null) return;
        sfxSource.pitch = defaultPitch;
        sfxSource.PlayOneShot(clip, volume);
    }

    private AudioSource GetAvailableAudioSource()
    {
        foreach (var source in audioSourcePool)
        {
            if (!source.isPlaying)
            {
                source.pitch = defaultPitch;
                return source;
            }
        }
        return audioSourcePool[0];
    }

    private void PlaySoundAtPosition(AudioClip clip, Vector3 position, float volume, AudioReverbPreset reverb = AudioReverbPreset.Off)
    {
        if (clip == null) return;

        GameObject tempGO = new GameObject("TempAudio");
        tempGO.transform.position = position;

        AudioSource source = tempGO.AddComponent<AudioSource>();
        source.clip = clip;
        source.volume = volume;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        source.maxDistance = 30f;

        if (reverb != AudioReverbPreset.Off)
        {
            AudioReverbZone reverbZone = tempGO.AddComponent<AudioReverbZone>();
            reverbZone.reverbPreset = reverb;
        }

        source.Play();
        Destroy(tempGO, clip.length + 0.5f);
    }

    public void SetMusicVolume(float volume)
    {
        if (musicSource != null)
            musicSource.volume = Mathf.Clamp01(volume);
    }

    public void SetSFXVolume(float volume)
    {
        if (sfxSource != null)
            sfxSource.volume = Mathf.Clamp01(volume);
        if (footstepSource != null)
            footstepSource.volume = footstepVolume * Mathf.Clamp01(volume);
    }

    public void PlayMusic(AudioClip clip, bool loop = true)
    {
        if (musicSource == null || clip == null) return;
        musicSource.clip = clip;
        musicSource.loop = loop;
        musicSource.Play();
    }

    public void StopMusic()
    {
        if (musicSource != null)
            musicSource.Stop();
    }

    #endregion

    [System.Serializable]
    public class FootstepSounds
    {
        public string groundTag;
        public AudioClip[] clips;

        public AudioClip GetRandomClip()
        {
            if (clips == null || clips.Length == 0) return null;
            return clips[Random.Range(0, clips.Length)];
        }
    }
}