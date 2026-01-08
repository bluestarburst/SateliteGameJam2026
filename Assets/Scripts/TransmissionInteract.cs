using UnityEngine;

public class TransmissionInteract : MonoBehaviour, IInteractable
{
    public bool answeringTransmission = false;
    public float tetherDistance;

    public void Interact(GroundPlayerInteractor interactor) {
        answeringTransmission = !answeringTransmission;
        if (answeringTransmission) interactor.restrictMovementTo(transform.position, tetherDistance);
        else interactor.releaseMovement();
    }

    public void OnScroll(GroundPlayerInteractor interactor, float vertical) {
        //nothing
    }
}
