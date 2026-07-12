using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class OutsideSpacePlayerControl : MonoBehaviour
{
    [Header("References")]
    public Transform cameraTransform;

    [Header("Rotation")]
    public float sensitivity = 0.1f;

    [Header("Movement")]
    public float maxSpeed = 10f;
    public float acceleration = 12f;
    public float deceleration = 8f;
    public float damping = 0.98f;

    [Header("Braking")]
    public float brakeStrength = 20f;
    public float brakeDamping = 0.95f;

    [Header("Rotation Inertia")]
    public float rollAcceleration = 120f;
    public float rollBrakeStrength = 200f;
    public float rollDamping = 0.9f;

    private float rollVelocity;

    [Header("Input Actions")]
    public InputActionReference moveAction;

    private Rigidbody rb;
    private Vector3 velocity;
    private float cameraRoll;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void OnEnable()
    {
        moveAction.action.Enable();
    }

    void OnDisable()
    {
        moveAction.action.Disable();
    }

    void Start()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        Rotation();
    }

    void FixedUpdate()
    {
        SpaceBrake();
        ExteriorMovement();
    }

    void SpaceBrake()
    {
        if (!Keyboard.current.xKey.isPressed)
            return;

        // Smoothly reduce velocity to zero
        velocity = Vector3.MoveTowards(
            velocity,
            Vector3.zero,
            brakeStrength * Time.fixedDeltaTime
        );

        // Extra damping for crisp stop
        velocity *= brakeDamping;
    }

    // ---------------- ROTATION ----------------
    void Rotation()
    {
        Vector2 mouseInput = Mouse.current.delta.ReadValue();

        float pitch = -mouseInput.y * sensitivity;
        float yaw   =  mouseInput.x * sensitivity;

        // Roll input → angular velocity
        if (Keyboard.current.eKey.isPressed)
            rollVelocity -= rollAcceleration * Time.deltaTime;

        if (Keyboard.current.qKey.isPressed)
            rollVelocity += rollAcceleration * Time.deltaTime;

        // Brake roll when X is held
        if (Keyboard.current.xKey.isPressed)
        {
            rollVelocity = Mathf.MoveTowards(
                rollVelocity,
                0f,
                rollBrakeStrength * Time.deltaTime
            );
        }
        else
        {
            // Natural rotational damping
            rollVelocity *= rollDamping;
        }

        Quaternion deltaRotation = Quaternion.Euler(
            pitch,
            yaw,
            rollVelocity * Time.deltaTime
        );

        rb.MoveRotation(rb.rotation * deltaRotation);
    }


    // ---------------- MOVEMENT ----------------
    void ExteriorMovement()
    {
        Vector2 input = moveAction.action.ReadValue<Vector2>();

        Vector3 inputDir =
            cameraTransform.forward * input.y +
            cameraTransform.right   * input.x;

        if (Keyboard.current.spaceKey.isPressed)
            inputDir += cameraTransform.up;

        if (Keyboard.current.leftCtrlKey.isPressed)
            inputDir -= cameraTransform.up;

        inputDir = Vector3.ClampMagnitude(inputDir, 1f);

        if (inputDir.sqrMagnitude > 0.001f)
        {
            velocity += inputDir * acceleration * Time.fixedDeltaTime;
        }
        else
        {
            velocity = Vector3.MoveTowards(
                velocity,
                Vector3.zero,
                deceleration * Time.fixedDeltaTime
            );
        }

        velocity = Vector3.ClampMagnitude(velocity, maxSpeed);
        velocity *= damping;

        rb.linearVelocity = velocity;
    }

    // ---------------- COLLISION BOUNCE ----------------
    void OnCollisionEnter(Collision collision)
    {
        Vector3 normal = collision.contacts[0].normal;

        // Reflect velocity for bounce
        velocity = Vector3.Reflect(velocity, normal);

        // Energy loss (tweak this)
        velocity *= 0.8f;

        rb.linearVelocity = velocity;
    }
}
