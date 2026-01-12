using SatelliteGameJam.Networking.State;
using SatelliteGameJam.Networking.Messages;
using UnityEngine;

namespace SatelliteGameJam.SceneManagers
{
    /// <summary>
    /// Scene bootstrap for Space Station.
    /// Sets local player role/scene and provides simple helpers to modify satellite state.
    /// </summary>
    public class SpaceStationSceneManager : MonoBehaviour
    {
        private void Start()
        {
            // Set player role and scene
            if (PlayerStateManager.Instance != null)
            {
                PlayerStateManager.Instance.SetLocalPlayerRole(PlayerRole.SpaceStation);
                PlayerStateManager.Instance.SetLocalPlayerScene(NetworkSceneId.SpaceStation);
            }
        }

        /// <summary>
        /// Example: Repair a component by index. Authority required (enforced by manager).
        /// </summary>
        public void RepairComponent(int componentIndex)
        {
            if (SatelliteStateManager.Instance != null)
            {
                SatelliteStateManager.Instance.SetComponentRepaired(componentIndex);
                Debug.Log($"[SpaceStation] Repaired component {componentIndex}");
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
