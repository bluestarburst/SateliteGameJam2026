
using UnityEngine;
using UnityEngine.InputSystem;

public class SpacePlayerControl : MonoBehaviour
{
    public GameObject playerCamera;
    public float playerSpeed = 5.0f;
    public float rollSpeed = 5.0f;
    public float sensitivity;
    public float gravityValue = -9.81f;
    public bool hasBooster = false;

    private Vector3 playerVelocity = Vector3.zero;
    private float jumpHeight = 1.5f;

    public CharacterController controller;
    public Vector3 groundAvgNormal = Vector3.up;
    public bool isGrounded;
    public float nextGroundCheckTime;


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

    private bool canMove = false;
    // don't enable rotation/movement until 1s after start
    private void Start()
    {
        Invoke("EnableMovement", 1f);
    }

    private void EnableMovement()
    {
        canMove = true;
    }


    void Update()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        if (!canMove) return;
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
    private bool wasGrounded = false;
    private bool needsAlignment = true;

    private Vector3 lastNormal = Vector3.up;
    private Quaternion storeAngle = Quaternion.identity;
    public void Rotation()
    {
        bool justLanded = !wasGrounded && isGrounded;
        wasGrounded = isGrounded;

        if (justLanded)
        {
            lastNormal = groundAvgNormal;
        }

        if (!justLanded && lastNormal != groundAvgNormal)
        {
            // needsAlignment = Vector3.Distance(transform.up, groundAvgNormal) > 0.001f;
            // get the relative world rotation between lastNormal and groundAvgNormal for rotating the camera that amount
            storeAngle = Quaternion.FromToRotation(lastNormal, groundAvgNormal);
            // start a coroutine to rotate the camera by this rotation quaternion over time 
            StartCoroutine(RotateOverTime(storeAngle, 0.5f)); // rotate over 0.5 seconds
            lastNormal = groundAvgNormal;
        }

        // if (needsAlignment)
        // {

        //     // STANDARD WALKING ALIGNMENT (Same as before)
        //     Vector3 projectedForward = Vector3.ProjectOnPlane(transform.forward, groundAvgNormal).normalized;

        //     if (projectedForward.sqrMagnitude < 0.001f)
        //     {
        //         projectedForward = Vector3.ProjectOnPlane(transform.up, groundAvgNormal).normalized;
        //     }

        //     if (projectedForward.sqrMagnitude > 0.001f)
        //     {
        //         Quaternion targetRotation = Quaternion.LookRotation(projectedForward, groundAvgNormal);
        //         transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 10f * Time.deltaTime);
        //     }


        //     // Rotate the transform by the stored angle over time
        //     if (Vector3.Distance(transform.up, groundAvgNormal) <= 0.001f)
        //     {
        //         needsAlignment = false;
        //     }
        // }

        Vector2 mouseInput = Mouse.current.delta.ReadValue();

        float currentRoll = 0f;

        // E and Q for Roll (Camera Rotation)
        if (Keyboard.current.eKey.isPressed)
        {
            currentRoll = -0.1f;
        }
        else if (Keyboard.current.qKey.isPressed)
        {
            currentRoll = 0.1f;
        }

        while (currentRoll > 180) currentRoll -= 360;
        while (currentRoll < -180) currentRoll += 360;

        // Apply relative rotation to the camera
        playerCamera.transform.Rotate(-mouseInput.y * sensitivity * Time.deltaTime, mouseInput.x * sensitivity * Time.deltaTime, currentRoll * rollSpeed * Time.deltaTime, Space.Self);

    }

    // Nicholas was right from the start lol
    // Smoothly applies a fraction of a relative rotation to the camera over a fixed duration.
    // The rotation is applied incrementally (additive), so there is no fixed end orientation.
    private System.Collections.IEnumerator RotateOverTime(Quaternion rotationAmount, float duration, float fraction = 1f)
    {
        // Clamp fraction to [0,1] and compute the fractional target delta
        float clampedFraction = Mathf.Clamp01(fraction);
        Quaternion targetDelta = Quaternion.Slerp(Quaternion.identity, rotationAmount, clampedFraction);

        // Immediate application if duration is non-positive
        if (duration <= 0f)
        {
            playerCamera.transform.rotation = targetDelta * playerCamera.transform.rotation;
            yield break;
        }

        float elapsed = 0f;
        float lastElapsed = 0f;

        // Apply incremental delta each frame so that it adds onto the current rotation
        while (elapsed < duration)
        {
            yield return null;
            elapsed += Time.deltaTime;
            if (elapsed > duration) elapsed = duration;

            // Cumulative rotation up to current time and previous frame
            Quaternion cumulative = Quaternion.Slerp(Quaternion.identity, targetDelta, elapsed / duration);
            Quaternion previous = Quaternion.Slerp(Quaternion.identity, targetDelta, lastElapsed / duration);

            // Frame delta is the difference between cumulative and previous
            Quaternion frameDelta = cumulative * Quaternion.Inverse(previous);

            // Apply relative rotation (additive) to the camera
            playerCamera.transform.rotation = frameDelta * playerCamera.transform.rotation;

            lastElapsed = elapsed;
        }
    }

    private Vector3 jumpXZDirection = Vector3.zero;

    public int groundCount = 1;

    public void InteriorMovement()
    {

        Vector3 move;

        if (isGrounded)
        {
            Vector2 input = moveAction.action.ReadValue<Vector2>();
            move = new Vector3(input.x, 0, input.y);
            move = Vector3.ClampMagnitude(move, 1f);
            move = move * playerSpeed;
            if (groundCount == 0)
            {
                playerVelocity.y = -9.81f;
            }
            else
            {
                playerVelocity.y = -0.2f;
            }

            playerVelocity.x = move.x;
            playerVelocity.z = move.z;

            if (move == Vector3.zero)
            {
                playerVelocity.x = 0;
                playerVelocity.z = 0;
            }

            // move = transform.TransformDirection(new Vector3(playerVelocity.x, 0, playerVelocity.z));

            // get the forward of the camera projected onto the ground plane
            Vector3 cameraForward = Vector3.ProjectOnPlane(playerCamera.transform.forward, groundAvgNormal).normalized;
            Vector3 cameraRight = Vector3.ProjectOnPlane(playerCamera.transform.right, groundAvgNormal).normalized;

            move = cameraForward * move.z + cameraRight * move.x;

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
            nextGroundCheckTime = Time.time + 0.5f; // prevent immediate ground re-detection

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