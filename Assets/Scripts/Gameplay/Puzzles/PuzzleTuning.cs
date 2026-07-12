using System;
using UnityEngine;

namespace SatelliteGameJam.Gameplay.Puzzles
{
    [Serializable]
    public struct PuzzleTuning
    {
        public PuzzleDifficulty Difficulty;

        [Min(0.01f)] public float ComplexityMultiplier;
        [Min(0.01f)] public float RepairSpeedMultiplier;
        [Min(0.01f)] public float DecaySpeedMultiplier;
        [Min(0.01f)] public float GracePeriodMultiplier;
        [Min(0f)] public float HealthDamage;

        public static PuzzleTuning Default(PuzzleDifficulty difficulty)
        {
            switch (difficulty)
            {
                case PuzzleDifficulty.Easy:
                    return new PuzzleTuning
                    {
                        Difficulty = difficulty,
                        ComplexityMultiplier = 0.75f,
                        RepairSpeedMultiplier = 1.25f,
                        DecaySpeedMultiplier = 0.75f,
                        GracePeriodMultiplier = 1.5f,
                        HealthDamage = 5f
                    };
                case PuzzleDifficulty.Hard:
                    return new PuzzleTuning
                    {
                        Difficulty = difficulty,
                        ComplexityMultiplier = 1.35f,
                        RepairSpeedMultiplier = 0.85f,
                        DecaySpeedMultiplier = 1.5f,
                        GracePeriodMultiplier = 0.65f,
                        HealthDamage = 15f
                    };
                default:
                    return new PuzzleTuning
                    {
                        Difficulty = difficulty,
                        ComplexityMultiplier = 1f,
                        RepairSpeedMultiplier = 1f,
                        DecaySpeedMultiplier = 1f,
                        GracePeriodMultiplier = 1f,
                        HealthDamage = 10f
                    };
            }
        }
    }
}
