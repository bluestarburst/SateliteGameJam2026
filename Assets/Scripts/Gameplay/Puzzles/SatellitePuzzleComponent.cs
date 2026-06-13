using System;
using SatelliteGameJam.Networking;
using SatelliteGameJam.Networking.State;
using UnityEngine;

namespace SatelliteGameJam.Gameplay.Puzzles
{
    public class SatellitePuzzleComponent : MonoBehaviour
    {
        [Header("Definition")]
        [SerializeField] private SatellitePuzzleDefinition definition;
        [SerializeField] private PuzzleDifficulty difficulty = PuzzleDifficulty.Normal;

        [Header("Overrides")]
        [SerializeField] private bool overrideComponentIndex;
        [SerializeField, Range(0, 31)] private int componentIndex;

        [Header("Runtime")]
        [SerializeField, Range(0f, 1f)] private float progress;
        [SerializeField] private bool damaged;
        [SerializeField] private bool solved = true;

        public event Action<SatellitePuzzleComponent> OnDamaged;
        public event Action<SatellitePuzzleComponent> OnRepaired;
        public event Action<SatellitePuzzleComponent, float> OnProgressChanged;

        public SatellitePuzzleDefinition Definition => definition;
        public PuzzleDifficulty Difficulty => difficulty;
        public SatelliteModuleId ModuleId => definition != null ? definition.ModuleId : SatelliteModuleId.CoreSystems;
        public int ComponentIndex => overrideComponentIndex || definition == null ? componentIndex : definition.ComponentIndex;
        public bool IsDamaged => damaged;
        public bool IsSolved => solved;
        public float Progress => progress;

        public PuzzleTuning Tuning => definition != null
            ? definition.GetTuning(difficulty)
            : PuzzleTuning.Default(difficulty);

        public void SetDifficulty(PuzzleDifficulty newDifficulty)
        {
            difficulty = newDifficulty;
        }

        public bool MatchesDefinition(SatellitePuzzleDefinition candidate)
        {
            if (candidate == null || definition == null)
            {
                return false;
            }

            return definition == candidate || definition.PuzzleId == candidate.PuzzleId;
        }

        public void SetProgress(float normalizedProgress)
        {
            float nextProgress = Mathf.Clamp01(normalizedProgress);
            if (Mathf.Approximately(progress, nextProgress))
            {
                return;
            }

            progress = nextProgress;
            OnProgressChanged?.Invoke(this, progress);
        }

        public void MarkDamaged()
        {
            if (damaged)
            {
                return;
            }

            damaged = true;
            solved = false;
            ReportDamageState(true);
            OnDamaged?.Invoke(this);
        }

        public void MarkRepaired()
        {
            if (!damaged && solved)
            {
                return;
            }

            damaged = false;
            solved = true;
            SetProgress(1f);
            ReportDamageState(false);
            OnRepaired?.Invoke(this);
        }

        private void ReportDamageState(bool isDamaged)
        {
            if (GameFlowManager.Instance == null)
            {
                return;
            }

            if (isDamaged)
            {
                GameFlowManager.Instance.ReportSatelliteDamage(ComponentIndex);
            }
            else
            {
                GameFlowManager.Instance.ReportSatelliteRepair(ComponentIndex);
            }
        }

        private void OnValidate()
        {
            progress = Mathf.Clamp01(progress);
        }
    }
}
