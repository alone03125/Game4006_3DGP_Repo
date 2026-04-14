using UnityEngine;

public class KeyUnlock: MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject doorClearObject; // 通關門物件（原本先關閉）

    [Header("Filter")]
    [SerializeField] private string playerTag = "Player";

    private bool pickedUp;

    private void OnTriggerEnter(Collider other)
    {
        if (pickedUp) return;

        Transform root = other.transform.root;
        if (root == null || !root.CompareTag(playerTag)) return;

        pickedUp = true;

        if (doorClearObject != null)
            doorClearObject.SetActive(true); // 啟用通關門

        Destroy(gameObject); // Key 消失
    }
}