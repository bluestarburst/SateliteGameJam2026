using UnityEngine;

public class FileCrateInteract : MonoBehaviour, IInteractable
{
    public void Interact(GroundPlayerInteractor interactor) {
        //nothing
    }

    public void OnScroll(GroundPlayerInteractor interactor, float vertical) {
        Debug.Log(vertical > 0 ? "Forward" : "backward");
    }
}
