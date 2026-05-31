using UnityEngine;

public class FolderInteract : MonoBehaviour
{
    private Vector3 baseLocalPos;
    public Vector3 selectedOffset = new Vector3(0, 0.30f, 0);
    public float riseSpeed = 10f;
    private Vector3 target;
    
    public void Init()
    {
        baseLocalPos = transform.localPosition;
    }

    public void SetSelected(bool selected)
    {
        target = baseLocalPos + (selected ? selectedOffset : Vector3.zero);
    }

    void Update() {
        transform.localPosition = Vector3.Lerp(
            transform.localPosition,
            target,
            Time.deltaTime * riseSpeed
        );
    }
}
