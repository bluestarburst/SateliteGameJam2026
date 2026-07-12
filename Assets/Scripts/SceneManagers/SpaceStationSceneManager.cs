using SatelliteGameJam.Networking;
using SatelliteGameJam.Networking.State;
using UnityEngine;

namespace SatelliteGameJam.SceneManagers
{
    /// <summary>
    /// Scene-specific networking manager for Space Station.
    /// Manages remote player spawning for Space Station players only.
    /// Space players always hear Ground Control players (when at console).
    /// Refs: DeveloperExperienceImprovements.md Part 2
    /// </summary>
    public class SpaceStationSceneManager : MonoBehaviour
    {
        /// <summary>
        /// Example: Repair a component by index. Authority required (enforced by manager).
        /// </summary>
        public void RepairComponent(int componentIndex)
        {
            if (GameFlowManager.Instance != null)
            {
                GameFlowManager.Instance.ReportSatelliteRepair(componentIndex);
                Debug.Log($"[SpaceStation] Repaired component {componentIndex}");
            }
            else
            {
                SatelliteStateManager.Instance?.RequestComponentRepaired(componentIndex);
            }
        }

        /// <summary>
        /// Example: Apply damage to the satellite.
        /// </summary>
        public void DamageSatellite(float damage)
        {
            if (SatelliteStateManager.Instance != null)
            {
                float currentHealth = SatelliteStateManager.Instance.GetHealth();
                SatelliteStateManager.Instance.SetHealth(currentHealth - Mathf.Abs(damage));
                Debug.Log($"[SpaceStation] Damaged satellite by {damage}");
            }
        }
    }
}
