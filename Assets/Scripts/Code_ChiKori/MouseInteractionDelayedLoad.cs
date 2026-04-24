using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class MouseInteractionDelayedLoad : MonoBehaviour
{
    [Header("—— 渲染器材质切换 ——")]
    public List<RendererMaterialOverride> rendererOverrides = new List<RendererMaterialOverride>();

    [Header("—— TMP 文字透明度配置 ——")]
    [Tooltip("只改变文字的透明度，不改变颜色")]
    public List<TextAlphaOverride> textOverrides = new List<TextAlphaOverride>();

    [Header("场景加载（延迟）")]
    public bool enableDelayedSceneLoad = true;
    public float loadDelay = 2.0f;
    public string sceneToLoad;

    [Header("事件")]
    public UnityEvent onClick;

    [System.Serializable]
    public class RendererMaterialOverride
    {
        public Renderer targetRenderer;
        public bool enabled = true;
        public Material normalMaterial;
        public Material hoverMaterial;
        public Material clickMaterial;
    }

    [System.Serializable]
    public class TextAlphaOverride
    {
        public TMP_Text targetText;
        public bool enabled = true;
        [Range(0f, 1f)] public float normalAlpha = 1f;
        [Range(0f, 1f)] public float hoverAlpha = 0.8f;
        [Range(0f, 1f)] public float clickAlpha = 0.5f;
    }

    private class RendererRuntime
    {
        public Renderer renderer;
        public Material originalMaterial;
        public Material normalMaterial;
        public Material hoverMaterial;
        public Material clickMaterial;
    }

    private class TextRuntime
    {
        public TMP_Text text;
        public Color originalColor;          // 完整的原始颜色（含原始Alpha）
        public float normalAlpha;
        public float hoverAlpha;
        public float clickAlpha;
    }

    private List<RendererRuntime> rendererRuntimes = new List<RendererRuntime>();
    private List<TextRuntime> textRuntimes = new List<TextRuntime>();
    private bool hasAnyTarget = false;
    private bool isHovering = false;
    private bool isPressed = false;
    private Coroutine delayedLoadCoroutine;

    void Start()
    {
        CollectTargets();
    }

    void CollectTargets()
    {
        rendererRuntimes.Clear();
        textRuntimes.Clear();

        // 材质切换配置
        foreach (var ov in rendererOverrides)
        {
            if (!ov.enabled || ov.targetRenderer == null) continue;
            Renderer renderer = ov.targetRenderer;
            Material currentMat = renderer.material; // 获取实例
            rendererRuntimes.Add(new RendererRuntime
            {
                renderer = renderer,
                originalMaterial = currentMat,
                normalMaterial = ov.normalMaterial,
                hoverMaterial = ov.hoverMaterial,
                clickMaterial = ov.clickMaterial
            });
        }

        // 文字透明度配置
        foreach (var ov in textOverrides)
        {
            if (!ov.enabled || ov.targetText == null) continue;
            textRuntimes.Add(new TextRuntime
            {
                text = ov.targetText,
                originalColor = ov.targetText.color,
                normalAlpha = ov.normalAlpha,
                hoverAlpha = ov.hoverAlpha,
                clickAlpha = ov.clickAlpha
            });
        }

        hasAnyTarget = (rendererRuntimes.Count + textRuntimes.Count) > 0;
        if (!hasAnyTarget)
            Debug.LogWarning("未配置任何需要变化的 Renderer 或 TMP 文字。");
        else
        {
            ApplyState();
        }
    }

    void OnMouseEnter()
    {
        isHovering = true;
        ApplyState();
    }

    void OnMouseExit()
    {
        isHovering = false;
        isPressed = false;
        ApplyState();
    }

    void OnMouseDown()
    {
        isPressed = true;
        ApplyState();
        onClick?.Invoke();
        if (enableDelayedSceneLoad && !string.IsNullOrEmpty(sceneToLoad))
        {
            if (delayedLoadCoroutine != null)
                StopCoroutine(delayedLoadCoroutine);
            delayedLoadCoroutine = StartCoroutine(LoadSceneAfterDelay(loadDelay));
        }
    }

    void OnMouseUp()
    {
        isPressed = false;
        ApplyState();
    }

    void ApplyState()
    {
        if (!hasAnyTarget) return;

        bool click = isPressed;
        bool hover = isHovering && !click;

        // 切换材质
        foreach (var rt in rendererRuntimes)
        {
            if (rt.renderer == null) continue;
            Material targetMat = null;
            if (click && rt.clickMaterial != null)
                targetMat = rt.clickMaterial;
            else if (hover && rt.hoverMaterial != null)
                targetMat = rt.hoverMaterial;
            else if (rt.normalMaterial != null)
                targetMat = rt.normalMaterial;
            if (targetMat != null)
                rt.renderer.material = targetMat;
        }

        // 只改变文字的透明度（保留原始 RGB）
        foreach (var rt in textRuntimes)
        {
            if (rt.text == null) continue;
            float targetAlpha;
            if (click)
                targetAlpha = rt.clickAlpha;
            else if (hover)
                targetAlpha = rt.hoverAlpha;
            else
                targetAlpha = rt.normalAlpha;

            Color originalRGB = rt.originalColor;
            // 新的颜色 = 原始RGB + 新的Alpha（注意保留原始RGB，不要用当前text.color，因为可能被之前修改过）
            // 但为了安全，始终基于 originalColor 重建
            Color newColor = new Color(originalRGB.r, originalRGB.g, originalRGB.b, targetAlpha);
            rt.text.color = newColor;
        }
    }

    void RestoreOriginal()
    {
        if (!hasAnyTarget) return;

        foreach (var rt in rendererRuntimes)
        {
            if (rt.renderer != null && rt.originalMaterial != null)
                rt.renderer.material = rt.originalMaterial;
        }

        foreach (var rt in textRuntimes)
        {
            if (rt.text != null)
                rt.text.color = rt.originalColor; // 完全恢复原始颜色（含原始Alpha）
        }
    }

    IEnumerator LoadSceneAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        SceneManager.LoadScene(sceneToLoad);
    }

    void OnDisable() { RestoreOriginal(); }
    void OnDestroy() { RestoreOriginal(); }
}