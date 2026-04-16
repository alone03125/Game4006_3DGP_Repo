using System.Collections;
using UnityEngine;

public class DeadZone : MonoBehaviour
{
    [Header("Filter")]
    [SerializeField] private string playerTag = "Player";

    [Header("Safety")]
    [SerializeField] private float triggerCooldown = 0.15f;
    private float nextAllowedTime = 0f;

    [Header("Level Reset")]
    [SerializeField] private float reloadDelay = 0.05f;
    private bool isReloading = false;

    private void OnTriggerEnter(Collider other)
    {
        if (Time.time < nextAllowedTime || isReloading) return;
        nextAllowedTime = Time.time + triggerCooldown;

        Transform root = other.transform.root;
        if (root == null || !root.CompareTag(playerTag)) return;

        Debug.Log("DeadZone hit Player -> reset level");
        StartCoroutine(ReloadCurrentLevel());
    }

  private IEnumerator ReloadCurrentLevel()
    {
        isReloading = true;

        var cloneMgr = FindAnyObjectByType<CloneManager>();
        if (cloneMgr != null)
            cloneMgr.ForceExitTimeStop(false, handleTraceClone: false);

        var traceMgr = FindAnyObjectByType<TraceCloneManager>();
        if (traceMgr != null && traceMgr.IsPhantomActive())
            traceMgr.ForceExitClean();

        yield return LevelReset.ReloadCurrentLevel(reloadDelay);
    }
}