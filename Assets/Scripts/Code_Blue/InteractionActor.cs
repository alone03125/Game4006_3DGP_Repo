using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InteractionActor : MonoBehaviour
{
    [SerializeField] private bool canAffectWorldMechanisms = true;
    
    public bool CanAffectWorldMechanisms => canAffectWorldMechanisms;

    public void SetCanAffectWorldMechanisms(bool value)
    {
        canAffectWorldMechanisms = value;
    }
}
