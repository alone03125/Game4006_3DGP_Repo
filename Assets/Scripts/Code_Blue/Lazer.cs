using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Lazer : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        var actor = other.GetComponentInParent<InteractionActor>();
        if (actor == null || !actor.CanAffectWorldMechanisms) return;

        if (other.GetComponentInParent<PlayerInteraction>() != null)
        {
            Debug.Log("Lazer hit player");
        }
    }

}
