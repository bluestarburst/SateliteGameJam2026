using System.Collections.Generic;
using Steamworks;
using UnityEngine;
using SatelliteGameJam.Networking.Core;
using SatelliteGameJam.Networking.State;
using SatelliteGameJam.Networking.Voice;
using SatelliteGameJam.Networking.Messages;

namespace SatelliteGameJam.SceneManagers
{
    /// <summary>
    /// Scene-specific networking manager for Lobby scene.
    /// Spawns remote player models, manages lobby voice chat, handles ready states.
    /// All players can hear each other in the lobby.
    /// Refs: DeveloperExperienceImprovements.md Part 2
    /// </summary>
    public class LobbyNetworkingManager : MonoBehaviour
    {
        [Header("Player Spawning")]
        [SerializeField] private Transform playerSpawnParent;
        [SerializeField] private Vector3 playerSpawnOffset = Vector3.zero;

        [Header("Debug")]
        [SerializeField] private bool logDebug = false;

        private NetworkingConfiguration config;
        private List<SteamId> spawnedPlayers = new();

        private void Start()
        {
            config = NetworkingConfiguration.Instance;

            // Set local player state to Lobby
            if (PlayerStateManager.Instance != null)
            {
                PlayerStateManager.Instance.SetLocalPlayerScene(NetworkSceneId.Lobby);
                PlayerStateManager.Instance.SetLocalPlayerRole(PlayerRole.Lobby);
                
                if (logDebug || (config != null && config.verboseLogging))
                {
                    Debug.Log("[Lobby] Set local player to Lobby scene/role");
                }
            }

            // Spawn existing lobby members
            SpawnExistingPlayers();

            // Subscribe to player join/leave events
            if (PlayerStateManager.Instance != null)
            {
                PlayerStateManager.Instance.OnPlayerJoined += OnRemotePlayerJoined;
                PlayerStateManager.Instance.OnPlayerLeft += OnRemotePlayerLeft;
            }

            // Voice chat: everyone can hear everyone in lobby
            if (VoiceSessionManager.Instance != null)
            {
                VoiceSessionManager.Instance.SetLocalPlayerAtConsole(true); // Lobby = always hear everyone
                
                if (logDebug) Debug.Log("[Lobby] Voice chat enabled for all players");
            }
        }

        private void OnDestroy()
        {
            // Unsubscribe from events
            if (PlayerStateManager.Instance != null)
            {
                PlayerStateManager.Instance.OnPlayerJoined -= OnRemotePlayerJoined;
                PlayerStateManager.Instance.OnPlayerLeft -= OnRemotePlayerLeft;
            }

            // Clean up spawned players
            CleanupSpawnedPlayers();
        }

        /// <summary>
        /// Spawns player models for all existing lobby members (except local player).
        /// </summary>
        private void SpawnExistingPlayers()
        {
            if (SteamManager.Instance == null)
            {
                if (logDebug) Debug.LogWarning("[Lobby] Cannot spawn players - SteamManager not ready");
                return;
            }

            if (config == null || !config.autoSpawnPlayers)
            {
                if (logDebug) Debug.Log("[Lobby] Auto-spawn disabled in config");
                return;
            }

            int spawnedCount = 0;
            foreach (var member in SteamManager.Instance.currentLobby.Members)
            {
                if (member.Id != SteamManager.Instance.PlayerSteamId)
                {
                    SpawnRemotePlayer(member.Id, member.Name);
                    spawnedCount++;
                }
            }

            if (logDebug || (config != null && config.verboseLogging))
            {
                Debug.Log($"[Lobby] Spawned {spawnedCount} existing lobby players");
            }
        }

        /// <summary>
        /// Spawns a remote player model for the given SteamId.
        /// </summary>
        private void SpawnRemotePlayer(SteamId steamId, string displayName)
        {
            if (spawnedPlayers.Contains(steamId))
            {
                if (logDebug) Debug.LogWarning($"[Lobby] Player {displayName} already spawned");
                return;
            }

            if (config == null || config.remotePlayerPrefab == null)
            {
                if (logDebug) Debug.LogWarning("[Lobby] No player prefab configured");
                return;
            }

            // Spawn via NetworkConnectionManager
            NetworkConnectionManager.Instance?.SpawnRemotePlayerFor(steamId, displayName);

            spawnedPlayers.Add(steamId);

            // Register with voice system for proximity audio
            // Note: In lobby, proximity doesn't matter - everyone hears everyone
            if (VoiceSessionManager.Instance != null)
            {
                // Get the spawned player GameObject
                var playerGO = GameObject.Find($"RemotePlayer_{displayName}");
                if (playerGO != null)
                {
                    VoiceSessionManager.Instance.RegisterRemotePlayerAvatar(steamId, playerGO);
                }
            }

            if (logDebug || (config != null && config.verboseLogging))
            {
                Debug.Log($"[Lobby] Spawned remote player: {displayName}");
            }
        }

        /// <summary>
        /// Called when a new player joins the lobby.
        /// </summary>
        private void OnRemotePlayerJoined(SteamId steamId)
        {
            if (logDebug) Debug.Log($"[Lobby] Player joined: {steamId}");

            // Find player in lobby members and spawn if found
            if (SteamManager.Instance != null && SteamManager.Instance.currentLobby.MemberCount > 0)
            {
                foreach (var member in SteamManager.Instance.currentLobby.Members)
                {
                    if (member.Id == steamId)
                    {
                        SpawnRemotePlayer(steamId, member.Name);
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Called when a player leaves the lobby.
        /// </summary>
        private void OnRemotePlayerLeft(SteamId steamId)
        {
            if (logDebug) Debug.Log($"[Lobby] Player left: {steamId}");

            if (spawnedPlayers.Contains(steamId))
            {
                spawnedPlayers.Remove(steamId);

                // Despawn via NetworkConnectionManager
                NetworkConnectionManager.Instance?.DespawnRemotePlayer(steamId);

                // Unregister from voice system
                if (VoiceSessionManager.Instance != null)
                {
                    VoiceSessionManager.Instance.UnregisterRemotePlayer(steamId);
                }
            }
        }

        /// <summary>
        /// Cleans up all spawned player models.
        /// </summary>
        private void CleanupSpawnedPlayers()
        {
            if (logDebug) Debug.Log($"[Lobby] Cleaning up {spawnedPlayers.Count} spawned players");

            foreach (var steamId in spawnedPlayers)
            {
                NetworkConnectionManager.Instance?.DespawnRemotePlayer(steamId);
            }

            spawnedPlayers.Clear();
        }

        // ===== PUBLIC API FOR UI =====

        /// <summary>
        /// Call this when the local player clicks the Ready button.
        /// Marks the player as ready to start the game.
        /// </summary>
        public void OnReadyButtonPressed()
        {
            if (PlayerStateManager.Instance != null)
            {
                PlayerStateManager.Instance.SetLocalPlayerReady();
                
                if (logDebug) Debug.Log("[Lobby] Local player marked as ready");
            }
        }

        /// <summary>
        /// Call this when the local player selects their role.
        /// Updates the player's role for the upcoming game.
        /// </summary>
        public void OnRoleSelected(PlayerRole role)
        {
            if (PlayerStateManager.Instance != null)
            {
                PlayerStateManager.Instance.SetLocalPlayerRole(role);
                
                if (logDebug) Debug.Log($"[Lobby] Local player selected role: {role}");
            }
        }

        /// <summary>
        /// Call this when the host clicks the Start Game button.
        /// Initiates role-based scene transitions for all players.
        /// Host only.
        /// </summary>
        public void OnStartGamePressed()
        {
            if (SceneSyncManager.Instance != null)
            {
                SceneSyncManager.Instance.RequestStartGame();
                
                if (logDebug) Debug.Log("[Lobby] Host requested game start");
            }
            else
            {
                Debug.LogError("[Lobby] Cannot start game - SceneSyncManager not available");
            }
        }

        /// <summary>
        /// Checks if the local player is the lobby owner (host).
        /// </summary>
        public bool IsLocalPlayerHost()
        {
            if (SteamManager.Instance == null) return false;
            
            return SteamManager.Instance.currentLobby.Owner.Id == SteamManager.Instance.PlayerSteamId;
        }

        /// <summary>
        /// Gets the count of ready players in the lobby.
        /// </summary>
        public int GetReadyPlayerCount()
        {
            if (SteamManager.Instance == null || PlayerStateManager.Instance == null) return 0;

            int readyCount = 0;
            foreach (var member in SteamManager.Instance.currentLobby.Members)
            {
                var state = PlayerStateManager.Instance.GetPlayerState(member.Id);
                if (state.IsReady) readyCount++;
            }

            return readyCount;
        }

        /// <summary>
        /// Gets the total player count in the lobby.
        /// </summary>
        public int GetTotalPlayerCount()
        {
            return SteamManager.Instance?.currentLobby.MemberCount ?? 0;
        }
    }
}
