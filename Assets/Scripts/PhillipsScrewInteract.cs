using UnityEngine;
using SatelliteGameJam.Gameplay.Puzzles;

public class PhillipsScrewInteract : MonoBehaviour, OInteractable
{
    [Header("Puzzle")]
    [SerializeField] private SatellitePuzzleComponent puzzle;
    [SerializeField] private bool usePuzzleTuning = true;

    [Header("Screw Settings")]
    public float pitch = 0.01f;     // meters per turn
    public float turns;
    public float minTurns = 0f;
    public float maxTurns = 10f;
    public float startingTurns = 1f;

    [Header("Interact Settings")]
    public float screwSpeed = 0.5f;
    public float unscrewSpeed = 0.01f;
    public float waitPeriod = 5f;

    [Header("Puzzle State")]
    [SerializeField] private float damagedAtTurns = 8f;
    [SerializeField] private float repairedAtTurns = 1f;
    [SerializeField] private bool reportSatelliteState = true;

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
        if (puzzle == null)
        {
            puzzle = GetComponent<SatellitePuzzleComponent>();
        }

        SetTurns(startingTurns);
    }

    void FixedUpdate() {
        if (!screwing) AddTurns(GetDecaySpeed() * Time.fixedDeltaTime);
        else {
            if (Time.time - timeSinceScrew > GetWaitPeriod()) screwing = false;
        }
    }

    public void Interact(OutsideSpacePlayerInteractor interactor) {
        screwing = true;
        timeSinceScrew = Time.time;
        AddTurns(-GetScrewSpeed());
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

        UpdatePuzzleState();
    }

    public void AddTurns(float delta)
    {
        SetTurns(turns + delta);
    }

    private float GetScrewSpeed()
    {
        if (!usePuzzleTuning || puzzle == null) return screwSpeed;
        return screwSpeed * puzzle.Tuning.RepairSpeedMultiplier;
    }

    private float GetDecaySpeed()
    {
        if (!usePuzzleTuning || puzzle == null) return unscrewSpeed;
        return unscrewSpeed * puzzle.Tuning.DecaySpeedMultiplier;
    }

    private float GetWaitPeriod()
    {
        if (!usePuzzleTuning || puzzle == null) return waitPeriod;
        return waitPeriod * puzzle.Tuning.GracePeriodMultiplier;
    }

    private void UpdatePuzzleState()
    {
        if (!reportSatelliteState || puzzle == null)
        {
            return;
        }

        float progress = Mathf.InverseLerp(damagedAtTurns, repairedAtTurns, turns);
        puzzle.SetProgress(progress);

        if (turns >= damagedAtTurns)
        {
            puzzle.MarkDamaged();
        }
        else if (turns <= repairedAtTurns)
        {
            puzzle.MarkRepaired();
        }
    }
}
