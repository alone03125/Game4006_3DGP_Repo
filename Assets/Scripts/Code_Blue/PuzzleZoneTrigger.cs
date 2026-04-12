using UnityEngine;

public class PuzzleZoneTrigger : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        var player = other.GetComponent<PlayerInteraction>();
        if (player != null) player.SetPuzzleZoneState(true);
    }

    private void OnTriggerExit(Collider other)
    {
        var player = other.GetComponent<PlayerInteraction>();
        if (player != null) player.SetPuzzleZoneState(false);
    }
}