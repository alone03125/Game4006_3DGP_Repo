using UnityEngine;

public class TrapdoorRotate : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform trapdoorPlaceholder;
    [SerializeField] private Transform anchor;

    [Header("Rotation")]
    [SerializeField] private float openAngle = 90f;
    [SerializeField] private float rotateSpeed = 180f;

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
    [SerializeField] private AudioClip rotateLoopSfx;

    [Range(0f, 1f)]
    [SerializeField] private float sfxVolume = 1f;

    private Vector3 closedPosition;
    private Quaternion closedRotation;
    private float currentAngle;
    private float targetAngle;
    private bool wasMoving;

    private void Awake()
    {
        if (trapdoorPlaceholder == null) trapdoorPlaceholder = transform;
        if (anchor == null) anchor = transform;

        closedPosition = trapdoorPlaceholder.position;
        closedRotation = trapdoorPlaceholder.rotation;
        currentAngle = 0f;
        targetAngle = 0f;

        ApplyRotation(0f);
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

    private void Update()
    {
        bool isMoving = !Mathf.Approximately(currentAngle, targetAngle);

        currentAngle = Mathf.MoveTowards(currentAngle, targetAngle, rotateSpeed * Time.deltaTime);
        ApplyRotation(currentAngle);

        // 判斷「剛抵達目標」的那一幀
        if (wasMoving && Mathf.Approximately(currentAngle, targetAngle))
        {
            StopLoopSfx();
            if (Mathf.Approximately(targetAngle, openAngle))
                PlayOneShot(openedSfx);
            else if (Mathf.Approximately(targetAngle, 0f))
                PlayOneShot(closedSfx);
        }

        wasMoving = isMoving;
    }

    public void OnPressed()
    {
        if (Mathf.Approximately(targetAngle, openAngle)) return; // 已經是開的目標,不重複觸發
        targetAngle = openAngle;
        PlayOneShot(openStartSfx);
        StartLoopSfx();
    }

    public void OnReleased()
    {
        if (Mathf.Approximately(targetAngle, 0f)) return; // 已經是關的目標,不重複觸發
        targetAngle = 0f;
        PlayOneShot(closeStartSfx);
        StartLoopSfx();
    }

    public void SetPressed(bool pressed)
    {
        if (pressed) OnPressed();
        else OnReleased();
    }

    private void ApplyRotation(float angle)
    {
        Vector3 worldAxis = anchor.TransformDirection(Vector3.forward);
        Quaternion delta = Quaternion.AngleAxis(angle, worldAxis);

        Vector3 offsetFromAnchor = closedPosition - anchor.position;
        trapdoorPlaceholder.position = anchor.position + delta * offsetFromAnchor;
        trapdoorPlaceholder.rotation = delta * closedRotation;
    }

    private void StartLoopSfx()
    {
        if (rotateLoopSfx == null || loopAudioSource == null) return;
        if (loopAudioSource.isPlaying && loopAudioSource.clip == rotateLoopSfx) return;

        loopAudioSource.clip = rotateLoopSfx;
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