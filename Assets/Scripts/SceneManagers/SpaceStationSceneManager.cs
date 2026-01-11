using SatelliteGameJam.Networking.State;
using SatelliteGameJam.Networking.Messages;
using SatelliteGameJam.Networking.Core;
using Steamworks;
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
        [Header("Debug")]
        [SerializeField] private bool logDebug = false;

        private NetworkingConfiguration config;

        private void Start()
        {
            config = NetworkingConfiguration.Instance;

            // Set local player role and scene
            if (PlayerStateManager.Instance != null)
            {
                PlayerStateManager.Instance.SetLocalPlayerRole(PlayerRole.SpaceStation);
                PlayerStateManager.Instance.SetLocalPlayerScene(NetworkSceneId.SpaceStation);
                
                if (logDebug || (config != null && config.verboseLogging))
                {
                    Debug.Log("[SpaceStation] Set local player to Space Station scene/role");
                }
            }

            // Spawn Space Station players only
            SpawnSpaceStationPlayers();
        }

        /// <summary>
        /// Spawns remote player models for other Space Station players only.
        /// Refs: DeveloperExperienceImprovements.md Part 2
        /// </summary>
        private void SpawnSpaceStationPlayers()
        {
            if (SteamManager.Instance == null || PlayerStateManager.Instance == null)
            {
                if (logDebug) Debug.LogWarning("[SpaceStation] Cannot spawn players - managers not ready");
                return;
            }

            if (config == null || !config.autoSpawnPlayers)
            {
                if (logDebug) Debug.Log("[SpaceStation] Auto-spawn disabled in config");
                return;
            }

            int spawnedCount = 0;
            foreach (var member in SteamManager.Instance.currentLobby.Members)
            {
                if (member.Id == SteamManager.Instance.PlayerSteamId) continue;

                var playerState = PlayerStateManager.Instance.GetPlayerState(member.Id);
                
                // Only spawn players who are also in Space Station scene
                if (playerState.Scene == NetworkSceneId.SpaceStation)
                {
                    NetworkConnectionManager.Instance?.SpawnRemotePlayerFor(member.Id, member.Name);
                    spawnedCount++;
                }
            }

            if (logDebug || (config != null && config.verboseLogging))
            {
                Debug.Log($"[SpaceStation] Spawned {spawnedCount} remote Space Station players");
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
