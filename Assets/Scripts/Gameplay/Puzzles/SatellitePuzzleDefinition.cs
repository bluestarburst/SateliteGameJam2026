using SatelliteGameJam.Networking.State;
using UnityEngine;

namespace SatelliteGameJam.Gameplay.Puzzles
{
    [CreateAssetMenu(
        fileName = "SatellitePuzzleDefinition",
        menuName = "Satellite Game/Puzzles/Satellite Puzzle Definition")]
    public class SatellitePuzzleDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string puzzleId = "new_puzzle";
        [SerializeField] private string displayName = "New Puzzle";
        [SerializeField] private SatelliteModuleId moduleId = SatelliteModuleId.CoreSystems;
        [SerializeField, Range(0, 31)] private int componentIndex;
        [SerializeField] private bool enabledByDefault = true;

        [Header("Tuning")]
        [SerializeField] private PuzzleTuning easy = PuzzleTuning.Default(PuzzleDifficulty.Easy);
        [SerializeField] private PuzzleTuning normal = PuzzleTuning.Default(PuzzleDifficulty.Normal);
        [SerializeField] private PuzzleTuning hard = PuzzleTuning.Default(PuzzleDifficulty.Hard);
        [SerializeField] private PuzzleTuning custom = PuzzleTuning.Default(PuzzleDifficulty.Custom);

        public string PuzzleId => puzzleId;
        public string DisplayName => displayName;
        public SatelliteModuleId ModuleId => moduleId;
        public int ComponentIndex => componentIndex;
        public bool EnabledByDefault => enabledByDefault;

        public PuzzleTuning GetTuning(PuzzleDifficulty difficulty)
        {
            switch (difficulty)
            {
                case PuzzleDifficulty.Easy:
                    return easy;
                case PuzzleDifficulty.Hard:
                    return hard;
                case PuzzleDifficulty.Custom:
                    return custom;
                default:
                    return normal;
            }
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(puzzleId))
            {
                puzzleId = name;
            }
        }
    }
}
