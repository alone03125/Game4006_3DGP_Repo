using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 全局音效管理器 - 单例模式
/// - 移除所有序列化 AudioSource，统一使用 AudioClip 随机池
/// - 背景音乐已分离至 MusicManager
/// - 所有音效支持随机池（可配置多个 Clip）
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
        InitializeAudioSourcePool();
    }
    #endregion

    [Header("Volume Settings")]
    [Range(0, 1)] public float masterVolume = 1f;
    [Range(0, 1)] public float sfxVolume = 0.8f;
    [Range(0, 1)] public float footstepVolume = 0.7f;
    [Range(0, 1)] public float traceCloneVolume = 0.6f;

    [Header("Player Movement - Random Pools")]
    [SerializeField] private FootstepSoundGroup[] footstepGroups; // 按地面标签分组
    [SerializeField] private AudioClip[] defaultFootstepClips;
    [SerializeField] private AudioClip[] landingClips;
    [SerializeField] private AudioClip[] jumpClips;
    [Range(0.5f, 2f)] public float footstepPitchVariation = 0.1f;

    [Header("Clone Ability - Random Pools")]
    [SerializeField] private AudioClip[] timeStopEnterClips;
    [SerializeField] private AudioClip[] timeStopExitClips;
    [SerializeField] private AudioClip[] timeStopForceExitClips;
    [SerializeField] private AudioClip[] cloneSolidifyClips;
    [SerializeField] private AudioClip[] cloneSwapClips;
    [SerializeField] private AudioClip[] cloneDisappearClips;

    [Header("Trace Clone - Random Pools")]
    [SerializeField] private AudioClip[] traceCloneFootstepClips;
    [SerializeField] private AudioClip[] traceCloneLandingClips;
    [SerializeField] private AudioReverbPreset traceReverbPreset = AudioReverbPreset.Hallway;

    [Header("Interaction (Reserved) - Random Pools")]
    [SerializeField] private AudioClip[] interactSuccessClips;
    [SerializeField] private AudioClip[] interactFailClips;
    [SerializeField] private AudioClip[] deathClips;
    [SerializeField] private AudioClip[] levelCompleteClips;
    [SerializeField] private AudioClip[] respawnClips;

    [Header("Settings")]
    [SerializeField] private float defaultPitch = 1f;
    [SerializeField] private float forceExitPitch = 0.6f;

    // 音频源池
    private List<AudioSource> audioSourcePool = new List<AudioSource>();
    private const int POOL_SIZE = 12;

    // 玩家引用
    private PlayerController playerController;
    private float footstepTimer = 0f;
    private const float WALK_FOOTSTEP_INTERVAL = 0.5f;
    private const float SPRINT_FOOTSTEP_INTERVAL = 0.35f;
    private RaycastHit groundHit;
    private string currentGroundTag = "Untagged";

    private void InitializeAudioSourcePool()
    {
        for (int i = 0; i < POOL_SIZE; i++)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f; // 默认2D音效
            audioSourcePool.Add(source);
        }
    }

    private void Start()
    {
        playerController = FindObjectOfType<PlayerController>();
    }

    private void Update()
    {
        HandlePlayerFootsteps();
    }

    #region Player Footsteps (with Random Pool)

    private void HandlePlayerFootsteps()
    {
        if (playerController == null) return;
        if (!playerController.Controller.isGrounded) return;
        if (playerController.freezeGravity) return;

        float speed = playerController.GetCurrentHorizontalSpeed();
        if (speed < 0.1f) return;

        float maxSpeed = playerController.CurrentMaxSpeed;
        float speedRatio = speed / maxSpeed;
        float interval = Mathf.Lerp(WALK_FOOTSTEP_INTERVAL, SPRINT_FOOTSTEP_INTERVAL, speedRatio);

        footstepTimer -= Time.deltaTime;
        if (footstepTimer <= 0f)
        {
            PlayPlayerFootstep();
            footstepTimer = interval;
        }
    }

    private void PlayPlayerFootstep()
    {
        AudioClip clip = GetFootstepClipForCurrentGround();
        if (clip == null) return;

        PlaySoundWithRandomPitch(clip, footstepVolume * sfxVolume, 0f);
    }

    private AudioClip GetFootstepClipForCurrentGround()
    {
        if (playerController == null) return GetRandomClip(defaultFootstepClips);

        if (Physics.Raycast(playerController.transform.position, Vector3.down, out groundHit, 2f))
        {
            currentGroundTag = groundHit.collider.tag;
            foreach (var group in footstepGroups)
            {
                if (group.groundTag == currentGroundTag)
                    return group.GetRandomClip();
            }
        }
        return GetRandomClip(defaultFootstepClips);
    }

    public void PlayLandingSound(float velocity)
    {
        AudioClip clip = GetRandomClip(landingClips);
        if (clip == null) return;

        float vol = Mathf.Clamp01(Mathf.Abs(velocity) / 15f) * footstepVolume * sfxVolume;
        PlaySound(clip, vol, defaultPitch);
    }

    public void PlayJumpSound()
    {
        AudioClip clip = GetRandomClip(jumpClips);
        if (clip == null) return;

        PlaySound(clip, footstepVolume * sfxVolume, defaultPitch);
    }

    #endregion

    #region Clone Ability Sounds (with Random Pools)

    public void PlayTimeStopEnter()
    {
        AudioClip clip = GetRandomClip(timeStopEnterClips);
        if (clip == null) return;
        PlaySound(clip, sfxVolume, defaultPitch);

        // 通知音乐管理器切换
        if (MusicManager.Instance != null)
            MusicManager.Instance.TransitionToTimeStop();
    }

    public void PlayTimeStopExit()
    {
        AudioClip clip = GetRandomClip(timeStopExitClips);
        if (clip == null) return;
        PlaySound(clip, sfxVolume, defaultPitch);

        // 通知音乐管理器切换回正常
        if (MusicManager.Instance != null)
            MusicManager.Instance.TransitionToNormal();
    }

    public void PlayTimeStopForceExit()
    {
        AudioClip[] pool = timeStopForceExitClips.Length > 0 ? timeStopForceExitClips : timeStopExitClips;
        AudioClip clip = GetRandomClip(pool);
        if (clip == null) return;

        PlaySound(clip, sfxVolume, forceExitPitch);
    }

    public void PlayCloneSolidify()
    {
        AudioClip clip = GetRandomClip(cloneSolidifyClips);
        if (clip == null) return;
        PlaySound(clip, sfxVolume, defaultPitch);
    }

    public void PlayCloneSwap()
    {
        AudioClip clip = GetRandomClip(cloneSwapClips);
        if (clip == null) return;
        PlaySound(clip, sfxVolume, defaultPitch);
    }

    public void PlayCloneDisappear()
    {
        AudioClip clip = GetRandomClip(cloneDisappearClips);
        if (clip == null) return;
        PlaySound(clip, sfxVolume, defaultPitch);
    }

    #endregion

    #region Trace Clone Sounds (with Random Pools + Spatial)

    public void PlayTraceCloneFootstep(Vector3 position)
    {
        AudioClip clip = GetRandomClip(traceCloneFootstepClips.Length > 0 ? traceCloneFootstepClips : defaultFootstepClips);
        if (clip == null) return;

        PlaySoundAtPosition(clip, position, traceCloneVolume * sfxVolume, traceReverbPreset);
    }

    public void PlayTraceCloneLanding(Vector3 position, float velocity)
    {
        AudioClip clip = GetRandomClip(traceCloneLandingClips.Length > 0 ? traceCloneLandingClips : landingClips);
        if (clip == null) return;

        float vol = Mathf.Clamp01(Mathf.Abs(velocity) / 15f) * traceCloneVolume * sfxVolume;
        PlaySoundAtPosition(clip, position, vol, traceReverbPreset);
    }

    #endregion

    #region Interaction Sounds (Reserved, with Random Pools)

    public void PlayInteractSuccess()
    {
        AudioClip clip = GetRandomClip(interactSuccessClips);
        if (clip == null) return;
        PlaySound(clip, sfxVolume, defaultPitch);
    }

    public void PlayInteractFail()
    {
        AudioClip clip = GetRandomClip(interactFailClips);
        if (clip == null) return;
        PlaySound(clip, sfxVolume, defaultPitch);
    }

    public void PlayDeathSound()
    {
        AudioClip clip = GetRandomClip(deathClips);
        if (clip == null) return;
        PlaySound(clip, sfxVolume, defaultPitch);
    }

    public void PlayRespawnSound()
    {
        AudioClip clip = GetRandomClip(respawnClips);
        if (clip == null) return;
        PlaySound(clip, sfxVolume, defaultPitch);
    }

    public void PlayLevelComplete()
    {
        AudioClip clip = GetRandomClip(levelCompleteClips);
        if (clip == null) return;
        PlaySound(clip, sfxVolume, defaultPitch);
    }

    #endregion

    #region Core Playback Methods

    /// <summary>
    /// 播放2D音效（无空间定位）
    /// </summary>
    private void PlaySound(AudioClip clip, float volume, float pitch)
    {
        if (clip == null) return;

        AudioSource source = GetAvailableAudioSource();
        source.spatialBlend = 0f;
        source.pitch = pitch;
        source.PlayOneShot(clip, volume * masterVolume);
    }

    /// <summary>
    /// 播放带随机音高的2D音效（用于脚步声）
    /// </summary>
    private void PlaySoundWithRandomPitch(AudioClip clip, float volume, float spatialBlend = 0f)
    {
        if (clip == null) return;

        AudioSource source = GetAvailableAudioSource();
        source.spatialBlend = spatialBlend;
        source.pitch = defaultPitch + Random.Range(-footstepPitchVariation, footstepPitchVariation);
        source.PlayOneShot(clip, volume * masterVolume);
    }

    /// <summary>
    /// 在指定位置播放3D音效
    /// </summary>
    private void PlaySoundAtPosition(AudioClip clip, Vector3 position, float volume, AudioReverbPreset reverb = AudioReverbPreset.Off)
    {
        if (clip == null) return;

        GameObject tempGO = new GameObject("TempAudio");
        tempGO.transform.position = position;

        AudioSource source = tempGO.AddComponent<AudioSource>();
        source.clip = clip;
        source.volume = volume * masterVolume;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        source.maxDistance = 30f;
        source.pitch = defaultPitch;

        if (reverb != AudioReverbPreset.Off)
        {
            AudioReverbZone reverbZone = tempGO.AddComponent<AudioReverbZone>();
            reverbZone.reverbPreset = reverb;
        }

        source.Play();
        Destroy(tempGO, clip.length + 0.5f);
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
        // 若全忙，返回第一个（会中断最早的声音）
        return audioSourcePool[0];
    }

    /// <summary>
    /// 从数组中随机获取一个Clip，若数组为空返回null
    /// </summary>
    private AudioClip GetRandomClip(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0) return null;
        return clips[Random.Range(0, clips.Length)];
    }

    #endregion

    #region Volume Control

    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
    }

    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
    }

    #endregion

    [System.Serializable]
    public class FootstepSoundGroup
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