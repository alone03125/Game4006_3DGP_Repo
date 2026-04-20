using System.Collections.Generic;
using UnityEngine;

// 掛在一個帶 Trigger Collider 的「子物件」上 (例如 SphereCollider 包住互動物)。
// 玩家或任何 Clone 進入 → 父層 InteractableHighlight.AddHighlight()；離開則 Remove。
// 會在 FixedUpdate 主動清除被 Destroy / Disable 的 actor，避免 outline 卡住。
[RequireComponent(typeof(Collider))]
public class InteractableProximityTrigger : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private InteractableHighlight target;

    [Header("Filter")]
    [Tooltip("任何一個 root tag 命中就算有效；預設包含 Player 與所有 Clone 變體")]
    [SerializeField]
    private string[] validActorTags = new[]
    {
        "Player",
        "Clone",
        "VisionClone",
        "SolidClone",
        "TraceClone",
        "TracePhantom"
    };

    // 追蹤目前在範圍內的 actor root (存 Transform 以便偵測 null / disabled)
    private readonly HashSet<Transform> _activeRoots = new();
    private readonly List<Transform> _staleBuffer = new();

    private Collider _myTrigger;
    private int _addedCount;

    private void Awake()
    {
        if (target == null) target = GetComponentInParent<InteractableHighlight>();

        _myTrigger = GetComponent<Collider>();
        if (_myTrigger != null && !_myTrigger.isTrigger) _myTrigger.isTrigger = true;
    }

    private void OnDisable()    => ForceReleaseAll();
    private void OnDestroy()    => ForceReleaseAll();

    private void OnTriggerEnter(Collider other)
    {
        if (target == null) return;
        Transform root = GetActorRoot(other);
        if (root == null) return;

        if (_activeRoots.Add(root))
        {
            target.AddHighlight();
            _addedCount++;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (target == null) return;
        Transform root = GetActorRoot(other);
        if (root == null) return;

        if (_activeRoots.Remove(root))
        {
            target.RemoveHighlight();
            _addedCount = Mathf.Max(0, _addedCount - 1);
        }
    }

    // 每物理幀清除「已被 Destroy / Disable / 不再重疊」的 actor。
    // 這是解決 Clone 消失後 outline 卡住的關鍵。
    private void FixedUpdate()
    {
        if (target == null || _activeRoots.Count == 0) return;

        _staleBuffer.Clear();
        foreach (var root in _activeRoots)
        {
            // Transform 被銷毀時 == null 會回 true (Unity 的 fake-null)
            if (root == null) { _staleBuffer.Add(root); continue; }
            if (!root.gameObject.activeInHierarchy) { _staleBuffer.Add(root); continue; }

            // (可選) 更嚴格：確認 root 還真的在我們的 trigger 範圍內
            // if (!IsOverlappingRoot(root)) _staleBuffer.Add(root);
        }

        if (_staleBuffer.Count == 0) return;

        for (int i = 0; i < _staleBuffer.Count; i++)
        {
            _activeRoots.Remove(_staleBuffer[i]);
            target.RemoveHighlight();
            _addedCount = Mathf.Max(0, _addedCount - 1);
        }
    }

    private void ForceReleaseAll()
    {
        if (target != null && _addedCount > 0)
            for (int i = 0; i < _addedCount; i++) target.RemoveHighlight();

        _addedCount = 0;
        _activeRoots.Clear();
        _staleBuffer.Clear();
    }

    private Transform GetActorRoot(Collider other)
    {
        if (other == null) return null;
        Transform root = other.transform.root;
        if (root == null) return null;

        for (int i = 0; i < validActorTags.Length; i++)
        {
            string t = validActorTags[i];
            if (!string.IsNullOrEmpty(t) && root.CompareTag(t))
                return root;
        }
        return null;
    }
}