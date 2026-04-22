using UnityEngine;
using System.Collections;

/// <summary>
/// 背景音乐独立管理器
/// - 两首音乐：正常音乐 / 时停音乐
/// - 切换时：当前音乐减速暂停，目标音乐加速启动
/// - 均支持循环播放
/// </summary>
public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance { get; private set; }

    [Header("Music Clips")]
    [SerializeField] private AudioClip normalMusicClip;
    [SerializeField] private AudioClip timeStopMusicClip;

    [Header("Transition Settings")]
    [SerializeField] private float pitchDropSpeed = 2.5f;      // 减速速率
    [SerializeField] private float pitchRiseSpeed = 3f;        // 加速速率
    [SerializeField] private float minPitch = 0.1f;            // 减速最低音高
    [SerializeField] private float crossfadeDuration = 1.5f;   // 淡入淡出时间

    [Header("Volume")]
    [Range(0f, 1f)] public float musicVolume = 0.8f;

    private AudioSource sourceNormal;
    private AudioSource sourceTimeStop;
    private Coroutine transitionCoroutine;
    private MusicState currentState = MusicState.Normal;

    private enum MusicState
    {
        Normal,
        TimeStop,
        Transitioning
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        CreateAudioSources();
    }

    private void CreateAudioSources()
    {
        // 正常音乐源
        sourceNormal = gameObject.AddComponent<AudioSource>();
        sourceNormal.clip = normalMusicClip;
        sourceNormal.loop = true;
        sourceNormal.playOnAwake = false;
        sourceNormal.volume = musicVolume;
        sourceNormal.pitch = 1f;

        // 时停音乐源
        sourceTimeStop = gameObject.AddComponent<AudioSource>();
        sourceTimeStop.clip = timeStopMusicClip;
        sourceTimeStop.loop = true;
        sourceTimeStop.playOnAwake = false;
        sourceTimeStop.volume = 0f;
        sourceTimeStop.pitch = 1f;

        // 默认开始正常音乐
        if (normalMusicClip != null)
        {
            sourceNormal.Play();
        }
    }

    /// <summary>
    /// 切换至时停音乐（正常音乐减速暂停，时停音乐加速启动）
    /// </summary>
    public void TransitionToTimeStop()
    {
        if (currentState == MusicState.TimeStop || timeStopMusicClip == null) return;
        if (transitionCoroutine != null) StopCoroutine(transitionCoroutine);
        transitionCoroutine = StartCoroutine(TransitionToTimeStopRoutine());
    }

    /// <summary>
    /// 切换至正常音乐（时停音乐减速暂停，正常音乐加速启动）
    /// </summary>
    public void TransitionToNormal()
    {
        if (currentState == MusicState.Normal || normalMusicClip == null) return;
        if (transitionCoroutine != null) StopCoroutine(transitionCoroutine);
        transitionCoroutine = StartCoroutine(TransitionToNormalRoutine());
    }

    private IEnumerator TransitionToTimeStopRoutine()
    {
        currentState = MusicState.Transitioning;

        // 确保时停音乐准备好（从头播放，静音）
        sourceTimeStop.volume = 0f;
        sourceTimeStop.pitch = minPitch;
        sourceTimeStop.Play();

        // 正常音乐减速并淡出
        float normalStartPitch = sourceNormal.pitch;
        float normalStartVolume = sourceNormal.volume;

        while (sourceNormal.pitch > minPitch || sourceNormal.volume > 0f)
        {
            // 减速
            sourceNormal.pitch = Mathf.MoveTowards(sourceNormal.pitch, minPitch, pitchDropSpeed * Time.unscaledDeltaTime);
            // 淡出
            sourceNormal.volume = Mathf.Lerp(sourceNormal.volume, 0f, Time.unscaledDeltaTime * (1f / crossfadeDuration));

            // 时停音乐加速并淡入
            sourceTimeStop.pitch = Mathf.MoveTowards(sourceTimeStop.pitch, 1f, pitchRiseSpeed * Time.unscaledDeltaTime);
            sourceTimeStop.volume = Mathf.Lerp(sourceTimeStop.volume, musicVolume, Time.unscaledDeltaTime * (1f / crossfadeDuration));

            yield return null;
        }

        sourceNormal.volume = 0f;
        sourceNormal.Pause();
        sourceNormal.pitch = 1f; // 重置以备下次使用

        sourceTimeStop.volume = musicVolume;
        sourceTimeStop.pitch = 1f;

        currentState = MusicState.TimeStop;
        transitionCoroutine = null;
    }

    private IEnumerator TransitionToNormalRoutine()
    {
        currentState = MusicState.Transitioning;

        sourceNormal.volume = 0f;
        sourceNormal.pitch = minPitch;
        sourceNormal.UnPause();

        while (sourceTimeStop.pitch > minPitch || sourceTimeStop.volume > 0f)
        {
            sourceTimeStop.pitch = Mathf.MoveTowards(sourceTimeStop.pitch, minPitch, pitchDropSpeed * Time.unscaledDeltaTime);
            sourceTimeStop.volume = Mathf.Lerp(sourceTimeStop.volume, 0f, Time.unscaledDeltaTime * (1f / crossfadeDuration));

            sourceNormal.pitch = Mathf.MoveTowards(sourceNormal.pitch, 1f, pitchRiseSpeed * Time.unscaledDeltaTime);
            sourceNormal.volume = Mathf.Lerp(sourceNormal.volume, musicVolume, Time.unscaledDeltaTime * (1f / crossfadeDuration));

            yield return null;
        }

        sourceTimeStop.volume = 0f;
        sourceTimeStop.Pause();
        sourceTimeStop.pitch = 1f;

        sourceNormal.volume = musicVolume;
        sourceNormal.pitch = 1f;

        currentState = MusicState.Normal;
        transitionCoroutine = null;
    }

    /// <summary>
    /// 设置整体音量
    /// </summary>
    public void SetVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        if (currentState == MusicState.Normal)
            sourceNormal.volume = musicVolume;
        else if (currentState == MusicState.TimeStop)
            sourceTimeStop.volume = musicVolume;
    }

    /// <summary>
    /// 立即停止所有音乐
    /// </summary>
    public void StopAll()
    {
        if (transitionCoroutine != null)
            StopCoroutine(transitionCoroutine);
        sourceNormal.Stop();
        sourceTimeStop.Stop();
    }

    public bool IsInTimeStopMusic() => currentState == MusicState.TimeStop ||
        (currentState == MusicState.Transitioning && transitionCoroutine != null);
}