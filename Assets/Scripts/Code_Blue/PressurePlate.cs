using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

public class PressurePlate : MonoBehaviour
{
    [Header("Events")]
    [SerializeField] private UnityEvent onPressed;
    [SerializeField] private UnityEvent onReleased;

    [Header("Button Visual")]
    [Tooltip("被踩下時會下壓的按鈕物件 (例如 botonlow)")]
    [SerializeField] private Transform buttonVisual;

    [Tooltip("up y offset (usually 0)")]
    [SerializeField] private float upYOffset = 0f;

    [Tooltip("down y offset (negative = down)")]
    [SerializeField] private float downYOffset = -0.05f;

    [Tooltip("button movement speed (units per second)")]
    [SerializeField] private float buttonMoveSpeed = 0.5f;

    [Header("SFX")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip pressedSfx;
    [SerializeField] private AudioClip releasedSfx;
    [Range(0f, 1f)]
    [SerializeField] private float sfxVolume = 1f;

    private readonly HashSet<Collider> activeColliders = new();
    private bool isPressed;
    private Collider plateTrigger;
    private readonly List<Collider> staleBuffer = new();
    private Vector3 buttonBaseLocalPosition;

    private void Awake()
    {
        plateTrigger = GetComponent<Collider>();
        if (plateTrigger == null || !plateTrigger.isTrigger)
        {
            Debug.LogError("[Plate] Missing trigger collider on PressurePlate.", this);
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
            }
        }

        if (buttonVisual != null)
            buttonBaseLocalPosition = buttonVisual.localPosition;
    }

    private void Update()
    {
        if (buttonVisual == null) return;

        float yOffset = isPressed ? downYOffset : upYOffset;
        Vector3 target = buttonBaseLocalPosition + new Vector3(0f, yOffset, 0f);

        buttonVisual.localPosition = Vector3.MoveTowards(
            buttonVisual.localPosition,
            target,
            buttonMoveSpeed * Time.deltaTime
        );
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsValidActor(other)) return;
        if (!activeColliders.Add(other)) return;
        RefreshState();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!activeColliders.Remove(other)) return;
        RefreshState();
    }

    private void FixedUpdate()
    {
        if (plateTrigger == null) return;

        staleBuffer.Clear();
        foreach (var c in activeColliders)
        {
            if (c == null) { staleBuffer.Add(c); continue; }
            if (!IsStillOverlapping(c)) staleBuffer.Add(c);
        }

        if (staleBuffer.Count > 0)
        {
            foreach (var c in staleBuffer) activeColliders.Remove(c);
            RefreshState();
        }
    }

    private void RefreshState()
    {
        bool nowPressed = activeColliders.Count > 0;
        if (nowPressed == isPressed) return;
        isPressed = nowPressed;

        if (isPressed)
        {
            PlaySfx(pressedSfx);
            onPressed?.Invoke();
        }
        else
        {
            PlaySfx(releasedSfx);
            onReleased?.Invoke();
        }
    }

    private void PlaySfx(AudioClip clip)
    {
        if (clip == null || audioSource == null) return;
        audioSource.PlayOneShot(clip, sfxVolume);
    }

    private bool IsStillOverlapping(Collider other)
    {
        if (other == null) return false;
        if (!other.enabled || !other.gameObject.activeInHierarchy) return false;
        if (plateTrigger == null) return false;

        return Physics.ComputePenetration(
            plateTrigger, plateTrigger.transform.position, plateTrigger.transform.rotation,
            other, other.transform.position, other.transform.rotation,
            out _, out _);
    }

    private static bool IsValidActor(Collider other)
    {
        var actor = other.GetComponentInParent<InteractionActor>();
        var player = other.GetComponentInParent<PlayerInteraction>();
        if (actor == null || !actor.CanAffectWorldMechanisms) return false;
        return player != null;
    }
}