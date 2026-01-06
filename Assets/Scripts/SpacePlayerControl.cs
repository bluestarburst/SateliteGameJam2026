// This first example shows how to move using Input System Package (New)

using UnityEngine;
using UnityEngine.InputSystem;

public class SpacePlayerControl : MonoBehaviour
{
    public GameObject playerCamera;
    private float playerSpeed = 5.0f;
    private Vector3 playerVelocity = Vector3.zero;
    private float jumpHeight = 1.5f;
    public float gravityValue = -9.81f;
    public CharacterController controller;
    public float sensitivity;


    public Vector3 groundAvgNormal = Vector3.up;
    public bool isGrounded;
    private bool groundedPlayer;


    [Header("Input Actions")]
    public InputActionReference moveAction;
    public InputActionReference jumpAction;

    private void OnEnable()
    {
        moveAction.action.Enable();
        jumpAction.action.Enable();
    }

    private void OnDisable()
    {
        moveAction.action.Disable();
        jumpAction.action.Disable();
    }


    void Update()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        Movement();
        Rotation();
    }

    public void Rotation()
    {
        // Vector3 mouseInput = new Vector3(-Input.GetAxis("Mouse Y"), Input.GetAxis("Mouse X"), 0);



        // lerp to the new normal to smooth out the rotation using shortest path rotation
        

        transform.up = Vector3.Lerp(transform.up + Vector3.one/1000, groundAvgNormal, Time.deltaTime * 5f);

        // new unity input system
        Vector2 mouseInput = Mouse.current.delta.ReadValue();

        // flip x and y for better control
        mouseInput = new Vector2(-mouseInput.y, mouseInput.x);

        playerCamera.transform.Rotate(mouseInput * sensitivity);

        Vector3 eulerRotation = playerCamera.transform.localRotation.eulerAngles;
        playerCamera.transform.localRotation = Quaternion.Euler(eulerRotation.x, eulerRotation.y, 0);
        // rotate by the ground avg normal to keep the player aligned with the ground
        // playerCamera.transform.rotation = Quaternion.FromToRotation(Vector3.up, groundAvgNormal) * playerCamera.transform.rotation;


    }

    public void Movement()
    {
        groundedPlayer = isGrounded;

        // Read input
        Vector2 input = moveAction.action.ReadValue<Vector2>();
        Vector3 move = new Vector3(input.x, 0, input.y);
        move = Vector3.ClampMagnitude(move, 1f);

        if (groundedPlayer)
        {
            playerVelocity.y = -2f;
        }

        // Jump using WasPressedThisFrame()
        if (groundedPlayer && jumpAction.action.WasPressedThisFrame())
        {
            playerVelocity.y = Mathf.Sqrt(jumpHeight * -2f * -9.81f);
            isGrounded = false;
        }

        // Apply gravity
        // playerVelocity.y += gravityValue * Time.deltaTime;

        // Move
        Vector3 finalMove = move * playerSpeed + Vector3.up * playerVelocity.y;


        // get player rotation along y axis and apply it to the movement vector
        finalMove = Quaternion.Euler(0, playerCamera.transform.localRotation.eulerAngles.y, 0) * finalMove;

        // translate the final move vector by the ground average normal to keep the player aligned with the ground
        finalMove = Quaternion.FromToRotation(Vector3.up, groundAvgNormal) * finalMove;

        controller.Move(finalMove * Time.deltaTime);
    }

}