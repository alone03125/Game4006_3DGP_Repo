using System.Collections;
using UnityEngine;

public class Lazer : MonoBehaviour
{
    [Header("Safety")]
    [SerializeField] private float hitCooldown = 0.1f;
    [SerializeField] private float reloadDelay = 0.05f;

    private float _nextHitTime;
    private bool _isReloading;

    private TraceCloneManager _traceMgr;
    private CloneManager _cloneMgr;
    private LazerFreeze _freeze;

    private void Awake()
    {
        _traceMgr = FindAnyObjectByType<TraceCloneManager>();
        _cloneMgr = FindAnyObjectByType<CloneManager>();
        _freeze = GetComponentInParent<LazerFreeze>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_freeze != null && _freeze.IsFrozen)
            return;

        if (_isReloading || Time.time < _nextHitTime)
            return;

        _nextHitTime = Time.time + hitCooldown;

        Transform root = other.transform.root;
        if (root == null) return;

        Debug.Log("Lazer hit " + root.tag);
        Debug.Log("Lazer hit " + root.name);

        //Discard recording on hit TracePhantom
        if (root.CompareTag("TracePhantom"))
        {
            if (_traceMgr != null) _traceMgr.ForceExitClean();
            else Destroy(root.gameObject);

            Debug.Log("Lazer hit TracePhantom -> discard recording");
            return;
        }

        //Remove VisionClone
        if (root.CompareTag("VisionClone"))
        {
            if (_cloneMgr != null) _cloneMgr.ForceExitTimeStop(false);
            else Destroy(root.gameObject);

            Debug.Log("Lazer hit VisionClone -> removed");
            return;
        }

        //Remove TraceClone
        if (root.CompareTag("TraceClone"))
        {
            Destroy(root.gameObject);
            Debug.Log("Lazer hit TraceClone -> removed");
            return;
        }

        //Reset level on hit Player
        if (root.CompareTag("Player"))
        {
            Debug.Log("Lazer hit Player -> reset level");
            StartCoroutine(ReloadCurrentLevel());
        }
    }

    //Reset level on hit Player
    private IEnumerator ReloadCurrentLevel()
    {
        _isReloading = true;
        yield return LevelReset.ReloadCurrentLevel(reloadDelay);
    }
}