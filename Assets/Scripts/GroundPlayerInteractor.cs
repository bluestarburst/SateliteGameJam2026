using UnityEngine;
using UnityEngine.InputSystem;

interface IInteractable {
    public void Interact(GroundPlayerInteractor interactor);
}

public class GroundPlayerInteractor : MonoBehaviour
{
    public InputActionReference InteractAction;
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
        if (InteractAction.action.WasPressedThisFrame()) {
            // Debug.Log("Interact button pressed");
            Ray r = new Ray(InteractorSource.position, InteractorSource.forward);
            if (Physics.Raycast(r, out RaycastHit hitInfo, InteractRange)) {
                if (hitInfo.collider.gameObject.TryGetComponent(out IInteractable interactObj)) {
                    interactObj.Interact(this);
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
