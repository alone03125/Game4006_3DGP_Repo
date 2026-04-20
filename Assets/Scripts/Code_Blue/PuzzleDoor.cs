using UnityEngine;

public class PuzzleDoor : MonoBehaviour
{
    [SerializeField] private float dropDistance = 3f;
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private bool autoClose = true;
    [SerializeField] private float openDuration = 2f;

    [Header("SFX")]
    [Tooltip("loop sfx audio source, will be forced to loop")]
    [SerializeField] private AudioSource loopAudioSource;

    [Tooltip("one shot sfx audio source, can be shared with loop audio source")]
    [SerializeField] private AudioSource oneShotAudioSource;

    [Tooltip("open start sfx")]
    [SerializeField] private AudioClip openStartSfx;

    [Tooltip("opened sfx")]
    [SerializeField] private AudioClip openedSfx;

    [Tooltip("close start sfx")]
    [SerializeField] private AudioClip closeStartSfx;

    [Tooltip("door closed sfx")]
    [SerializeField] private AudioClip closedSfx;

    [Tooltip("loop sfx")]
    [SerializeField] private AudioClip moveLoopSfx;

    [Range(0f, 1f)]
    [SerializeField] private float sfxVolume = 1f;

    private Vector3 closedPosition;
    private Vector3 openedPosition;
    private float timer = 0f;

    private enum DoorState
    {
        Closed,
        Opening,
        Open,
        Closing
    }

    private DoorState state = DoorState.Closed;

    private void Awake()
    {
        closedPosition = transform.position;
        openedPosition = closedPosition + Vector3.down * dropDistance;

        EnsureAudioSources();
    }

    private void EnsureAudioSources()
    {
        if (loopAudioSource == null)
        {
            loopAudioSource = gameObject.AddComponent<AudioSource>();
            loopAudioSource.playOnAwake = false;
        }
        loopAudioSource.loop = true;

        if (oneShotAudioSource == null)
        {
            oneShotAudioSource = gameObject.AddComponent<AudioSource>();
            oneShotAudioSource.playOnAwake = false;
            oneShotAudioSource.loop = false;
        }
    }

    public void OpenDoor()
    {
        if (state == DoorState.Open || state == DoorState.Opening)
        {
            if (autoClose) timer = openDuration;
            return;
        }

        state = DoorState.Opening;
        PlayOneShot(openStartSfx);
        StartLoopSfx();
    }

    public void CloseDoor()
    {
        if (state == DoorState.Closed || state == DoorState.Closing)
            return;

        timer = 0f;
        state = DoorState.Closing;
        PlayOneShot(closeStartSfx);
        StartLoopSfx();
    }

    private void Update()
    {
        switch (state)
        {
            case DoorState.Opening:
                transform.position = Vector3.MoveTowards(
                    transform.position,
                    openedPosition,
                    moveSpeed * Time.deltaTime
                );

                if (Vector3.Distance(transform.position, openedPosition) < 0.01f)
                {
                    transform.position = openedPosition;
                    state = DoorState.Open;
                    if (autoClose) timer = openDuration;

                    StopLoopSfx();
                    PlayOneShot(openedSfx);
                }
                break;

            case DoorState.Open:
                if (!autoClose) break;
                timer -= Time.deltaTime;

                if (timer <= 0f)
                {
                    state = DoorState.Closing;
                    PlayOneShot(closeStartSfx);
                    StartLoopSfx();
                }
                break;

            case DoorState.Closing:
                transform.position = Vector3.MoveTowards(
                    transform.position,
                    closedPosition,
                    moveSpeed * Time.deltaTime
                );

                if (Vector3.Distance(transform.position, closedPosition) < 0.01f)
                {
                    transform.position = closedPosition;
                    state = DoorState.Closed;

                    StopLoopSfx();
                    PlayOneShot(closedSfx);
                }
                break;
        }
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