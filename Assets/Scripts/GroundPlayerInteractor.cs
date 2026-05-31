using UnityEngine;
using UnityEngine.InputSystem;

interface IInteractable {
    public void Interact(GroundPlayerInteractor interactor);
    public void OnScroll(GroundPlayerInteractor interactor, float vertical);
}

public class GroundPlayerInteractor : MonoBehaviour
{
    public InputActionReference InteractAction;
    public InputActionReference ScrollAction;
    public Transform InteractorSource;
    public Transform HoldPoint;
    public float InteractRange;
    
    private GroundPlayerControl movement;
    private IInteractable heldObject;

    void Awake()
    {
        movement = GetComponent<GroundPlayerControl>();
    }

    // Update is called once per frame
    void Update()
    {
        if (InteractAction.action.WasPressedThisFrame()) {
            if (heldObject != null) {
                heldObject.Interact(this);
                heldObject = null;
                return;
            }

            // Debug.Log("Interact button pressed");
            Ray r = new Ray(InteractorSource.position, InteractorSource.forward);
            if (Physics.Raycast(r, out RaycastHit hitInfo, InteractRange)) {
                if (hitInfo.collider.gameObject.TryGetComponent(out IInteractable interactObj)) {
                    interactObj.Interact(this);
                    heldObject = interactObj;
                }
            }
        }

        Vector2 scrollDelta = ScrollAction.action.ReadValue<Vector2>();
        if (scrollDelta != Vector2.zero)
        {
            float vertical = scrollDelta.y;

            if (heldObject != null) {
                heldObject.OnScroll(this, vertical);
                return;
            }

            // Debug.Log("Scroll " + (vertical > 0 ? "UP" : "DOWN") + " detected");
            Ray r = new Ray(InteractorSource.position, InteractorSource.forward);
            if (Physics.Raycast(r, out RaycastHit hitInfo, InteractRange)) {
                if (hitInfo.collider.gameObject.TryGetComponent(out IInteractable interactObj)) {
                    interactObj.OnScroll(this, vertical);
                }
            }
        }
    }

    public void restrictMovementTo(Vector3 coord, float distance)
    {
        movement.tetherTo(coord, distance);
    }

    public void releaseMovement()
    {
        movement.untether();
    }
}
