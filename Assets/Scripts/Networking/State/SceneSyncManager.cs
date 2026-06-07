using System;
using System.Collections.Generic;
using System.Linq;
using Steamworks;
using UnityEngine;
using UnityEngine.SceneManagement;
using SatelliteGameJam.Networking.Messages;
using SatelliteGameJam.Networking.Core;

namespace SatelliteGameJam.Networking.State
{
    /// <summary>
    /// Coordinates collective scene transitions. Owner broadcasts target scenes per player
    /// using PlayerSceneState messages. Each client loads its scene upon receiving its own assignment
    /// and sends an acknowledgement.
    /// </summary>
    public class SceneSyncManager : MonoBehaviour
    {
        public static SceneSyncManager Instance { get; private set; }

        [Header("Configuration")]
        [SerializeField] private NetworkingConfiguration config;
        [SerializeField] private SceneFlowController sceneFlowController;

        [Header("Fallback Scene Names (if no config assigned)")]
        [SerializeField] private string lobbySceneName = "Lobby";
        [SerializeField] private string groundControlSceneName = "GroundControl";
        [SerializeField] private string spaceStationSceneName = "SpaceStation";

        [Header("Behavior")]
        [SerializeField] private float sceneChangeTimeoutSeconds = 10f;
        [SerializeField] private bool logDebug = true;

        private HashSet<SteamId> pendingAcks = new HashSet<SteamId>();
        private bool handlersRegistered;

        // Properties for accessing configuration
        private string LobbySceneName => GetFallbackSceneName(NetworkSceneId.Lobby);
        private string GroundControlSceneName => GetFallbackSceneName(NetworkSceneId.GroundControl);
        private string SpaceStationSceneName => GetFallbackSceneName(NetworkSceneId.SpaceStation);
        private float SceneChangeTimeout => config != null ? config.sceneChangeTimeoutSeconds : sceneChangeTimeoutSeconds;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            if (sceneFlowController == null)
            {
                sceneFlowController = SceneFlowController.Instance;
            }

            RegisterHandlers();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void RegisterHandlers()
        {
            if (handlersRegistered)
            {
                return;
            }

            if (NetworkConnectionManager.Instance == null)
            {
                Debug.LogWarning("[SceneSync] NetworkConnectionManager not found. Retrying...");
                Invoke(nameof(RegisterHandlers), 0.5f);
                return;
            }

            NetworkConnectionManager.Instance.RegisterHandler(NetworkMessageType.SceneChangeRequest, OnReceiveSceneChangeRequest);
            NetworkConnectionManager.Instance.RegisterHandler(NetworkMessageType.SceneChangeAcknowledge, OnReceiveSceneChangeAcknowledge);

            if (PlayerStateManager.Instance != null)
            {
                PlayerStateManager.Instance.OnPlayerSceneChanged += OnPlayerSceneChanged;
                PlayerStateManager.Instance.OnPlayerSceneChanged += OnRemotePlayerSceneChanged;
            }

            handlersRegistered = true;
        }

        private void OnDestroy()
        {
            CancelInvoke(nameof(RegisterHandlers));
            CancelInvoke(nameof(CheckAckTimeout));
            CancelInvoke(nameof(SpawnPlayersForCurrentScene));

            if (NetworkConnectionManager.Instance != null)
            {
                NetworkConnectionManager.Instance.UnregisterHandler(NetworkMessageType.SceneChangeRequest, OnReceiveSceneChangeRequest);
                NetworkConnectionManager.Instance.UnregisterHandler(NetworkMessageType.SceneChangeAcknowledge, OnReceiveSceneChangeAcknowledge);
            }
            if (PlayerStateManager.Instance != null)
            {
                PlayerStateManager.Instance.OnPlayerSceneChanged -= OnPlayerSceneChanged;
                PlayerStateManager.Instance.OnPlayerSceneChanged -= OnRemotePlayerSceneChanged;
            }
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        /// <summary>
        /// Owner initiates game start: assigns scenes by role and broadcasts to all.
        /// </summary>
        public void RequestStartGame()
        {
            if (!IsOwner())
            {
                if (logDebug) Debug.Log("[SceneSync] Not lobby owner; cannot start game.");
                return;
            }

            if (sceneFlowController != null && !sceneFlowController.CanHostStartGame(out string reason))
            {
                Debug.LogWarning($"[SceneSync] Cannot start game: {reason}");
                return;
            }

            BroadcastRoleBasedScenes();
            BeginAckWindow();
        }

        /// <summary>
        /// Owner initiates end game: sends all players back to lobby.
        /// </summary>
        public void RequestEndGame()
        {
            if (!IsOwner())
            {
                if (logDebug) Debug.Log("[SceneSync] Not lobby owner; cannot end game.");
                return;
            }
            BroadcastSceneForAll(NetworkSceneId.Lobby);
            BeginAckWindow();
        }

        private bool IsOwner()
        {
            return SteamManager.Instance != null && SteamManager.Instance.IsLocalPlayerLobbyHost;
        }

        private void BroadcastRoleBasedScenes()
        {
            if (SteamManager.Instance == null || PlayerStateManager.Instance == null) return;

            var members = SteamManager.Instance.currentLobby.Members.ToList();
            foreach (var member in members)
            {
                var state = PlayerStateManager.Instance.GetPlayerState(member.Id);
                NetworkSceneId target = ResolveGameplaySceneForRole(state.Role);

                if (member.Id == SteamManager.Instance.PlayerSteamId)
                {
                    // Set local immediately so we also load
                    PlayerStateManager.Instance.SetLocalPlayerScene(target);
                }
                else
                {
                    SendPlayerSceneAssignment(member.Id, target);
                }
            }
        }

        private void BroadcastSceneForAll(NetworkSceneId target)
        {
            if (SteamManager.Instance == null) return;
            var members = SteamManager.Instance.currentLobby.Members.ToList();
            foreach (var member in members)
            {
                if (member.Id == SteamManager.Instance.PlayerSteamId)
                {
                    PlayerStateManager.Instance.SetLocalPlayerScene(target);
                }
                else
                {
                    SendPlayerSceneAssignment(member.Id, target);
                }
            }
        }

        private void BeginAckWindow()
        {
            pendingAcks.Clear();
            if (SteamManager.Instance == null) return;
            
            // Cancel any pending timeout checks
            CancelInvoke(nameof(CheckAckTimeout));
            
            foreach (var m in SteamManager.Instance.currentLobby.Members)
            {
                if (m.Id != SteamManager.Instance.PlayerSteamId)
                    pendingAcks.Add(m.Id);
            }
            
            if (pendingAcks.Count > 0)
            {
                Invoke(nameof(CheckAckTimeout), SceneChangeTimeout);
            }
        }

        private void CheckAckTimeout()
        {
            if (pendingAcks.Count > 0)
            {
                if (logDebug)
                {
                    Debug.LogWarning($"[SceneSync] Ack timeout. Missing: {string.Join(", ", pendingAcks.Select(id => id.Value))}");
                }
                // Clear pending acks to prevent stale state
                pendingAcks.Clear();
            }
        }

        private void OnPlayerSceneChanged(SteamId steamId, NetworkSceneId sceneId)
        {
            if (steamId != SteamManager.Instance?.PlayerSteamId) return;

            string sceneName = ResolveSceneName(sceneId);
            if (string.IsNullOrEmpty(sceneName))
            {
                if (logDebug) Debug.LogWarning($"[SceneSync] No scene name mapped for {sceneId}");
                return;
            }

            if (logDebug) Debug.Log($"[SceneSync] Loading scene '{sceneName}' for local player");
            if (sceneFlowController != null)
            {
                sceneFlowController.LoadSceneForLocal(sceneId);
            }
            else
            {
                SceneManager.LoadScene(sceneName);
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // CRITICAL FIX: Only clean up remote player prefabs when NOT in Lobby/Matchmaking
            // Lobby uses voice proxies managed by LobbyNetworkingManager
            // Cleaning up on entry to Lobby would destroy those voice proxies immediately after creation
            bool isLobbyOrMatchmaking = sceneFlowController != null
                ? sceneFlowController.IsLobbyOrMatchmakingScene(scene.name)
                : scene.name == LobbySceneName || scene.name == "Matchmaking";
            bool isLobbyScene = scene.name == LobbySceneName;

            // Ensure lobby voice/chat routing has a deterministic baseline state, even when
            // scene-local lobby manager wiring is missing or delayed.
            if (isLobbyScene && PlayerStateManager.Instance != null && SteamManager.Instance != null)
            {
                SteamId localId = SteamManager.Instance.PlayerSteamId;
                if (localId.Value != 0)
                {
                    PlayerState localState = PlayerStateManager.Instance.GetPlayerState(localId);

                    if (localState.Scene != NetworkSceneId.Lobby)
                    {
                        PlayerStateManager.Instance.SetLocalPlayerScene(NetworkSceneId.Lobby);
                    }

                    if (localState.Role == PlayerRole.None)
                    {
                        PlayerStateManager.Instance.SetLocalPlayerRole(PlayerRole.Lobby);
                    }
                }
            }

            if (NetworkConnectionManager.Instance != null && !isLobbyOrMatchmaking)
            {
                // Clean up when entering gameplay scenes (Ground Control/Space Station)
                // This ensures old prefabs from previous scene are removed
                NetworkConnectionManager.Instance.CleanupAllRemotePlayers();

                // Spawn remote players for this game scene after a short delay
                // This gives time for network messages to arrive with player scene states
                Invoke(nameof(SpawnPlayersForCurrentScene), 0.2f);
            }

            SendSceneAck();
        }

        /// <summary>
        /// Spawns remote players who are in the same scene as the local player.
        /// Called automatically after loading a game scene (not Lobby/Matchmaking).
        /// </summary>
        private void SpawnPlayersForCurrentScene()
        {
            if (SteamManager.Instance == null || PlayerStateManager.Instance == null)
            {
                if (logDebug) Debug.LogWarning("[SceneSync] Cannot spawn players - managers not ready");
                return;
            }

            var localState = PlayerStateManager.Instance.GetPlayerState(SteamManager.Instance.PlayerSteamId);
            NetworkSceneId localScene = localState.Scene;

            if (localScene == NetworkSceneId.None || localScene == NetworkSceneId.Lobby)
            {
                if (logDebug) Debug.Log("[SceneSync] Not in a game scene, skipping player spawn");
                return;
            }

            int spawnedCount = 0;
            foreach (var member in SteamManager.Instance.currentLobby.Members)
            {
                // Skip local player
                if (member.Id == SteamManager.Instance.PlayerSteamId) continue;

                var playerState = PlayerStateManager.Instance.GetPlayerState(member.Id);

                // Spawn players who are in the same scene OR have matching role but scene not yet set
                bool sameScene = playerState.Scene == localScene;
                bool matchingRole = playerState.Scene == NetworkSceneId.None &&
                    ((localScene == NetworkSceneId.GroundControl && playerState.Role == PlayerRole.GroundControl) ||
                     (localScene == NetworkSceneId.SpaceStation && playerState.Role == PlayerRole.SpaceStation));

                if (sameScene || matchingRole)
                {
                    NetworkConnectionManager.Instance?.SpawnRemotePlayerFor(member.Id, member.Name);
                    spawnedCount++;

                    if (logDebug)
                    {
                        Debug.Log($"[SceneSync] Spawned remote player {member.Name} for scene {localScene}");
                    }
                }
            }

            if (logDebug)
            {
                Debug.Log($"[SceneSync] Spawned {spawnedCount} remote players for scene {localScene}");
            }
        }

        /// <summary>
        /// Called when any player's scene changes. Spawns remote players who enter the same scene as local player.
        /// </summary>
        private void OnRemotePlayerSceneChanged(SteamId steamId, NetworkSceneId sceneId)
        {
            // Skip local player - we only care about remote players joining our scene
            if (SteamManager.Instance != null && steamId == SteamManager.Instance.PlayerSteamId)
                return;

            // Get local player's scene
            var localState = PlayerStateManager.Instance?.GetPlayerState(SteamManager.Instance.PlayerSteamId);
            if (localState == null) return;

            NetworkSceneId localScene = localState.Scene;

            // Only spawn if they're joining the same scene as us AND we're in a game scene
            if (sceneId == localScene && localScene != NetworkSceneId.None && localScene != NetworkSceneId.Lobby)
            {
                // Get display name from lobby
                string displayName = "Unknown";
                foreach (var member in SteamManager.Instance.currentLobby.Members)
                {
                    if (member.Id == steamId)
                    {
                        displayName = member.Name;
                        break;
                    }
                }

                NetworkConnectionManager.Instance?.SpawnRemotePlayerFor(steamId, displayName);

                if (logDebug)
                {
                    Debug.Log($"[SceneSync] Late spawn: {displayName} joined scene {sceneId}");
                }
            }
        }

        private string ResolveSceneName(NetworkSceneId sceneId)
        {
            if (sceneFlowController != null)
            {
                string mapped = sceneFlowController.ResolveSceneName(sceneId);
                if (!string.IsNullOrWhiteSpace(mapped))
                {
                    return mapped;
                }
            }

            switch (sceneId)
            {
                case NetworkSceneId.Lobby: return LobbySceneName;
                case NetworkSceneId.GroundControl: return GroundControlSceneName;
                case NetworkSceneId.SpaceStation: return SpaceStationSceneName;
                default: return string.Empty;
            }
        }

        private NetworkSceneId ResolveGameplaySceneForRole(PlayerRole role)
        {
            if (sceneFlowController != null)
            {
                return sceneFlowController.ResolveGameplaySceneForRole(role);
            }

            return role == PlayerRole.SpaceStation
                ? NetworkSceneId.SpaceStation
                : NetworkSceneId.GroundControl;
        }

        private string GetFallbackSceneName(NetworkSceneId sceneId)
        {
            if (config != null)
            {
                switch (sceneId)
                {
                    case NetworkSceneId.Lobby:
                        return config.lobbySceneName;
                    case NetworkSceneId.GroundControl:
                        return config.groundControlSceneName;
                    case NetworkSceneId.SpaceStation:
                        return config.spaceStationSceneName;
                }
            }

            switch (sceneId)
            {
                case NetworkSceneId.Lobby: return lobbySceneName;
                case NetworkSceneId.GroundControl: return groundControlSceneName;
                case NetworkSceneId.SpaceStation: return spaceStationSceneName;
                default: return string.Empty;
            }
        }

        private void SendPlayerSceneAssignment(SteamId targetPlayer, NetworkSceneId targetScene)
        {
              byte[] packet = new byte[16]; // Type(1) + SteamId(8) + SceneId(2) + Role(1) + Timestamp(4)
            packet[0] = (byte)NetworkMessageType.PlayerSceneState;
            int offset = 1;
            NetworkSerialization.WriteULong(packet, ref offset, targetPlayer);
            packet[offset++] = (byte)(((ushort)targetScene) >> 8);
            packet[offset++] = (byte)(((ushort)targetScene) & 0xFF);
            var role = PlayerStateManager.Instance?.GetPlayerState(targetPlayer).Role ?? PlayerRole.None;
            packet[offset++] = (byte)role;
            NetworkSerialization.WriteFloat(packet, ref offset, Time.time);

            NetworkConnectionManager.Instance.SendToAll(packet, 4, P2PSend.Reliable);
        }

        private void SendSceneAck()
        {
            if (SteamManager.Instance == null || NetworkConnectionManager.Instance == null)
            {
                if (logDebug) Debug.LogWarning("[SceneSync] Cannot send ack - manager not ready");
                return;
            }

            if (SteamManager.Instance.currentLobby.Id.Value == 0)
            {
                if (logDebug) Debug.Log("[SceneSync] Deferring ack - no active lobby yet");
                CancelInvoke(nameof(SendSceneAck));
                Invoke(nameof(SendSceneAck), 0.5f);
                return;
            }

            byte[] packet = new byte[11];
            packet[0] = (byte)NetworkMessageType.SceneChangeAcknowledge;
            int offset = 1;
            NetworkSerialization.WriteULong(packet, ref offset, SteamManager.Instance.PlayerSteamId);
            var currentState = PlayerStateManager.Instance?.GetPlayerState(SteamManager.Instance.PlayerSteamId);
            var sceneId = currentState?.Scene ?? NetworkSceneId.None;
            packet[offset++] = (byte)(((ushort)sceneId) >> 8);
            packet[offset++] = (byte)(((ushort)sceneId) & 0xFF);
            NetworkConnectionManager.Instance.SendToAll(packet, 0, P2PSend.Reliable);
            
            if (logDebug) Debug.Log($"[SceneSync] Sent ack for scene {sceneId}");
        }

        public void AcknowledgeCurrentScene()
        {
            SendSceneAck();
        }

        private void OnReceiveSceneChangeRequest(SteamId sender, byte[] data)
        {
            if (data == null || data.Length < 15 || PlayerStateManager.Instance == null || SteamManager.Instance == null)
            {
                return;
            }

            int offset = 1;
            SteamId targetPlayer = NetworkSerialization.ReadULong(data, ref offset);
            ushort sceneValue = (ushort)((data[offset++] << 8) | data[offset++]);
            NetworkSceneId targetScene = (NetworkSceneId)sceneValue;

            if (targetPlayer != SteamManager.Instance.PlayerSteamId)
            {
                return;
            }

            if (logDebug)
            {
                Debug.Log($"[SceneSync] Received direct scene change request from {sender} for {targetScene}");
            }

            PlayerStateManager.Instance.SetLocalPlayerScene(targetScene);
        }

        private void OnReceiveSceneChangeAcknowledge(SteamId sender, byte[] data)
        {
            if (data.Length < 11) return;
            int offset = 1;
            SteamId who = NetworkSerialization.ReadULong(data, ref offset);
            ushort sceneIdValue = (ushort)((data[offset++] << 8) | data[offset++]);
            NetworkSceneId sceneId = (NetworkSceneId)sceneIdValue;

            if (pendingAcks.Remove(who))
            {
                if (logDebug)
                {
                    Debug.Log($"[SceneSync] Ack from {who} for scene {sceneId}. Remaining: {pendingAcks.Count}");
                }
                
                // All acks received - cancel timeout
                if (pendingAcks.Count == 0)
                {
                    CancelInvoke(nameof(CheckAckTimeout));
                    if (logDebug) Debug.Log("[SceneSync] All players acknowledged scene change");
                }
            }
        }
    }
}
