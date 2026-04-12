using UnityEngine;

public class SequencePuzzleManager : MonoBehaviour
{
    [Header("Correct Order")]
    [SerializeField] private int[] correctOrder = { 1, 2, 3, 4 };

    [Header("Door")]
    [SerializeField] private PuzzleDoor door;

    private int currentIndex = 0;
    private bool solved = false;

    // Function for cube puzzle 
    public void PressCube(int cubeNo)
    {
        if (solved) return;

        Debug.Log($"Cube no: {cubeNo} is pressed.");

        if (cubeNo == correctOrder[currentIndex])
        {
            currentIndex++;

            if (currentIndex >= correctOrder.Length)
            {
                solved = true;
                Debug.Log("Puzzle solved. Door opening.");
                door.OpenDoor();
            }
        }
        else
        {
            Debug.Log("Wrong order. Retry from beginning.");
            currentIndex = 0;
        }
    }
}