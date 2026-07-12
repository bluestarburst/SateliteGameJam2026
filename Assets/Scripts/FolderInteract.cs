using UnityEngine;

public class FolderInteract : MonoBehaviour
{
    private Vector3 baseLocalPos;
    private Quaternion baseLocalRot;
    private Transform baseParent;
    public bool open = false;
    public Vector3 selectedOffset = new Vector3(0, 0.30f, 0);
    public Vector3 openOffset = new Vector3(0, 0.20f, 1f);
    public float riseSpeed = 10f;
    private Vector3 target;
    private Quaternion target_rot;
    
    public void Init()
    {
        baseLocalPos = transform.localPosition;
        baseLocalRot = transform.localRotation;
        baseParent = transform.parent;
    }

    public void SetSelected(bool selected)
    {
        target = baseLocalPos + (selected ? selectedOffset : Vector3.zero);
    }

    public void SetOpen(bool setopen, GroundPlayerInteractor interactor = null)
    {
        open = setopen;
        if (open && interactor)
        {
            Debug.Log("i'm opening");
            transform.SetParent(interactor.InteractorSource);
            target = Vector3.zero + openOffset;
            target_rot = Quaternion.Euler(-90f, 0f, -90f);
        } 
        else if (!open)
        {
            Debug.Log("i'm closing");
            transform.SetParent(baseParent);
            target = baseLocalPos + selectedOffset;
            target_rot = baseLocalRot;
        }
    }

    void Update() {
        transform.localPosition = Vector3.Lerp(
            transform.localPosition,
            target,
            Time.deltaTime * riseSpeed
        );
        transform.localRotation = Quaternion.Lerp(
            transform.localRotation,
            target_rot,
            Time.deltaTime * riseSpeed
        );
    }
}
