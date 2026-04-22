using UnityEngine;
using UnityEngine.Events;

public class MouseInteractionBrightness : MonoBehaviour
{
    [Header("Brightness Settings")]
    [Tooltip("鼠标悬停时的亮度倍率 (相对于原始亮度)")]
    [Range(0.5f, 2f)]
    public float hoverBrightnessMultiplier = 1.3f;

    [Tooltip("鼠标点击时的亮度倍率")]
    [Range(0.5f, 2f)]
    public float clickBrightnessMultiplier = 0.8f;

    [Header("Events")]
    [Tooltip("点击物体时触发的自定义事件")]
    public UnityEvent onClick;

    private Renderer objectRenderer;
    private Color originalColor;
    private float originalBrightness; // 原始亮度值 (HSV中的V)
    private float originalHue, originalSaturation;

    private bool isHovering = false;
    private bool isPressed = false;

    void Start()
    {
        objectRenderer = GetComponent<Renderer>();
        if (objectRenderer == null)
        {
            Debug.LogError($"{gameObject.name} 未找到 Renderer 组件。");
            enabled = false;
            return;
        }

        if (objectRenderer.material != null)
        {
            originalColor = objectRenderer.material.color;
            // 将原始颜色转换为HSV，保存色相、饱和度、明度
            Color.RGBToHSV(originalColor, out originalHue, out originalSaturation, out originalBrightness);
        }
        else
        {
            originalColor = Color.white;
            originalBrightness = 1f;
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

        // 触发点击事件
        onClick?.Invoke();
    }

    void OnMouseUp()
    {
        isPressed = false;
        UpdateBrightness();
    }

    void UpdateBrightness()
    {
        if (objectRenderer == null) return;

        float targetBrightness;
        if (isPressed)
            targetBrightness = originalBrightness * clickBrightnessMultiplier;
        else if (isHovering)
            targetBrightness = originalBrightness * hoverBrightnessMultiplier;
        else
            targetBrightness = originalBrightness;

        // 限制亮度在合理范围内
        targetBrightness = Mathf.Clamp01(targetBrightness);

        // 使用原始色相和饱和度，结合新亮度生成颜色
        Color newColor = Color.HSVToRGB(originalHue, originalSaturation, targetBrightness);
        objectRenderer.material.color = newColor;
    }

    void OnDisable()
    {
        if (objectRenderer != null && objectRenderer.material != null)
            objectRenderer.material.color = originalColor;
    }

    void OnDestroy()
    {
        if (objectRenderer != null && objectRenderer.material != null)
            objectRenderer.material.color = originalColor;
    }
}