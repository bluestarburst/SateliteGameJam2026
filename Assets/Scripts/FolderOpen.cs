using UnityEngine;

public class FolderOpen : MonoBehaviour
{
    [SerializeField] private float openSpeed = 8f;

    private Vector3 baseLocalPos;
    private Quaternion baseLocalRot;
    private Transform baseParent;
    private FolderInteract folder;
    private Quaternion closedRot;
    private Quaternion openRot;

    [SerializeField]
    private bool isOpen;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        folder = GetComponentInParent<FolderInteract>();
        closedRot = transform.localRotation;
        openRot = closedRot * Quaternion.Euler(0f, 179f, 0f);
    }

    // Update is called once per frame
    void Update()
    {
        Quaternion targetRot = (folder != null && folder.open) ? openRot : closedRot;
        isOpen = folder != null && folder.open;

        transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRot, openSpeed * Time.deltaTime);
    }
}
