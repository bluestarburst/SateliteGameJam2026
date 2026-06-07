using UnityEngine;

public class KeyboardInteract : MonoBehaviour, IInteractable
{
    public void Interact(GroundPlayerInteractor interactor) {
        Debug.Log(Random.Range(0, 100));
    }
    public void OnScroll(GroundPlayerInteractor interactor, float vertical) {
        //nothing
    }
    public void Click(GroundPlayerInteractor interactor) {
        //nothing
    }
}
