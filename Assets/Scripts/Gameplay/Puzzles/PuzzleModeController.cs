using UnityEngine;

namespace SatelliteGameJam.Gameplay.Puzzles
{
    public class PuzzleModeController : MonoBehaviour
    {
        [SerializeField] private PuzzleModeDefinition modeDefinition;
        [SerializeField] private PuzzleDifficulty fallbackDifficulty = PuzzleDifficulty.Normal;
        [SerializeField] private bool applyOnStart = true;
        [SerializeField] private bool includeInactivePuzzles = true;
        [SerializeField] private bool disableUnlistedPuzzles;

        public PuzzleModeDefinition ModeDefinition => modeDefinition;

        private void Start()
        {
            if (applyOnStart)
            {
                ApplyMode();
            }
        }

        public void SetMode(PuzzleModeDefinition nextMode)
        {
            modeDefinition = nextMode;
            ApplyMode();
        }

        public void ApplyMode()
        {
            var puzzles = FindObjectsByType<SatellitePuzzleComponent>(
                includeInactivePuzzles ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            foreach (var puzzle in puzzles)
            {
                ApplyModeToPuzzle(puzzle);
            }
        }

        private void ApplyModeToPuzzle(SatellitePuzzleComponent puzzle)
        {
            if (puzzle == null)
            {
                return;
            }

            if (TryFindEntry(puzzle, out var entry))
            {
                puzzle.SetDifficulty(entry.OverrideDifficulty ? entry.Difficulty : modeDefinition.DefaultDifficulty);
                puzzle.gameObject.SetActive(entry.Enabled);
                return;
            }

            puzzle.SetDifficulty(modeDefinition != null ? modeDefinition.DefaultDifficulty : fallbackDifficulty);

            if (disableUnlistedPuzzles)
            {
                puzzle.gameObject.SetActive(false);
            }
        }

        private bool TryFindEntry(SatellitePuzzleComponent puzzle, out PuzzleModeEntry entry)
        {
            entry = null;
            if (modeDefinition == null)
            {
                return false;
            }

            foreach (var candidate in modeDefinition.Puzzles)
            {
                if (candidate?.Puzzle == null)
                {
                    continue;
                }

                if (puzzle.MatchesDefinition(candidate.Puzzle))
                {
                    entry = candidate;
                    return true;
                }
            }

            return false;
        }
    }
}
