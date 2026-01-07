using UnityEngine;
using UnityEngine.InputSystem;

public class OutsideSpacePlayerControl : MonoBehaviour
{
    public GameObject playerCamera;
    public float playerSpeed = 5.0f;
    public float rollSpeed = 5.0f;
    public float sensitivity;

    private Vector3 playerVelocity = Vector3.zero;
    private float cameraRoll = 0.0f;

    public CharacterController controller;

    [Header("Input Actions")]
    public InputActionReference moveAction;
    public InputActionReference jumpAction;
    public InputActionReference crouchAction;

    private void OnEnable()
    {
        moveAction.action.Enable();
        jumpAction.action.Enable();
        crouchAction.action.Enable();
    }

    private void OnDisable()
    {
        moveAction.action.Disable();
        jumpAction.action.Disable();
        crouchAction.action.Disable();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        Rotation();
        ExteriorMovement();
    }

    public void Rotation()
    {
        // new unity input system
        Vector2 mouseInput = Mouse.current.delta.ReadValue();

        // E and Q for Roll (Camera Rotation)
        if (Keyboard.current.eKey.isPressed)
        {
            cameraRoll -= 0.1f;
        }
        else if (Keyboard.current.qKey.isPressed)
        {
            cameraRoll += 0.1f;
        }

        float currentRoll = cameraRoll;
        while (currentRoll > 180) currentRoll -= 360;
        while (currentRoll < -180) currentRoll += 360;

        // flip x and y for better control
        mouseInput = new Vector2(-mouseInput.y, mouseInput.x);
        transform.Rotate(mouseInput * sensitivity);

        // float x = transform.rotation.eulerAngles.x;
        // if (x > 180f) x -= 360f;

        // x = Mathf.Max(x, -85f);
        // x = Mathf.Min(x, 85f);

        // transform.rotation = Quaternion.Euler(x, transform.rotation.eulerAngles.y, 0);
    }

    public void ExteriorMovement()
    {
        Vector2 input = moveAction.action.ReadValue<Vector2>();
        Vector3 move = new Vector3(input.x, 0, input.y);
        move = Vector3.ClampMagnitude(move, 1f);

        Vector3 finalMove = playerSpeed * (move + playerVelocity);
        finalMove = Quaternion.Euler(0, transform.rotation.eulerAngles.y, 0) * finalMove;

        controller.Move(finalMove * Time.deltaTime);
    }
}
