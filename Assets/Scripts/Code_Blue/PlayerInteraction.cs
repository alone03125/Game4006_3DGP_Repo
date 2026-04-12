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

    private bool isInPuzzleZone = false;
    private IHoldInteractable _currentHoldTarget;// Start is called before the first frame update

    private InputAction _activateAction;


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
            TryInteract();
        }
        private void OnActivateCanceled(InputAction.CallbackContext context)
        {
            ReleaseHoldInteract();
        }


      private void TryInteract()
    {
        Transform origin = rayOrigin != null ? rayOrigin : Camera.main.transform;
        Ray ray = new Ray(origin.position, origin.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, interactDistance, interactMask))
            return;
        // PuzzleCube must be in PuzzleZone
        PuzzleCube puzzleCube = hit.collider.GetComponent<PuzzleCube>();
        if (puzzleCube != null)
        {
            if (!isInPuzzleZone) return;
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

    private void Update()
    {
        Transform origin = rayOrigin != null ? rayOrigin : (Camera.main != null ? Camera.main.transform : null);
        if (origin != null)
        {
            Color rayColor = isInPuzzleZone ? Color.green : Color.red;
            Debug.DrawRay(origin.position, origin.forward * interactDistance, rayColor);
        }
    }

    public void SetPuzzleZoneState(bool inZone)
    {
        isInPuzzleZone = inZone;
    }

}

