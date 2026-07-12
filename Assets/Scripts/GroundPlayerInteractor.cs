using UnityEngine;
using UnityEngine.InputSystem;

public interface IInteractable {
    public void Interact(GroundPlayerInteractor interactor);
    public void Place(GroundPlayerInteractor interactor, IPlaceLocation loc);
    public void Click(GroundPlayerInteractor interactor);
    public void OnScroll(GroundPlayerInteractor interactor, float vertical);
}

public interface IPlaceLocation {
    public void Suggest(IInteractable obj);
}

public class GroundPlayerInteractor : MonoBehaviour
{
    public InputActionReference InteractAction;
    public InputActionReference ScrollAction;
    public InputActionReference ClickAction;
    public InputActionReference PlaceAction;
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

        if (PlaceAction.action.IsPressed()) {
            if (heldObject == null) {
                return;
            }

            FileCrateInteract crate = GetComponentInChildren<FileCrateInteract>();
            if (crate != null) {
                Ray r = new Ray(InteractorSource.position, InteractorSource.forward);
                if (Physics.Raycast(r, out RaycastHit hitInfo, InteractRange)) {
                    if (hitInfo.collider.gameObject.TryGetComponent(out IPlaceLocation placeloc)) {
                        placeloc.Suggest(heldObject);
                    }
                }
            }
        }

        if (PlaceAction.action.WasReleasedThisFrame()) {
            if (heldObject == null) {
                return;
            }

            FileCrateInteract crate = GetComponentInChildren<FileCrateInteract>();
            if (crate != null) {
                Ray r = new Ray(InteractorSource.position, InteractorSource.forward);
                if (Physics.Raycast(r, out RaycastHit hitInfo, InteractRange)) {
                    if (hitInfo.collider.gameObject.TryGetComponent(out IPlaceLocation placeloc)) {
                        heldObject.Place(this, placeloc);
                    }
                }
            }
        }

        if (ClickAction.action.WasPressedThisFrame()) {
            if (heldObject != null) {
                heldObject.Click(this);
                return;
            }

            // Debug.Log("Click button pressed");
            Ray r = new Ray(InteractorSource.position, InteractorSource.forward);
            if (Physics.Raycast(r, out RaycastHit hitInfo, InteractRange)) {
                if (hitInfo.collider.gameObject.TryGetComponent(out IInteractable interactObj)) {
                    interactObj.Click(this);
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
