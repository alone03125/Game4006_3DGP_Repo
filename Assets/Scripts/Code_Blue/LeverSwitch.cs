using UnityEngine;
using UnityEngine.Events;

public class LeverSwitch : MonoBehaviour, IInteractable, IHoldInteractable
{
    [Header("Lever Visual")]
    [SerializeField] private Transform leverVisual;

    [Tooltip("relative to initial position when not pressed (local)")]
    [SerializeField] private float topZOffset = 0f;

    [Tooltip("relative to initial position when pressed (local)")]
    [SerializeField] private float downZOffset = -0.1f;

    [Tooltip("lever movement speed (units per second)")]
    [SerializeField] private float moveSpeed = 1f;

    [Header("SFX")]
    [Tooltip("audio source, if empty, will automatically create one on this GameObject")]
    [SerializeField] private AudioSource audioSource;

    [Tooltip("hold start sfx")]
    [SerializeField] private AudioClip holdStartSfx;

    [Tooltip("hold end sfx")]
    [SerializeField] private AudioClip holdEndSfx;

    [Range(0f, 1f)]
    [SerializeField] private float sfxVolume = 1f;

    [Header("Events")]
    [SerializeField] private UnityEvent onHoldStart;
    [SerializeField] private UnityEvent onHoldEnd;

    private bool isHolding;
    private Vector3 baseLocalPosition;

    private void Awake()
    {
        if (leverVisual == null) leverVisual = transform;
        baseLocalPosition = leverVisual.localPosition;

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

    private void Update()
    {
        float zOffset = isHolding ? downZOffset : topZOffset;
        Vector3 target = baseLocalPosition + new Vector3(0f, 0f, zOffset);

        leverVisual.localPosition = Vector3.MoveTowards(
            leverVisual.localPosition,
            target,
            moveSpeed * Time.deltaTime
        );
    }

    public void Interact()
    {
        BeginHold();
    }

    public void BeginHold()
    {
        if (isHolding) return;
        isHolding = true;
        PlaySfx(holdStartSfx);
        onHoldStart?.Invoke();
    }

    public void EndHold()
    {
        if (!isHolding) return;
        isHolding = false;
        PlaySfx(holdEndSfx);
        onHoldEnd?.Invoke();
    }

    private void PlaySfx(AudioClip clip)
    {
        if (clip == null || audioSource == null) return;
        audioSource.PlayOneShot(clip, sfxVolume);
    }
}