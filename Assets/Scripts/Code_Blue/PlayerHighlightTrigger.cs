using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PlayerHighlightTrigger : MonoBehaviour
{
    // 追蹤目前在範圍內的高亮物件,避免重複 / 漏掉
    private readonly HashSet<InteractableHighlight> _inside = new HashSet<InteractableHighlight>();

    private static InteractableHighlight FindHighlight(Collider other)
    {
        // 優先找自己,找不到再往上找父物件,因為 collider 常在子物件上
        var h = other.GetComponent<InteractableHighlight>();
        if (h == null) h = other.GetComponentInParent<InteractableHighlight>();
        return h;
    }

    private void OnTriggerEnter(Collider other)
    {
        var h = FindHighlight(other);
        if (h == null) return;
        if (_inside.Add(h)) h.AddHighlight();
    }

    private void OnTriggerExit(Collider other)
    {
        var h = FindHighlight(other);
        if (h == null) return;
        if (_inside.Remove(h)) h.RemoveHighlight();
    }

    private void OnDisable()
    {
        // 離開場景或禁用 Player 時,把還在亮的全部關掉
        foreach (var h in _inside)
            if (h != null) h.RemoveHighlight();
        _inside.Clear();
    }
}