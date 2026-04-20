using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class InteractableHighlight : MonoBehaviour
{
    [Header("Glow Settings")]
    [ColorUsage(true, true)] 
    [SerializeField] private Color glowColor = new Color(1f, 0.8f, 0.2f, 1f);
    [SerializeField] private float glowIntensity = 2f;

    [Tooltip("若為空則自動抓取子物件的所有 Renderer")]
    [SerializeField] private Renderer[] targetRenderers;

    private MaterialPropertyBlock _mpb;
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private int _refCount; 
    private bool _isGlowing;

    private void Awake()
    {
        if (targetRenderers == null || targetRenderers.Length == 0)
            targetRenderers = GetComponentsInChildren<Renderer>(true);

        _mpb = new MaterialPropertyBlock();
    }

    public void AddHighlight()
    {
        _refCount++;
        if (!_isGlowing) SetGlow(true);
    }

    public void RemoveHighlight()
    {
        _refCount = Mathf.Max(0, _refCount - 1);
        if (_refCount == 0 && _isGlowing) SetGlow(false);
    }

    private void SetGlow(bool on)
    {
        _isGlowing = on;
        Color c = on ? glowColor * glowIntensity : Color.black;

        foreach (var r in targetRenderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(EmissionColorId, c);
            r.SetPropertyBlock(_mpb);
        }
    }

    private void OnDisable()
    {
        // 失效時還原,避免留著發光狀態
        if (_isGlowing) SetGlow(false);
        _refCount = 0;
    }
}