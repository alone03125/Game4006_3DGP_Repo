using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Lightweight FPS counter displayed in the top-right corner.
/// Controlled by GameSettings.ShowFPS and PauseManager video settings.
/// </summary>
public class FPSDisplay : MonoBehaviour
{
    private TMP_Text fpsText;
    private Canvas canvas;
    private float deltaTime = 0f;
    private float updateInterval = 0.25f;
    private float timer = 0f;

    void Awake()
    {
        // Create a simple overlay canvas for FPS
        var canvasObj = new GameObject("FPSCanvas");
        canvasObj.transform.SetParent(transform);
        canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        canvasObj.AddComponent<CanvasScaler>();

        var textObj = new GameObject("FPSText");
        textObj.transform.SetParent(canvasObj.transform, false);
        var rect = textObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(1, 1);
        rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(1, 1);
        rect.anchoredPosition = new Vector2(-10, -10);
        rect.sizeDelta = new Vector2(160, 35);

        fpsText = textObj.AddComponent<TextMeshProUGUI>();
        fpsText.fontSize = 22;
        fpsText.color = Color.green;
        fpsText.alignment = TextAlignmentOptions.TopRight;
        fpsText.text = "";

        SetVisible(GameSettings.ShowFPS);
    }

    void Update()
    {
        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
        timer += Time.unscaledDeltaTime;
        if (timer >= updateInterval)
        {
            timer = 0f;
            float fps = 1.0f / deltaTime;
            fpsText.text = $"FPS: {Mathf.RoundToInt(fps)}";
        }
    }

    public void SetVisible(bool visible)
    {
        if (canvas != null)
            canvas.gameObject.SetActive(visible);
    }
}
