using UnityEngine;

public class PuzzleCube : MonoBehaviour, IInteractable
{
    [SerializeField] private int cubeNo; // 1,2,3,4
    [SerializeField] private SequencePuzzleManager puzzleManager;

    public void Interact()
    {
        if (puzzleManager == null) return;
        puzzleManager.PressCube(cubeNo);
    }
}