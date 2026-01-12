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
    /// Creates voice proxies (NOT full player prefabs) for remote players.
    /// All players can hear each other in the lobby via auto-voice.
    /// Refs: DeveloperExperienceImprovements.md Part 2, networking-states.md
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
        /// Creates voice proxies for all existing lobby members (except local player).
        /// No player prefabs are spawned in lobby - only voice proxies for audio playback.
        /// </summary>
        private void SpawnExistingPlayers()
        {
            if (SteamManager.Instance == null)
            {
                if (logDebug) Debug.LogWarning("[Lobby] Cannot create voice proxies - SteamManager not ready");
                return;
            }

            int createdCount = 0;
            foreach (var member in SteamManager.Instance.currentLobby.Members)
            {
                if (member.Id != SteamManager.Instance.PlayerSteamId)
                {
                    CreateVoiceProxyForPlayer(member.Id, member.Name);
                    createdCount++;
                }
            }

            if (logDebug || (config != null && config.verboseLogging))
            {
                Debug.Log($"[Lobby] Created {createdCount} voice proxies for existing lobby players");
            }
        }

        /// <summary>
        /// Creates a voice proxy for the given SteamId.
        /// No player prefab is spawned - only a lightweight voice proxy for audio playback.
        /// </summary>
        private void CreateVoiceProxyForPlayer(SteamId steamId, string displayName)
        {
            if (spawnedPlayers.Contains(steamId))
            {
                if (logDebug) Debug.LogWarning($"[Lobby] Voice proxy for {displayName} already exists");
                return;
            }

            // Create voice proxy via VoiceSessionManager (lightweight AudioSource only)
            if (VoiceSessionManager.Instance != null)
            {
                VoiceSessionManager.Instance.GetOrCreateVoiceRemotePlayer(steamId);
            }

            spawnedPlayers.Add(steamId);

            if (logDebug || (config != null && config.verboseLogging))
            {
                Debug.Log($"[Lobby] Created voice proxy for: {displayName}");
            }
        }

        /// <summary>
        /// Called when a new player joins the lobby.
        /// </summary>
        private void OnRemotePlayerJoined(SteamId steamId)
        {
            if (logDebug) Debug.Log($"[Lobby] Player joined: {steamId}");

            // Find player in lobby members and create voice proxy
            if (SteamManager.Instance != null && SteamManager.Instance.currentLobby.MemberCount > 0)
            {
                foreach (var member in SteamManager.Instance.currentLobby.Members)
                {
                    if (member.Id == steamId)
                    {
                        CreateVoiceProxyForPlayer(steamId, member.Name);
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

                // Clean up voice proxy
                VoiceSessionManager.Instance?.UnregisterRemotePlayer(steamId);
            }
        }

        /// <summary>
        /// Cleans up all voice proxies.
        /// </summary>
        private void CleanupSpawnedPlayers()
        {
            if (logDebug) Debug.Log($"[Lobby] Cleaning up {spawnedPlayers.Count} voice proxies");

            foreach (var steamId in spawnedPlayers)
            {
                VoiceSessionManager.Instance?.UnregisterRemotePlayer(steamId);
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
