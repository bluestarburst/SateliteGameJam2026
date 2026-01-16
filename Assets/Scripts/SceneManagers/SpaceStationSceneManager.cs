using System.Collections.Generic;
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
        private HashSet<SteamId> spawnedRemotePlayers = new HashSet<SteamId>();

        private void Start()
        {
            config = NetworkingConfiguration.Instance;

            // Set local player role and scene
            if (PlayerStateManager.Instance != null)
            {
                PlayerStateManager.Instance.SetLocalPlayerRole(PlayerRole.SpaceStation);
                PlayerStateManager.Instance.SetLocalPlayerScene(NetworkSceneId.SpaceStation);

                // Subscribe to scene changes so we can spawn late-arriving players
                PlayerStateManager.Instance.OnPlayerSceneChanged += OnRemotePlayerSceneChanged;

                if (logDebug || (config != null && config.verboseLogging))
                {
                    Debug.Log("[SpaceStation] Set local player to Space Station scene/role");
                }
            }

            // Spawn Space Station players only (delayed slightly to allow network messages to arrive)
            Invoke(nameof(SpawnSpaceStationPlayers), 0.1f);
        }

        private void OnDestroy()
        {
            if (PlayerStateManager.Instance != null)
            {
                PlayerStateManager.Instance.OnPlayerSceneChanged -= OnRemotePlayerSceneChanged;
            }
        }

        /// <summary>
        /// Called when any player's scene changes. Spawns remote players who enter SpaceStation.
        /// </summary>
        private void OnRemotePlayerSceneChanged(SteamId steamId, NetworkSceneId sceneId)
        {
            // Skip local player
            if (SteamManager.Instance != null && steamId == SteamManager.Instance.PlayerSteamId)
                return;

            if (sceneId == NetworkSceneId.SpaceStation)
            {
                // Player has entered Space Station scene - spawn them if not already spawned
                TrySpawnRemotePlayer(steamId);
            }
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
                // Also spawn players with SpaceStation role if their scene state hasn't arrived yet
                bool shouldSpawn = playerState.Scene == NetworkSceneId.SpaceStation ||
                                   (playerState.Scene == NetworkSceneId.None && playerState.Role == PlayerRole.SpaceStation);

                if (shouldSpawn && TrySpawnRemotePlayer(member.Id))
                {
                    spawnedCount++;
                }
            }

            if (logDebug || (config != null && config.verboseLogging))
            {
                Debug.Log($"[SpaceStation] Spawned {spawnedCount} remote Space Station players");
            }
        }

        /// <summary>
        /// Attempts to spawn a remote player if not already spawned.
        /// </summary>
        private bool TrySpawnRemotePlayer(SteamId steamId)
        {
            if (spawnedRemotePlayers.Contains(steamId))
            {
                if (logDebug) Debug.Log($"[SpaceStation] Player {steamId} already spawned, skipping");
                return false;
            }

            if (NetworkConnectionManager.Instance == null)
            {
                if (logDebug) Debug.LogWarning("[SpaceStation] NetworkConnectionManager not available");
                return false;
            }

            // Get display name from lobby members
            string displayName = "Unknown";
            if (SteamManager.Instance != null)
            {
                foreach (var member in SteamManager.Instance.currentLobby.Members)
                {
                    if (member.Id == steamId)
                    {
                        displayName = member.Name;
                        break;
                    }
                }
            }

            NetworkConnectionManager.Instance.SpawnRemotePlayerFor(steamId, displayName);
            spawnedRemotePlayers.Add(steamId);

            if (logDebug || (config != null && config.verboseLogging))
            {
                Debug.Log($"[SpaceStation] Spawned remote player: {displayName} ({steamId})");
            }

            return true;
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
