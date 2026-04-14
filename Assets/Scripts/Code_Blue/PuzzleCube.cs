using UnityEngine;

public class PuzzleCube : MonoBehaviour, IInteractable
{
    [SerializeField] private int cubeNo; // 1,2,3,4
    [SerializeField] private SequencePuzzleManager puzzleManager;
    [SerializeField] private bool isEnabled = true;

    public void EnableCube()
    {
        isEnabled = true;
        Debug.Log($"[PuzzleCube:{name}] EnableCube -> isEnabled={isEnabled}");
    }

    public void DisableCube()
    {
        isEnabled = false;
        Debug.Log($"[PuzzleCube:{name}] DisableCube -> isEnabled={isEnabled}");
    }

    public void Interact()
    {
        Debug.Log($"[PuzzleCube:{name}] Interact called. isEnabled={isEnabled}, manager={(puzzleManager != null)}");

        if (!isEnabled) return;
        if (puzzleManager == null) return;

        puzzleManager.PressCube(cubeNo);
    }

    public void SetCubeEnabled(bool value)
    {
        isEnabled = value;
    }
}