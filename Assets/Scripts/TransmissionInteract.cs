using UnityEngine;

using SatelliteGameJam.Networking.Voice;

public class TransmissionInteract : MonoBehaviour, IInteractable
{
    public bool answeringTransmission = false;
    public float tetherDistance;
    public GameObject cable;
    public GameObject transmissionHeadphones;
    public GameObject playerHeadphones;

    void Awake() {
        cable.SetActive(false);
        transmissionHeadphones.SetActive(true);
        playerHeadphones.SetActive(false);
    }

    public void Interact(GroundPlayerInteractor interactor) {
        answeringTransmission = !answeringTransmission;
        // VoiceSessionManager.Instance.SetLocalPlayerAtConsole(answeringTransmission);
        if (answeringTransmission) {
            interactor.restrictMovementTo(transform.position, tetherDistance);
            cable.SetActive(true);
            transmissionHeadphones.SetActive(false);
            playerHeadphones.SetActive(true);
        }
        else {
            interactor.releaseMovement();
            cable.SetActive(false);
            transmissionHeadphones.SetActive(true);
            playerHeadphones.SetActive(false);
        }
    }

    public void OnScroll(GroundPlayerInteractor interactor, float vertical) {
        //nothing
    }
}
