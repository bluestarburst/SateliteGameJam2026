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

        [Header("Fallback Scene Names (if no config assigned)")]
        [SerializeField] private string lobbySceneName = "Lobby";
        [SerializeField] private string groundControlSceneName = "GroundControl";
        [SerializeField] private string spaceStationSceneName = "SpaceStation";

        [Header("Behavior")]
        [SerializeField] private float sceneChangeTimeoutSeconds = 10f;
        [SerializeField] private bool logDebug = true;

        private HashSet<SteamId> pendingAcks = new HashSet<SteamId>();

        // Properties for accessing configuration
        private string LobbySceneName => config != null ? config.lobbySceneName : lobbySceneName;
        private string GroundControlSceneName => config != null ? config.groundControlSceneName : groundControlSceneName;
        private string SpaceStationSceneName => config != null ? config.spaceStationSceneName : spaceStationSceneName;
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

            RegisterHandlers();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void RegisterHandlers()
        {
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
            }
        }

        private void OnDestroy()
        {
            if (NetworkConnectionManager.Instance != null)
            {
                NetworkConnectionManager.Instance.UnregisterHandler(NetworkMessageType.SceneChangeRequest, OnReceiveSceneChangeRequest);
                NetworkConnectionManager.Instance.UnregisterHandler(NetworkMessageType.SceneChangeAcknowledge, OnReceiveSceneChangeAcknowledge);
            }
            if (PlayerStateManager.Instance != null)
            {
                PlayerStateManager.Instance.OnPlayerSceneChanged -= OnPlayerSceneChanged;
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
            if (SteamManager.Instance == null) return false;
            var lobby = SteamManager.Instance.currentLobby;
            if (lobby.Id.Value == 0) return false;
            return lobby.IsOwnedBy(SteamManager.Instance.PlayerSteamId);
        }

        private void BroadcastRoleBasedScenes()
        {
            if (SteamManager.Instance == null || PlayerStateManager.Instance == null) return;

            var members = SteamManager.Instance.currentLobby.Members.ToList();
            foreach (var member in members)
            {
                var state = PlayerStateManager.Instance.GetPlayerState(member.Id);
                NetworkSceneId target = state.Role == PlayerRole.SpaceStation
                    ? NetworkSceneId.SpaceStation
                    : NetworkSceneId.GroundControl;

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
            SceneManager.LoadScene(sceneName);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Quick Fix #1: Clean up remote player models when transitioning scenes
            if (NetworkConnectionManager.Instance != null)
            {
                NetworkConnectionManager.Instance.CleanupAllRemotePlayers();
            }
            
            SendSceneAck();
        }

        private string ResolveSceneName(NetworkSceneId sceneId)
        {
            switch (sceneId)
            {
                case NetworkSceneId.Lobby: return LobbySceneName;
                case NetworkSceneId.GroundControl: return GroundControlSceneName;
                case NetworkSceneId.SpaceStation: return SpaceStationSceneName;
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
            if (logDebug) Debug.Log($"[SceneSync] Received SceneChangeRequest from {sender}");
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
