using UnityEngine;

public class ConsoleInteraction : MonoBehaviour, IInteractable
{

    // offset of the player camera away from the console terminal
    public Vector3 cameraPositionOffset;

    // look at position offset for the player camera when interacting with the console terminal
    public Vector3 lookAtPositionOffset;

    public Vector3 savedCameraPosition;

    public bool playerAtConsole = false;

    public void Interact(GroundPlayerInteractor interactor) {
        Debug.Log("Interacting with console terminal");
        // lock the player in place and move the camera to the console terminal
        interactor.lockPlayer();

        // wait for a frame to ensure the player's position is updated before moving the camera
        // this is a bit of a hack and should be replaced with a more robust solution in the future
        StartCoroutine(MoveCameraNextFrame());
    }

    private System.Collections.IEnumerator MoveCameraNextFrame() {
        yield return null; // wait for a frame

        savedCameraPosition = Camera.main.transform.position;

        Vector3 targetCameraPosition = transform.position + cameraPositionOffset;
        Vector3 targetLookAtPosition = transform.position + lookAtPositionOffset;

        // Lerp to the target camera position and look at the target look at position over 0.5 seconds
        float elapsedTime = 0f;
        float duration = 0.5f;
        Vector3 startingCameraPosition = Camera.main.transform.position;
        Vector3 startingLookAtPosition = Camera.main.transform.position + Camera.main.transform.forward;

        while (elapsedTime < duration)
        {
            float t = elapsedTime / duration;
            Camera.main.transform.position = Vector3.Lerp(startingCameraPosition, targetCameraPosition, t);
            Vector3 currentLookAtPosition = Vector3.Lerp(startingLookAtPosition, targetLookAtPosition, t);
            Camera.main.transform.LookAt(currentLookAtPosition);

            elapsedTime += Time.deltaTime;
            yield return null;
        }
    }

    private System.Collections.IEnumerator MoveCameraBackNextFrame(GroundPlayerInteractor interactor) {
        yield return null; // wait for a frame

        Vector3 targetCameraPosition = savedCameraPosition;
        Vector3 targetLookAtPosition = transform.position + lookAtPositionOffset;

        // Lerp to the target camera position and look at the target look at position over 0.5 seconds
        float elapsedTime = 0f;
        float duration = 0.5f;
        Vector3 startingCameraPosition = Camera.main.transform.position;
        Vector3 startingLookAtPosition = transform.position + lookAtPositionOffset;

        while (elapsedTime < duration)
        {
            float t = elapsedTime / duration;
            Camera.main.transform.position = Vector3.Lerp(startingCameraPosition, targetCameraPosition, t);
            Vector3 currentLookAtPosition = Vector3.Lerp(startingLookAtPosition, targetLookAtPosition, t);
            Camera.main.transform.LookAt(currentLookAtPosition);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // unlock the player after moving the camera back

        interactor.unlockPlayer();
    }

    public void Escape(GroundPlayerInteractor interactor) {
        Debug.Log("Releasing console terminal");
        // unlock the player and move the camera back to its original position

        StartCoroutine(MoveCameraBackNextFrame(interactor));
    }

    public void OnScroll(GroundPlayerInteractor interactor, float vertical) {
        //nothing
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        // 
    }

    // need a way to lock the player and camera to fixed location away from console.
}
