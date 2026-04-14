using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteraction : MonoBehaviour
{
    
    
    [Header("Puzzle Interaction")]
    [SerializeField] private float interactDistance = 3f;
    [SerializeField] private LayerMask interactMask;
    [SerializeField] private Transform rayOrigin;
    [SerializeField] private LayerMask occlusionMask = ~0;

    // private bool isInPuzzleZone = false;
    private IHoldInteractable _currentHoldTarget;// Start is called before the first frame update

    private InputAction _activateAction;
    private bool _activatePressedThisTick;
    private bool _activateReleasedThisTick;
    private bool _executeInteractImmediately = true;


     private void OnEnable()
        {
            var playerInput = GetComponent<PlayerInput>();
            if (playerInput == null) return;
            _activateAction = playerInput.actions.FindAction("Activate", throwIfNotFound: false);
            if (_activateAction == null) return;
            _activateAction.performed += OnActivatePerformed;
            _activateAction.canceled += OnActivateCanceled;
        }

        private void OnDisable()
        {
            if (_activateAction == null) return;

            _activateAction.performed -= OnActivatePerformed;
            _activateAction.canceled -= OnActivateCanceled;
        }

      private void OnActivatePerformed(InputAction.CallbackContext context)
        {
            _activatePressedThisTick = true;

            if (_executeInteractImmediately)
                TryInteract();
        }

        private void OnActivateCanceled(InputAction.CallbackContext context)
        {
            _activateReleasedThisTick = true;

            if (_executeInteractImmediately)
                ReleaseHoldInteract();
        }


      private void TryInteract()
    {
       
        //Ray
        
        // Transform origin = rayOrigin != null ? rayOrigin : Camera.main.transform;
        Transform origin = rayOrigin != null ? rayOrigin : transform; 

        Ray ray = new Ray(origin.position, origin.forward);

        if (!Physics.Raycast(ray, out RaycastHit hit, interactDistance, interactMask))
            return;
        
        
        // PuzzleCube must be in PuzzleZone
        PuzzleCube puzzleCube = hit.collider.GetComponent<PuzzleCube>();
        if (puzzleCube != null)
        {
            // if (!isInPuzzleZone) return;
            puzzleCube.Interact();
            return;
        }
        
        // Long press lever (LeverSwitch): Priority hold process
        IHoldInteractable hold = hit.collider.GetComponent<IHoldInteractable>()
            ?? hit.collider.GetComponentInParent<IHoldInteractable>();
        if (hold != null)
        {
            _currentHoldTarget = hold;
            hold.BeginHold();
            return;
        }
        
        // General interaction (Lever3StateSwitch, etc.): Not limited to PuzzleZone
        IInteractable interactable = hit.collider.GetComponent<IInteractable>()
            ?? hit.collider.GetComponentInParent<IInteractable>();
        interactable?.Interact();
    }

    private void ReleaseHoldInteract()
    {
        if (_currentHoldTarget == null) return;
        _currentHoldTarget.EndHold();
        _currentHoldTarget = null;
    }


    
    //Debug ray
    private bool IsInteractableHit(RaycastHit hit)
    {
        // Any interactable type counts
        if (hit.collider.GetComponent<PuzzleCube>() != null) return true;

        IHoldInteractable hold = hit.collider.GetComponent<IHoldInteractable>()
            ?? hit.collider.GetComponentInParent<IHoldInteractable>();
        if (hold != null) return true;

        IInteractable interactable = hit.collider.GetComponent<IInteractable>()
            ?? hit.collider.GetComponentInParent<IInteractable>();

        return interactable != null;
    }


    private void Update()
    {
        // Transform origin = rayOrigin != null ? rayOrigin : (Camera.main != null ? Camera.main.transform : null);
        
        Transform origin = rayOrigin != null ? rayOrigin : transform;

        if (origin == null) return;

        Ray ray = new Ray(origin.position, origin.forward);
        bool hasHit = Physics.Raycast(ray, out RaycastHit hit, interactDistance, interactMask);
        bool canInteract = hasHit && IsInteractableHit(hit);

        // 指到可互動物件 -> 紅色；否則綠色
        Color rayColor = canInteract ? Color.red : Color.green;
        Debug.DrawRay(origin.position, origin.forward * interactDistance, rayColor);
    }

    // public void SetPuzzleZoneState(bool inZone)
    // {
    //     isInPuzzleZone = inZone;
    // }

    public void RequestInteract()
    {
        TryInteract();
    }

    public void RequestReleaseInteract()
    {
        ReleaseHoldInteract();
    }

    //Consume activate pressed and released
    public bool ConsumeActivatePressed()
    {
        bool v = _activatePressedThisTick;
        _activatePressedThisTick = false;
        return v;
    }

    public bool ConsumeActivateReleased()
    {
        bool v = _activateReleasedThisTick;
        _activateReleasedThisTick = false;
        return v;
    }

    //Avoid hold interact when recording
    public void SetExecuteInteractImmediately(bool value)
    {
        _executeInteractImmediately = value;
        if (!value)
            ReleaseHoldInteract(); 
    }

}

