using UnityEngine;
using UnityEngine.InputSystem;

interface IInteractable {
    public void Interact(GroundPlayerInteractor interactor)
    {
        return;
    }
    public void OnScroll(GroundPlayerInteractor interactor, float vertical)
    {
        return;
    }
    public void Escape(GroundPlayerInteractor interactor)
    {
        return;
    }
}

public class GroundPlayerInteractor : MonoBehaviour
{
    public InputActionReference InteractAction;
    public InputActionReference EscapeAction;
    public InputActionReference ScrollAction;
    public Transform InteractorSource;
    public float InteractRange;
    
    private GroundPlayerControl movement;

    void Awake()
    {
        movement = GetComponent<GroundPlayerControl>();
    }

    // Update is called once per frame
    void Update()
    {
        if (WasPressedThisFrame(InteractAction) && TryGetInteractable(out IInteractable interactObj))
        {
            interactObj.Interact(this);
        }

        if (WasPressedThisFrame(EscapeAction) && TryGetInteractable(out interactObj))
        {
            interactObj.Escape(this);
        }

        Vector2 scrollDelta = ScrollAction != null && ScrollAction.action != null
            ? ScrollAction.action.ReadValue<Vector2>()
            : Vector2.zero;
        if (scrollDelta != Vector2.zero && TryGetInteractable(out interactObj))
        {
            interactObj.OnScroll(this, scrollDelta.y);
        }
    }

    private static bool WasPressedThisFrame(InputActionReference actionReference)
    {
        return actionReference != null && actionReference.action != null && actionReference.action.WasPressedThisFrame();
    }

    private bool TryGetInteractable(out IInteractable interactable)
    {
        interactable = null;
        if (InteractorSource == null || InteractRange <= 0f)
        {
            return false;
        }

        Ray ray = new Ray(InteractorSource.position, InteractorSource.forward);
        return Physics.Raycast(ray, out RaycastHit hitInfo, InteractRange) &&
            hitInfo.collider.gameObject.TryGetComponent(out interactable);
    }

    public void restrictMovementTo(Vector3 coord, float distance)
    {
        movement?.tetherTo(coord, distance);
    }

    public void releaseMovement()
    {
        movement?.untether();
    }

    public void lockPlayer() {
        movement?.lockPlayer();
    }

    public void unlockPlayer() {
        movement?.unlockPlayer();
    }
}
