using UnityEngine;
using UnityEngine.InputSystem;

public class GroundPlayerControl : MonoBehaviour
{

    public CharacterController controller;

    [Header("Input Actions")]
    public InputActionReference moveAction;
    public InputActionReference jumpAction;

    private float playerSpeed = 5.0f;

    public float gravityValue = -9.81f;
    public float sensitivity;

    private Vector3 playerVelocity;
    private bool groundedPlayer;

    // Update is called once per frame
    void Update()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        Movement();
        Rotation();
    }

    private void Rotation()
    {
        // new unity input system
        Vector2 mouseInput = Mouse.current.delta.ReadValue();

        // flip x and y for better control
        mouseInput = new Vector2(-mouseInput.y, mouseInput.x);
        transform.Rotate(mouseInput * sensitivity);

        float x = transform.rotation.eulerAngles.x;
        if (x > 180f) x -= 360f;

        x = Mathf.Max(x, -85f);
        x = Mathf.Min(x, 85f);

        transform.rotation = Quaternion.Euler(x, transform.rotation.eulerAngles.y, 0);
    }

    private void Movement()
    {
        groundedPlayer = controller.isGrounded;

        if (groundedPlayer)
        {
            // Slight downward velocity to keep grounded stable
            if (playerVelocity.y < -2f)
                playerVelocity.y = -2f;
        }

        // Read input
        Vector2 input = moveAction.action.ReadValue<Vector2>();
        Vector3 move = new Vector3(input.x, 0, input.y);
        move = Vector3.ClampMagnitude(move, 1f);

        // Jump using WasPressedThisFrame()
        // if (groundedPlayer && jumpAction.action.WasPressedThisFrame())
        // {
        //     playerVelocity.y = Mathf.Sqrt(jumpHeight * -2f * -9.81f);
        // }

        // // Apply gravity
        playerVelocity.y += gravityValue * Time.deltaTime;

        // Move
        Vector3 finalMove = playerSpeed * (move + playerVelocity);
        finalMove = Quaternion.Euler(0, transform.rotation.eulerAngles.y, 0) * finalMove;
        controller.Move(finalMove * Time.deltaTime);
    }
}
