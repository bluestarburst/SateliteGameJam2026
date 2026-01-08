using UnityEngine;
using UnityEngine.InputSystem;

interface OInteractable {
    public void Interact(OutsideSpacePlayerInteractor interactor);
}

public class OutsideSpacePlayerInteractor : MonoBehaviour
{
    public InputActionReference InteractAction;
    public Transform InteractorSource;
    public float InteractRange;
    
    private OutsideSpacePlayerControl movement;

    void Awake()
    {
        movement = GetComponent<OutsideSpacePlayerControl>();
    }

    // Update is called once per frame
    void Update()
    {
        if (InteractAction.action.WasPressedThisFrame()) {
            // Debug.Log("Interact button pressed");
            Ray r = new Ray(InteractorSource.position, InteractorSource.forward);
            if (Physics.Raycast(r, out RaycastHit hitInfo, InteractRange)) {
                if (hitInfo.collider.gameObject.TryGetComponent(out OInteractable interactObj)) {
                    interactObj.Interact(this);
                }
            }
        }
    }
}
