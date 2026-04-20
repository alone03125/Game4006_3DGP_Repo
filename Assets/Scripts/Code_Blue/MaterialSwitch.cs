using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class MaterialSwitch : MonoBehaviour
{
    [Header("Materials")]
    [Tooltip("default red")]
    [SerializeField] private Material redMaterial;

    [Tooltip("triggered,green material")]
    [SerializeField] private Material greenMaterial;

    [Header("Options")]
    [Tooltip("sub-material index, single material object keep 0")]
    [SerializeField] private int materialIndex = 0;

    [Tooltip("start with red material")]
    [SerializeField] private bool resetToRedOnStart = true;

    private Renderer targetRenderer;

    private void Awake()
    {
        targetRenderer = GetComponent<Renderer>();
    }

    private void Start()
    {
        if (resetToRedOnStart) SetRed();
    }

    public void SetGreen()
    {
        ApplyMaterial(greenMaterial);
    }


    public void SetRed()
    {
        ApplyMaterial(redMaterial);
    }

    
    public void Toggle()
    {
        if (targetRenderer == null) return;
        var current = targetRenderer.sharedMaterials[materialIndex];
        ApplyMaterial(current == greenMaterial ? redMaterial : greenMaterial);
    }

    private void ApplyMaterial(Material mat)
    {
        if (targetRenderer == null || mat == null) return;

    
        var mats = targetRenderer.sharedMaterials;
        if (materialIndex < 0 || materialIndex >= mats.Length) return;
        mats[materialIndex] = mat;
        targetRenderer.sharedMaterials = mats;
    }
}