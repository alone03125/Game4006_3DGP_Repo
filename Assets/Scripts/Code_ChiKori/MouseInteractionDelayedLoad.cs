using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

public class MouseInteractionDelayedLoad : MonoBehaviour
{
    [Header("Brightness Settings")]
    [Range(0.5f, 2f)]
    public float hoverBrightnessMultiplier = 1.3f;
    [Range(0.5f, 2f)]
    public float clickBrightnessMultiplier = 0.8f;

    [Header("Scene Loading (Delayed)")]
    [Tooltip("是否启用延迟加载场景")]
    public bool enableDelayedSceneLoad = true;
    [Tooltip("点击后延迟多少秒加载场景")]
    public float loadDelay = 2.0f;
    [Tooltip("要加载的场景名称（需已添加到 Build Settings）")]
    public string sceneToLoad;

    [Header("Events")]
    [Tooltip("点击时立即触发的事件（例如音效、动画等）")]
    public UnityEvent onClick;

    // 存储每个材质的状态
    private class MaterialState
    {
        public Material material;
        public float originalHue;
        public float originalSaturation;
        public float originalBrightness;
    }

    private List<MaterialState> materialStates = new List<MaterialState>();
    private bool hasAnyRenderer = false;
    private bool isHovering = false;
    private bool isPressed = false;
    private Coroutine delayedLoadCoroutine;

    void Start()
    {
        // 收集自身及所有子物体中的渲染器
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);

        if (renderers.Length == 0)
        {
            Debug.LogWarning($"物体 '{gameObject.name}' 及其子物体中未找到任何 Renderer 组件，亮度变化将无效。");
            hasAnyRenderer = false;
            // 注意：即使没有渲染器，其他功能（如延迟加载、事件）仍会正常工作
            return;
        }

        // 遍历每个渲染器，获取其实例化材质并存储原始 HSV 信息
        foreach (Renderer renderer in renderers)
        {
            // 获取材质实例数组（每个材质独立，避免影响其他物体）
            Material[] materials = renderer.materials;
            if (materials == null || materials.Length == 0)
                continue;

            foreach (Material mat in materials)
            {
                if (mat == null) continue;

                Color color = mat.color;
                Color.RGBToHSV(color, out float h, out float s, out float v);

                MaterialState state = new MaterialState
                {
                    material = mat,
                    originalHue = h,
                    originalSaturation = s,
                    originalBrightness = v
                };
                materialStates.Add(state);
            }
        }

        if (materialStates.Count == 0)
        {
            Debug.LogWarning($"物体 '{gameObject.name}' 及其子物体中找到了 Renderer，但没有有效的材质，亮度变化将无效。");
            hasAnyRenderer = false;
        }
        else
        {
            hasAnyRenderer = true;
        }
    }

    void OnMouseEnter()
    {
        isHovering = true;
        UpdateBrightness();
    }

    void OnMouseExit()
    {
        isHovering = false;
        isPressed = false;
        UpdateBrightness();
    }

    void OnMouseDown()
    {
        isPressed = true;
        UpdateBrightness();

        // 1. 立即触发所有绑定的事件
        onClick?.Invoke();

        // 2. 如果启用了延迟加载，启动协程
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
        UpdateBrightness();
    }

    void UpdateBrightness()
    {
        if (!hasAnyRenderer) return;

        // 确定目标亮度系数
        float brightnessMultiplier;
        if (isPressed)
            brightnessMultiplier = clickBrightnessMultiplier;
        else if (isHovering)
            brightnessMultiplier = hoverBrightnessMultiplier;
        else
            brightnessMultiplier = 1f;

        // 更新所有材质的颜色
        foreach (MaterialState state in materialStates)
        {
            if (state.material == null) continue;

            float targetBrightness = state.originalBrightness * brightnessMultiplier;
            targetBrightness = Mathf.Clamp01(targetBrightness);

            Color newColor = Color.HSVToRGB(state.originalHue, state.originalSaturation, targetBrightness);
            state.material.color = newColor;
        }
    }

    IEnumerator LoadSceneAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        SceneManager.LoadScene(sceneToLoad);
    }

    void OnDisable()
    {
        RestoreOriginalColors();
    }

    void OnDestroy()
    {
        RestoreOriginalColors();
    }

    private void RestoreOriginalColors()
    {
        if (!hasAnyRenderer) return;

        foreach (MaterialState state in materialStates)
        {
            if (state.material == null) continue;

            Color originalColor = Color.HSVToRGB(state.originalHue, state.originalSaturation, state.originalBrightness);
            state.material.color = originalColor;
        }
    }
}