using UnityEngine;
using UnityEngine.Events;

public class SequencePuzzleManager : MonoBehaviour
{
    [Header("Correct Order")]
    [SerializeField] private int[] correctOrder = { 1, 2, 3, 4 };

    [Header("Effects")]
    [SerializeField] private UnityEvent onPuzzleSolved;

    private int currentIndex = 0;
    private bool solved = false;

    // Function for cube puzzle 
   public void PressCube(int cubeNo)
    {
        Debug.Log($"[SeqPuzzle] PressCube({cubeNo}) solved={solved} index={currentIndex}");
        if (solved) return;
        if (cubeNo == correctOrder[currentIndex])
        {
            currentIndex++;
            Debug.Log($"[SeqPuzzle] Correct. index -> {currentIndex}");
            if (currentIndex >= correctOrder.Length)
            {
                solved = true;
                Debug.Log("[SeqPuzzle] Solved -> Invoke onPuzzleSolved");
                onPuzzleSolved?.Invoke();
            }
        }
        else
        {
            Debug.Log("[SeqPuzzle] Wrong order -> reset index to 0");
            currentIndex = 0;
        }
    }
}