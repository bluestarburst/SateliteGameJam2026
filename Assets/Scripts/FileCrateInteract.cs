using UnityEngine;

public class FileCrateInteract : MonoBehaviour, IInteractable
{
    public FolderInteract[] folders;
    public int selectedFolder = 0;
    
    private bool isHeld = false;
    private Rigidbody rb;
    private Collider cl;

    void Awake() {
        rb = GetComponent<Rigidbody>();
        cl = GetComponent<Collider>();
    }

    void Start() {
        for (int i = 0; i < folders.Length; i++) {
            folders[i].Init();
        }

        UpdateFolderVisuals();
    }

    public void Interact(GroundPlayerInteractor interactor) {
        if (!isHeld) {
            rb.isKinematic = true;
            rb.useGravity = false;
            cl.enabled = false;

            transform.SetParent(interactor.HoldPoint);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            
            isHeld = true;
        } else {
            transform.SetParent(null);

            rb.isKinematic = false;
            rb.useGravity = true;
            cl.enabled = true;

            isHeld = false;
        }
        
        // Debug.Log("I'll leap into your arms");
    }

    public void Click(GroundPlayerInteractor interactor) {
        if (folders == null) return;

        folders[selectedFolder].SetOpen(!folders[selectedFolder].open, interactor);
    }

    public void OnScroll(GroundPlayerInteractor interactor, float vertical) {
        if (vertical > 0) selectedFolder++;
        else selectedFolder--;

        if (selectedFolder >= folders.Length) selectedFolder = folders.Length-1;
        if (selectedFolder < 0) selectedFolder = 0;

        UpdateFolderVisuals();

        Debug.Log(selectedFolder);
    }

    void UpdateFolderVisuals() {
        if (folders == null) return;

        for (int i = 0; i < folders.Length; i++) {
            folders[i].SetSelected(i == selectedFolder);
        }
    }
}
