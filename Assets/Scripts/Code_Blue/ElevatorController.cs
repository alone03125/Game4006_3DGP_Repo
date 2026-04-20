using UnityEngine;

public class ElevatorController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform platform;
    [SerializeField] private Transform topPoint;
    [SerializeField] private Transform bottomPoint;

    [Header("Movement")]
    [SerializeField] private float speed = 2.5f;

    [Header("SFX")]
    [Tooltip("loop sfx audio source, recommended to set Loop = true")]
    [SerializeField] private AudioSource loopAudioSource;

    [Tooltip("one shot sfx audio source, can be shared with loop audio source")]
    [SerializeField] private AudioSource oneShotAudioSource;

    [Tooltip("start move sfx")]
    [SerializeField] private AudioClip startSfx;

    [Tooltip("loop sfx")]
    [SerializeField] private AudioClip moveLoopSfx;

    [Tooltip("arrive sfx")]
    [SerializeField] private AudioClip arriveSfx;

    [Range(0f, 1f)]
    [SerializeField] private float sfxVolume = 1f;

    private enum MoveDirection
    {
        Stop = 0,
        Up = 1,
        Down = -1
    }

    private MoveDirection currentDirection = MoveDirection.Stop;

    private void Awake()
    {
        EnsureAudioSources();
    }

    private void EnsureAudioSources()
    {
        if (loopAudioSource == null)
        {
            loopAudioSource = gameObject.AddComponent<AudioSource>();
            loopAudioSource.playOnAwake = false;
            loopAudioSource.loop = true;
        }
        else
        {
            loopAudioSource.loop = true;
            loopAudioSource.playOnAwake = false;
        }

        if (oneShotAudioSource == null)
        {
            oneShotAudioSource = gameObject.AddComponent<AudioSource>();
            oneShotAudioSource.playOnAwake = false;
            oneShotAudioSource.loop = false;
        }
    }

    public void MoveUp()
    {
        TryStartMove(MoveDirection.Up);
    }

    public void MoveDown()
    {
        TryStartMove(MoveDirection.Down);
    }

    public void StopMove()
    {
        if (currentDirection == MoveDirection.Stop) return;
        currentDirection = MoveDirection.Stop;
        StopLoopSfx();
    }

    private void TryStartMove(MoveDirection dir)
    {
        if (currentDirection == dir) return; // 同方向持續按不重複觸發音效

        currentDirection = dir;
        PlayOneShot(startSfx);
        StartLoopSfx();
    }

    private void Update()
    {
        if (platform == null || topPoint == null || bottomPoint == null) return;

        if (currentDirection == MoveDirection.Up)
        {
            Vector3 target = topPoint.position;
            platform.position = Vector3.MoveTowards(platform.position, target, speed * Time.deltaTime);

            if (Vector3.Distance(platform.position, target) < 0.001f)
                OnArrived();
        }
        else if (currentDirection == MoveDirection.Down)
        {
            Vector3 target = bottomPoint.position;
            platform.position = Vector3.MoveTowards(platform.position, target, speed * Time.deltaTime);

            if (Vector3.Distance(platform.position, target) < 0.001f)
                OnArrived();
        }
    }

    private void OnArrived()
    {
        currentDirection = MoveDirection.Stop;
        StopLoopSfx();
        PlayOneShot(arriveSfx);
    }

    private void StartLoopSfx()
    {
        if (moveLoopSfx == null || loopAudioSource == null) return;
        if (loopAudioSource.isPlaying && loopAudioSource.clip == moveLoopSfx) return;

        loopAudioSource.clip = moveLoopSfx;
        loopAudioSource.volume = sfxVolume;
        loopAudioSource.loop = true;
        loopAudioSource.Play();
    }

    private void StopLoopSfx()
    {
        if (loopAudioSource == null) return;
        if (loopAudioSource.isPlaying) loopAudioSource.Stop();
    }

    private void PlayOneShot(AudioClip clip)
    {
        if (clip == null || oneShotAudioSource == null) return;
        oneShotAudioSource.PlayOneShot(clip, sfxVolume);
    }
}