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

        [Header("Scene Names")]
        [SerializeField] private string lobbySceneName = "Lobby";
        [SerializeField] private string groundControlSceneName = "GroundControl";
        [SerializeField] private string spaceStationSceneName = "SpaceStation";

        [Header("Behavior")]
        [SerializeField] private float sceneChangeTimeoutSeconds = 10f;
        [SerializeField] private bool logDebug = true;

        private HashSet<SteamId> pendingAcks = new HashSet<SteamId>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (NetworkConnectionManager.Instance != null)
            {
                NetworkConnectionManager.Instance.RegisterHandler(NetworkMessageType.SceneChangeRequest, OnReceiveSceneChangeRequest);
                NetworkConnectionManager.Instance.RegisterHandler(NetworkMessageType.SceneChangeAcknowledge, OnReceiveSceneChangeAcknowledge);
            }

            if (PlayerStateManager.Instance != null)
            {
                PlayerStateManager.Instance.OnPlayerSceneChanged += OnPlayerSceneChanged;
            }

            SceneManager.sceneLoaded += OnSceneLoaded;
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
            return SteamManager.Instance != null && SteamManager.Instance.currentLobby.IsOwnedBy(SteamManager.Instance.PlayerSteamId);
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
            foreach (var m in SteamManager.Instance.currentLobby.Members)
            {
                if (m.Id != SteamManager.Instance.PlayerSteamId)
                    pendingAcks.Add(m.Id);
            }
            Invoke(nameof(CheckAckTimeout), sceneChangeTimeoutSeconds);
        }

        private void CheckAckTimeout()
        {
            if (pendingAcks.Count > 0 && logDebug)
            {
                Debug.Log($"[SceneSync] Ack timeout. Missing: {string.Join(", ", pendingAcks.Select(id => id.Value))}");
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
            SendSceneAck();
        }

        private string ResolveSceneName(NetworkSceneId sceneId)
        {
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
            byte[] packet = new byte[15];
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
            byte[] packet = new byte[11];
            packet[0] = (byte)NetworkMessageType.SceneChangeAcknowledge;
            int offset = 1;
            NetworkSerialization.WriteULong(packet, ref offset, SteamManager.Instance.PlayerSteamId);
            var currentState = PlayerStateManager.Instance?.GetPlayerState(SteamManager.Instance.PlayerSteamId);
            var sceneId = currentState?.Scene ?? NetworkSceneId.None;
            packet[offset++] = (byte)(((ushort)sceneId) >> 8);
            packet[offset++] = (byte)(((ushort)sceneId) & 0xFF);
            NetworkConnectionManager.Instance.SendToAll(packet, 0, P2PSend.Reliable);
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

            if (pendingAcks.Remove(who) && logDebug)
            {
                Debug.Log($"[SceneSync] Ack from {who} for scene {sceneId}. Remaining: {pendingAcks.Count}");
            }
        }
    }
}
