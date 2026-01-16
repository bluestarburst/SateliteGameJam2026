using System.Collections.Generic;
using SatelliteGameJam.Networking.State;
using SatelliteGameJam.Networking.Messages;
using SatelliteGameJam.Networking.Core;
using SatelliteGameJam.Networking.Voice;
using Steamworks;
using UnityEngine;

namespace SatelliteGameJam.SceneManagers
{
    /// <summary>
    /// Scene-specific networking manager for Ground Control.
    /// Manages remote player spawning, console interaction voice gating, and satellite state UI updates.
    /// Refs: DeveloperExperienceImprovements.md Part 2
    /// </summary>
    public class GroundControlSceneManager : MonoBehaviour
    {
        [Header("Console Interaction")]
        [SerializeField] private bool isLocalPlayerAtConsole = false;

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
                PlayerStateManager.Instance.SetLocalPlayerRole(PlayerRole.GroundControl);
                PlayerStateManager.Instance.SetLocalPlayerScene(NetworkSceneId.GroundControl);

                // Subscribe to scene changes so we can spawn late-arriving players
                PlayerStateManager.Instance.OnPlayerSceneChanged += OnRemotePlayerSceneChanged;

                if (logDebug || (config != null && config.verboseLogging))
                {
                    Debug.Log("[GroundControl] Set local player to Ground Control scene/role");
                }
            }

            // Spawn Ground Control players only (delayed slightly to allow network messages to arrive)
            Invoke(nameof(SpawnGroundControlPlayers), 0.1f);

            // Set initial voice gating (not at console by default)
            UpdateVoiceGating();

            // Subscribe to satellite state changes for UI updates
            if (SatelliteStateManager.Instance != null)
            {
                SatelliteStateManager.Instance.OnHealthChanged += OnHealthChanged;
                SatelliteStateManager.Instance.OnComponentDamaged += OnComponentDamaged;
                SatelliteStateManager.Instance.OnComponentRepaired += OnComponentRepaired;
                SatelliteStateManager.Instance.OnConsoleStateChanged += OnConsoleStateChanged;
            }
        }

        private void OnDestroy()
        {
            if (PlayerStateManager.Instance != null)
            {
                PlayerStateManager.Instance.OnPlayerSceneChanged -= OnRemotePlayerSceneChanged;
            }

            if (SatelliteStateManager.Instance != null)
            {
                SatelliteStateManager.Instance.OnHealthChanged -= OnHealthChanged;
                SatelliteStateManager.Instance.OnComponentDamaged -= OnComponentDamaged;
                SatelliteStateManager.Instance.OnComponentRepaired -= OnComponentRepaired;
                SatelliteStateManager.Instance.OnConsoleStateChanged -= OnConsoleStateChanged;
            }
        }

        /// <summary>
        /// Called when any player's scene changes. Spawns remote players who enter GroundControl.
        /// </summary>
        private void OnRemotePlayerSceneChanged(SteamId steamId, NetworkSceneId sceneId)
        {
            // Skip local player
            if (SteamManager.Instance != null && steamId == SteamManager.Instance.PlayerSteamId)
                return;

            if (sceneId == NetworkSceneId.GroundControl)
            {
                // Player has entered Ground Control scene - spawn them if not already spawned
                TrySpawnRemotePlayer(steamId);
            }
        }

        /// <summary>
        /// Spawns remote player models for other Ground Control players only.
        /// Refs: DeveloperExperienceImprovements.md Part 2
        /// </summary>
        private void SpawnGroundControlPlayers()
        {
            if (SteamManager.Instance == null || PlayerStateManager.Instance == null)
            {
                if (logDebug) Debug.LogWarning("[GroundControl] Cannot spawn players - managers not ready");
                return;
            }

            if (config == null || !config.autoSpawnPlayers)
            {
                if (logDebug) Debug.Log("[GroundControl] Auto-spawn disabled in config");
                return;
            }

            int spawnedCount = 0;
            foreach (var member in SteamManager.Instance.currentLobby.Members)
            {
                if (member.Id == SteamManager.Instance.PlayerSteamId) continue;

                var playerState = PlayerStateManager.Instance.GetPlayerState(member.Id);

                // Only spawn players who are also in Ground Control scene
                // Also spawn players with GroundControl role if their scene state hasn't arrived yet
                bool shouldSpawn = playerState.Scene == NetworkSceneId.GroundControl ||
                                   (playerState.Scene == NetworkSceneId.None && playerState.Role == PlayerRole.GroundControl);

                if (shouldSpawn && TrySpawnRemotePlayer(member.Id))
                {
                    spawnedCount++;
                }
            }

            if (logDebug || (config != null && config.verboseLogging))
            {
                Debug.Log($"[GroundControl] Spawned {spawnedCount} remote Ground Control players");
            }
        }

        /// <summary>
        /// Attempts to spawn a remote player if not already spawned.
        /// </summary>
        private bool TrySpawnRemotePlayer(SteamId steamId)
        {
            if (spawnedRemotePlayers.Contains(steamId))
            {
                if (logDebug) Debug.Log($"[GroundControl] Player {steamId} already spawned, skipping");
                return false;
            }

            if (NetworkConnectionManager.Instance == null)
            {
                if (logDebug) Debug.LogWarning("[GroundControl] NetworkConnectionManager not available");
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
                Debug.Log($"[GroundControl] Spawned remote player: {displayName} ({steamId})");
            }

            return true;
        }

        /// <summary>
        /// Updates voice gating based on console interaction state.
        /// Refs: GameFlowArchitecture.md - Voice Chat section
        /// </summary>
        private void UpdateVoiceGating()
        {
            if (VoiceSessionManager.Instance != null)
            {
                VoiceSessionManager.Instance.SetLocalPlayerAtConsole(isLocalPlayerAtConsole);
                
                if (logDebug || (config != null && config.verboseLogging))
                {
                    Debug.Log($"[GroundControl] Voice gating updated - At console: {isLocalPlayerAtConsole}");
                }
            }
        }

        /// <summary>
        /// Call this when player starts interacting with the transmission console.
        /// Enables hearing Space Station players through the console.
        /// </summary>
        public void OnConsoleInteractionStarted()
        {
            isLocalPlayerAtConsole = true;
            UpdateVoiceGating();
            
            if (logDebug) Debug.Log("[GroundControl] Console interaction STARTED - can now hear Space players");
        }

        /// <summary>
        /// Call this when player exits the transmission console.
        /// Disables hearing Space Station players.
        /// </summary>
        public void OnConsoleInteractionEnded()
        {
            isLocalPlayerAtConsole = false;
            UpdateVoiceGating();
            
            if (logDebug) Debug.Log("[GroundControl] Console interaction ENDED - cannot hear Space players");
        }

        private void OnHealthChanged(float health)
        {
            // TODO: Hook up to UI health bar
            Debug.Log($"[GroundControl] Satellite health: {health}%");
        }

        private void OnComponentDamaged(uint componentIndex)
        {
            // TODO: Show damage indicator on UI
            Debug.Log($"[GroundControl] Component {componentIndex} damaged");
        }

        private void OnComponentRepaired(uint componentIndex)
        {
            // TODO: Clear damage indicator on UI
            Debug.Log($"[GroundControl] Component {componentIndex} repaired");
        }

        private void OnConsoleStateChanged(uint consoleId, ConsoleStateData state)
        {
            // TODO: Update console screen/display
            Debug.Log($"[GroundControl] Console {consoleId} state changed: {state.StateByte}");
        }
    }
}
