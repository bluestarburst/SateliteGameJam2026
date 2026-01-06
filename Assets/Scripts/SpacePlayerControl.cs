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
        Rotation();
        Movement();
    }

    private Quaternion tmpRotation = Quaternion.identity;
    private float cameraPitch = 0.0f;
    public void Rotation()
    {
        // 1. GRAVITY / NORMAL ALIGNMENT

        // Check if we are aligned with the ground normal
        // We use a larger threshold (0.001f) to ensure smoother stops
        if (Vector3.Distance(transform.up, groundAvgNormal) > 0.001f)
        {
            // A. Standard Alignment
            // Project current forward onto the new ground plane.
            // This keeps the "Compass" direction the same while changing the "Up".
            Vector3 projectedForward = Vector3.ProjectOnPlane(transform.forward, groundAvgNormal).normalized;

            // B. Edge Case: "Gimbal Lock" / Looking Straight at Normal
            // If projectedForward is zero (e.g., looking straight down while gravity flips down),
            // we can't use 'forward' to determine orientation.
            // We fallback to projecting the Camera's Up vector or just existing transform.up.
            if (projectedForward.sqrMagnitude < 0.001f)
            {
                // Fallback: Try to maintain the current rotation logic by using the camera's up 
                // projected on the plane, effectively treating "Up" as the new "Forward" temporarily.
                projectedForward = Vector3.ProjectOnPlane(transform.up, groundAvgNormal).normalized;
            }

            // C. Apply Rotation
            // Only apply if we have a valid direction
            if (projectedForward.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(projectedForward, groundAvgNormal);

                // Slerp handles the 180 flip smoothly. 
                // If the flip is exactly 180, Slerp will pick the shortest path (pitch or roll).
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 10f * Time.deltaTime);
            }
        }

        // 2. MOUSE INPUT

        Vector2 mouseInput = Mouse.current.delta.ReadValue();

        // Yaw (Body Rotation)
        transform.Rotate(Vector3.up, mouseInput.x * sensitivity);

        // Pitch (Camera Rotation)
        cameraPitch -= mouseInput.y * sensitivity;
        cameraPitch = Mathf.Clamp(cameraPitch, -85f, 85f);
        playerCamera.transform.localRotation = Quaternion.Euler(cameraPitch, 0, 0);
    }

    public void Movement()
    {

        Vector3 move = Vector3.zero;

        if (isGrounded)
        {
            Vector2 input = moveAction.action.ReadValue<Vector2>();
            move = new Vector3(input.x, 0, input.y);
            move = Vector3.ClampMagnitude(move, 1f);
            move = move * playerSpeed;
            playerVelocity.y = -1f;
;
            playerVelocity.x = move.x;
            playerVelocity.z = move.z;

            if (move == Vector3.zero)
            {
                playerVelocity.x = 0;
                playerVelocity.z = 0;
            }
        }

        // Jump using WasPressedThisFrame()
        if (isGrounded && jumpAction.action.WasPressedThisFrame())
        {
            playerVelocity.y = Mathf.Sqrt(jumpHeight * -2f * -9.81f);
            isGrounded = false;
        }

        // Apply gravity
        // playerVelocity.y += gravityValue * Time.deltaTime;

        // move from local space to transform forward and right
        move = transform.TransformDirection(new Vector3(playerVelocity.x, 0, playerVelocity.z));

        // Move
        Vector3 finalMove = move + groundAvgNormal * playerVelocity.y;



        // translate the final move vector by the ground average normal to keep the player aligned with the ground
        // finalMove = Quaternion.FromToRotation(Vector3.up, groundAvgNormal) * finalMove;

        // rotate the final move to match the forward vector of the camera not the current transform
        // finalMove = Quaternion.Euler(0, playerCamera.transform.rotation.eulerAngles.y, 0) * finalMove;

        controller.Move(finalMove * Time.deltaTime);
    }

}