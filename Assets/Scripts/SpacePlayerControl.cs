using UnityEngine;
using UnityEngine.InputSystem;

public class SpacePlayerControl : MonoBehaviour
{
    public GameObject playerCamera;
    private float playerSpeed = 5.0f;
    private Vector3 playerVelocity = Vector3.zero;
    private float jumpHeight = 1.5f;
    public float gravityValue = -9.81f;

    public bool hasBooster = false;


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
        if (isGrounded)
        {
            InteriorMovement();
        }
        else if (hasBooster)
        {
            BoosterMovement();
        }
        else
        {
            InteriorMovement();
        }
    }
    private float cameraPitch = 0.0f;
    private float cameraRoll = 0.0f;
    private bool wasGrounded;
    public void Rotation()
    {
        bool justLanded = isGrounded && !wasGrounded;
        wasGrounded = isGrounded;

        // --- 1. GRAVITY / NORMAL ALIGNMENT ---

        if (justLanded)
        {
            // VIEW STABILIZATION LOGIC

            // A. Capture the exact World Rotation of the camera before we mess with the body
            Quaternion frozenCamWorldRot = playerCamera.transform.rotation;

            // B. Snap the Player Body to the new Surface Normal instantly
            Vector3 projectedForward = Vector3.ProjectOnPlane(transform.forward, groundAvgNormal).normalized;
            if (projectedForward.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(projectedForward, groundAvgNormal);
            }

            // C. Restore the Camera's World Rotation 
            // The body has moved, so we force the camera back to where it was looking in World Space
            playerCamera.transform.rotation = frozenCamWorldRot;

            // D. Recalculate Local Pitch and Roll
            // Because the parent (Body) moved but the child (Camera) stayed still, 
            // the Local Rotation of the camera has changed. We must update our variables to match.
            Vector3 newLocalEuler = playerCamera.transform.localEulerAngles;

            // Update Pitch (Wrap angle to -180 to 180 for clamping)
            cameraPitch = newLocalEuler.x;
            if (cameraPitch > 180) cameraPitch -= 360;

            // Update Roll (Crucial for keeping "World Up" different from "Player Up")
            cameraRoll = newLocalEuler.z;
        }
        else if (Vector3.Distance(transform.up, groundAvgNormal) > 0.001f)
        {
            // STANDARD WALKING ALIGNMENT (Same as before)
            Vector3 projectedForward = Vector3.ProjectOnPlane(transform.forward, groundAvgNormal).normalized;

            if (projectedForward.sqrMagnitude < 0.001f)
            {
                projectedForward = Vector3.ProjectOnPlane(transform.up, groundAvgNormal).normalized;
            }

            if (projectedForward.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(projectedForward, groundAvgNormal);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 10f * Time.deltaTime);
            }
        }

        // --- 2. MOUSE INPUT ---

        Vector2 mouseInput = Mouse.current.delta.ReadValue();

        // Yaw (Body Rotation) - Rotates around the player's current Up
        transform.Rotate(Vector3.up, mouseInput.x * sensitivity);

        // Pitch (Camera Rotation)
        cameraPitch -= mouseInput.y * sensitivity;
        cameraPitch = Mathf.Clamp(cameraPitch, -85f, 85f);

        // Apply Pitch AND Roll
        // We now include 'cameraRoll' in the Z axis instead of forcing 0
        playerCamera.transform.localRotation = Quaternion.Euler(cameraPitch, 0, cameraRoll);
    }

    private Vector3 jumpXZDirection = Vector3.zero;

    public void InteriorMovement()
    {

        Vector3 move;

        if (isGrounded)
    {
        Vector2 input = moveAction.action.ReadValue<Vector2>();

        // --- NEW: INPUT ROTATION CORRECTION ---
        // Get the camera's current Roll (Z-rotation) relative to the body
        float cameraRollAngle = playerCamera.transform.localEulerAngles.z;

        // Create a rotation that cancels out the camera roll.
        // We rotate around the Y-axis because our movement input is on the flat XZ plane.
        // We use NEGATIVE roll because: 
        // If Camera is rolled +90 (Left is Down), pushing W (Up) needs to move Body Left (-90).
        Quaternion inputRotation = Quaternion.Euler(0, -cameraRollAngle, 0);

        // Convert 2D input to 3D (x, 0, y) and apply the rotation
        Vector3 inputDir = new Vector3(input.x, 0, input.y);
        inputDir = inputRotation * inputDir;
        // --------------------------------------

        // Continue with the rest of your logic using 'inputDir' instead of creating new vector
        move = inputDir; 
        move = Vector3.ClampMagnitude(move, 1f);
        move = move * playerSpeed;
        
        playerVelocity.y = -1f;
        playerVelocity.x = move.x;
        playerVelocity.z = move.z;

        if (move == Vector3.zero)
        {
            playerVelocity.x = 0;
            playerVelocity.z = 0;
        }

        move = transform.TransformDirection(new Vector3(playerVelocity.x, 0, playerVelocity.z));
    }
        else
        {
            move = jumpXZDirection;
        }

        // Jump using WasPressedThisFrame()
        if (isGrounded && jumpAction.action.WasPressedThisFrame())
        {
            playerVelocity.y = Mathf.Sqrt(jumpHeight * -2f * -9.81f);
            isGrounded = false;

            jumpXZDirection = move * 1.5f;
        }

        // Move with 
        Vector3 finalMove = move + groundAvgNormal * playerVelocity.y;

        controller.Move(finalMove * Time.deltaTime);
    }

    public void BoosterMovement()
    {
        // Spacebar should boost the player in the 
    }

}