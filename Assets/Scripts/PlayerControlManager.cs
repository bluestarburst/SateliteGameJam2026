using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerControlManager : MonoBehaviour
{
    public bool ManCtrl;
    private bool InteriorCtrl;
    [SerializeField] private GameObject player;
    [SerializeField] private GameObject normalChild;
    [SerializeField] private GameObject groundCountChild;
    [SerializeField] private MonoBehaviour interiorControl;
    //[SerializeField] private MonoBehaviour interiorInteract;
    [SerializeField] private MonoBehaviour exteriorControl;
    [SerializeField] private MonoBehaviour exteriorInteract;

    private Rigidbody rb;
    private CharacterController cc;
    private SphereCollider sc;
    private MonoBehaviour[] scripts;
    private Camera mainCamera;

    private void Awake()
    {
        rb = player.GetComponent<Rigidbody>();
        cc = player.GetComponent<CharacterController>();
        sc = player.GetComponent<SphereCollider>();
        scripts = player.GetComponents<MonoBehaviour>();

        mainCamera = Camera.main;

        InteriorCtrl = !ManCtrl;
        SetInteriorCtrlEnabled(ManCtrl);
    }

    public void SetInteriorCtrlEnabled(bool enabled) {
        if (InteriorCtrl == enabled) return;
        
        InteriorCtrl = enabled;
        
        // rb.isKinematic = InteriorCtrl;
        
        if (InteriorCtrl) {
            rb.constraints &= ~RigidbodyConstraints.FreezeRotation;
        }
        else {
            rb.constraints |= RigidbodyConstraints.FreezeRotation;
        }

        cc.enabled = InteriorCtrl;
        sc.enabled = !InteriorCtrl;

        normalChild.SetActive(InteriorCtrl);
        groundCountChild.SetActive(InteriorCtrl);

        if (InteriorCtrl) {
            exteriorControl.enabled = false;
            exteriorInteract.enabled = false;
            interiorControl.enabled = true;
            //interiorInteract.enabled = true;
        } else {
            interiorControl.enabled = false;
            //interiorInteract.enabled = false;
            exteriorControl.enabled = true;
            exteriorInteract.enabled = true;
        }

        mainCamera.transform.rotation = Quaternion.Euler(0,0,0);
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        SetInteriorCtrlEnabled(ManCtrl);
    }
}
