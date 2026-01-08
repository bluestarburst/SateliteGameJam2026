using UnityEngine;

public class PhillipsScrewInteract : MonoBehaviour, OInteractable
{
    [Header("Screw Settings")]
    public float pitch = 0.01f;     // meters per turn
    public float turns;
    public float minTurns = 0f;
    public float maxTurns = 10f;

    [Header("Interact Settings")]
    public float screwSpeed = 0.5f;
    public float unscrewSpeed = 0.01f;
    public float waitPeriod = 5f;

    private bool screwing = false;
    private float timeSinceScrew = -Mathf.Infinity;

    Vector3 basePosition;
    Quaternion baseRotation;
    Vector3 screwAxis;

    void Awake()
    {
        basePosition = transform.position;
        baseRotation = transform.rotation;

        // Cache the axis at rest (important!)
        screwAxis = baseRotation * Vector3.right;
    }

    void Start() {
        SetTurns(1);
    }

    void FixedUpdate() {
        if (!screwing) AddTurns(unscrewSpeed);
        else {
            if (Time.time - timeSinceScrew > waitPeriod) screwing = false;
        }
    }

    public void Interact(OutsideSpacePlayerInteractor interactor) {
        screwing = true;
        timeSinceScrew = Time.time;
        AddTurns(-screwSpeed);
    }

    public void SetTurns(float t)
    {
        turns = Mathf.Clamp(t, minTurns, maxTurns);

        // Rotate locally about the screw axis
        transform.rotation =
            baseRotation *
            Quaternion.AngleAxis(-turns * 360f, Vector3.right);

        // Translate along the FIXED screw axis
        transform.position =
            basePosition + screwAxis * (turns * pitch);
    }

    public void AddTurns(float delta)
    {
        SetTurns(turns + delta);
    }
}
