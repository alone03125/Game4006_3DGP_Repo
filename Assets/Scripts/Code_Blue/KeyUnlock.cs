using UnityEngine;
using UnityEngine.Events;

public class KeyUnlock : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject doorClearObject;

    [Header("Filter")]
    [SerializeField] private string playerTag = "Player";

    [Header("Audio")]
    [Tooltip("拖一個音效進來，被撿到時會自動播放")]
    [SerializeField] private AudioClip pickupSfx;

    [Range(0f, 1f)]
    [SerializeField] private float sfxVolume = 1f;

    [Header("Events")]
    [SerializeField] private UnityEvent onPickedUp;

    private bool pickedUp;

    private void OnTriggerEnter(Collider other)
    {
        if (pickedUp) return;

        Transform root = other.transform.root;
        if (root == null || !root.CompareTag(playerTag)) return;

        pickedUp = true;

        if (doorClearObject != null)
            doorClearObject.SetActive(true);

        if (pickupSfx != null)
            AudioSource.PlayClipAtPoint(pickupSfx, transform.position, sfxVolume);

        onPickedUp?.Invoke();

        Destroy(gameObject);
    }
}