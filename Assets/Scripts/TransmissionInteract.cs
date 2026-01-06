using UnityEngine;

public class TransmissionInteract : MonoBehaviour, IInteractable
{
    public bool answeringTransmission = false;
    public void Interact() {
        answeringTransmission = !answeringTransmission;
    }
}
