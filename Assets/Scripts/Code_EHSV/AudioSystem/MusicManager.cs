using UnityEngine;
using System.Collections;

/// <summary>
/// 背景音乐独立管理器
/// - 两首音乐：正常音乐 / 时停音乐
/// - 切换时：当前音乐减速暂停，目标音乐加速启动
/// - 均支持循环播放
/// - 主音量 (Master) 与音乐独立音量 (Music) 分离，实际输出 = 音乐独立音量 × 主音量
/// - 任何音量调节立即生效，零延迟（包括在过渡中）
/// </summary>
public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance { get; private set; }

    [Header("Music Clips")]
    [SerializeField] private AudioClip normalMusicClip;
    [SerializeField] private AudioClip timeStopMusicClip;

    [Header("Transition Settings")]
    [SerializeField] private float pitchDropSpeed = 2.5f;
    [SerializeField] private float pitchRiseSpeed = 3f;
    [SerializeField] private float minPitch = 0.1f;
    [SerializeField] private float crossfadeDuration = 1.5f;

    [Header("Volume")]
    [Range(0f, 1f)] public float musicVolume = 0.8f;      // 音乐独立音量
    private float masterVolume = 1f;                      // 全局主音量（外部设置）

    private AudioSource sourceNormal;
    private AudioSource sourceTimeStop;
    private Coroutine transitionCoroutine;

    private MusicState currentState = MusicState.Normal;
    private MusicState targetState = MusicState.Normal;   // 过渡的目标状态

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
        sourceNormal = gameObject.AddComponent<AudioSource>();
        sourceNormal.clip = normalMusicClip;
        sourceNormal.loop = true;
        sourceNormal.playOnAwake = false;
        sourceNormal.volume = GetEffectiveVolume();
        sourceNormal.pitch = 1f;

        sourceTimeStop = gameObject.AddComponent<AudioSource>();
        sourceTimeStop.clip = timeStopMusicClip;
        sourceTimeStop.loop = true;
        sourceTimeStop.playOnAwake = false;
        sourceTimeStop.volume = 0f;
        sourceTimeStop.pitch = 1f;

        if (normalMusicClip != null)
        {
            sourceNormal.Play();
        }
    }

    /// <summary>
    /// 计算实际输出音量 = 音乐独立音量 × 主音量
    /// </summary>
    private float GetEffectiveVolume() => Mathf.Clamp01(musicVolume * masterVolume);

    /// <summary>
    /// 立即将音量应用到当前活跃的音频源（无视任何过渡）
    /// </summary>
    private void ApplyVolumeImmediate()
    {
        // 如果有过渡正在进行，强制终止并直接进入目标状态
        if (transitionCoroutine != null)
        {
            StopCoroutine(transitionCoroutine);
            transitionCoroutine = null;

            // 直接完成到目标状态
            switch (targetState)
            {
                case MusicState.Normal:
                    sourceNormal.volume = GetEffectiveVolume();
                    sourceNormal.pitch = 1f;
                    sourceNormal.UnPause();
                    sourceTimeStop.volume = 0f;
                    sourceTimeStop.pitch = 1f;
                    sourceTimeStop.Pause();
                    currentState = MusicState.Normal;
                    break;
                case MusicState.TimeStop:
                    sourceTimeStop.volume = GetEffectiveVolume();
                    sourceTimeStop.pitch = 1f;
                    sourceTimeStop.Play();
                    sourceNormal.volume = 0f;
                    sourceNormal.pitch = 1f;
                    sourceNormal.Pause();
                    currentState = MusicState.TimeStop;
                    break;
            }
            return;
        }

        // 没有过渡，直接根据当前状态应用音量
        switch (currentState)
        {
            case MusicState.Normal:
                sourceNormal.volume = GetEffectiveVolume();
                break;
            case MusicState.TimeStop:
                sourceTimeStop.volume = GetEffectiveVolume();
                break;
        }
    }

    /// <summary>
    /// 设置音乐独立音量（0～1），立即生效
    /// </summary>
    public void SetVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        ApplyVolumeImmediate();
    }

    /// <summary>
    /// 设置主音量（0～1），立即生效
    /// </summary>
    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
        ApplyVolumeImmediate();
    }

    /// <summary>
    /// 切换至时停音乐
    /// </summary>
    public void TransitionToTimeStop()
    {
        if (currentState == MusicState.TimeStop || timeStopMusicClip == null) return;
        if (transitionCoroutine != null) StopCoroutine(transitionCoroutine);
        transitionCoroutine = StartCoroutine(TransitionToTimeStopRoutine());
    }

    /// <summary>
    /// 切换至正常音乐
    /// </summary>
    public void TransitionToNormal()
    {
        if (currentState == MusicState.Normal || normalMusicClip == null) return;
        if (transitionCoroutine != null) StopCoroutine(transitionCoroutine);
        transitionCoroutine = StartCoroutine(TransitionToNormalRoutine());
    }

    private IEnumerator TransitionToTimeStopRoutine()
    {
        targetState = MusicState.TimeStop;
        currentState = MusicState.Transitioning;

        // 准备时停音乐：从最低音高开始，静音
        sourceTimeStop.volume = 0f;
        sourceTimeStop.pitch = minPitch;
        sourceTimeStop.Play();

        float targetVolume = GetEffectiveVolume();

        while (sourceNormal.pitch > minPitch || sourceNormal.volume > 0f)
        {
            // 正常音乐减速并淡出
            sourceNormal.pitch = Mathf.MoveTowards(sourceNormal.pitch, minPitch, pitchDropSpeed * Time.unscaledDeltaTime);
            sourceNormal.volume = Mathf.Lerp(sourceNormal.volume, 0f, Time.unscaledDeltaTime * (1f / crossfadeDuration));

            // 时停音乐加速并淡入（目标音量为当前有效音量）
            sourceTimeStop.pitch = Mathf.MoveTowards(sourceTimeStop.pitch, 1f, pitchRiseSpeed * Time.unscaledDeltaTime);
            sourceTimeStop.volume = Mathf.Lerp(sourceTimeStop.volume, targetVolume, Time.unscaledDeltaTime * (1f / crossfadeDuration));

            yield return null;
        }

        sourceNormal.volume = 0f;
        sourceNormal.Pause();
        sourceNormal.pitch = 1f;

        sourceTimeStop.volume = targetVolume;
        sourceTimeStop.pitch = 1f;

        currentState = MusicState.TimeStop;
        transitionCoroutine = null;
    }

    private IEnumerator TransitionToNormalRoutine()
    {
        targetState = MusicState.Normal;
        currentState = MusicState.Transitioning;

        // 准备正常音乐：从最低音高开始，静音
        sourceNormal.volume = 0f;
        sourceNormal.pitch = minPitch;
        sourceNormal.UnPause();

        float targetVolume = GetEffectiveVolume();

        while (sourceTimeStop.pitch > minPitch || sourceTimeStop.volume > 0f)
        {
            sourceTimeStop.pitch = Mathf.MoveTowards(sourceTimeStop.pitch, minPitch, pitchDropSpeed * Time.unscaledDeltaTime);
            sourceTimeStop.volume = Mathf.Lerp(sourceTimeStop.volume, 0f, Time.unscaledDeltaTime * (1f / crossfadeDuration));

            sourceNormal.pitch = Mathf.MoveTowards(sourceNormal.pitch, 1f, pitchRiseSpeed * Time.unscaledDeltaTime);
            sourceNormal.volume = Mathf.Lerp(sourceNormal.volume, targetVolume, Time.unscaledDeltaTime * (1f / crossfadeDuration));

            yield return null;
        }

        sourceTimeStop.volume = 0f;
        sourceTimeStop.Pause();
        sourceTimeStop.pitch = 1f;

        sourceNormal.volume = targetVolume;
        sourceNormal.pitch = 1f;

        currentState = MusicState.Normal;
        transitionCoroutine = null;
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
        (currentState == MusicState.Transitioning && targetState == MusicState.TimeStop);
}