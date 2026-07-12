using System;
using System.Collections.Generic;
using UnityEngine;

namespace SatelliteGameJam.Gameplay.Puzzles
{
    [CreateAssetMenu(
        fileName = "PuzzleModeDefinition",
        menuName = "Satellite Game/Puzzles/Puzzle Mode Definition")]
    public class PuzzleModeDefinition : ScriptableObject
    {
        [SerializeField] private string modeId = "normal";
        [SerializeField] private string displayName = "Normal";
        [SerializeField] private PuzzleDifficulty defaultDifficulty = PuzzleDifficulty.Normal;
        [SerializeField] private List<PuzzleModeEntry> puzzles = new();

        public string ModeId => modeId;
        public string DisplayName => displayName;
        public PuzzleDifficulty DefaultDifficulty => defaultDifficulty;
        public IReadOnlyList<PuzzleModeEntry> Puzzles => puzzles;
    }

    [Serializable]
    public class PuzzleModeEntry
    {
        [SerializeField] private SatellitePuzzleDefinition puzzle;
        [SerializeField] private bool enabled = true;
        [SerializeField] private bool overrideDifficulty;
        [SerializeField] private PuzzleDifficulty difficulty = PuzzleDifficulty.Normal;

        public SatellitePuzzleDefinition Puzzle => puzzle;
        public bool Enabled => enabled;
        public bool OverrideDifficulty => overrideDifficulty;
        public PuzzleDifficulty Difficulty => difficulty;
    }
}
