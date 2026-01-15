using System;
using System.Collections.Generic;
using System.IO;
using Steamworks;
using UnityEngine;
using UnityEngine.InputSystem;
using SatelliteGameJam.Networking.Identity;
using SatelliteGameJam.Networking.State;
using SatelliteGameJam.Networking.Messages;

namespace SatelliteGameJam.Networking.Voice
{
    /// <summary>
    /// Voice chat over Steam P2P using Facepunch Steamworks.
    ///
    /// Voice Model:
    /// - Lobby: Auto-record, send to all lobby members
    /// - Ground Control: Auto-record, send to Ground players always, send to Space only when at console
    /// - Space Station: Auto-record, send to nearby Space players, send to Ground only when 'B' pressed
    ///
    /// Routes incoming voice to per-sender VoiceRemotePlayer components for playback.
    /// </summary>
    public class VoiceChatP2P : MonoBehaviour
    {
        [Header("Voice Settings")]
        [SerializeField] private Key crossRolePTTKey = Key.B; // Space -> Ground PTT
        [SerializeField] private bool alwaysRecord = false; // For testing override
        [SerializeField] private int voiceChannel = 2; // Separate P2P channel for voice data

        [Header("Debug")]
        [SerializeField] private bool debugLogging = false;

        private MemoryStream voiceStream;
        private Dictionary<SteamId, VoiceRemotePlayer> remoteVoicePlayers = new();

        public bool isTalking => SteamUser.HasVoiceData;
        public bool isRecording => SteamUser.VoiceRecord;
        public bool isSending = false;
        public bool isCrossRolePTTPressed => Keyboard.current != null && Keyboard.current[crossRolePTTKey].isPressed;

        private bool isLocalPlayerActive => SteamManager.Instance != null && SteamManager.Instance.currentLobby.MemberCount > 1;

        private void Awake()
        {
            voiceStream = new MemoryStream();
        }

        private float shouldResetSendingState = 0f;

        private void Update()
        {
            // Reset sending indicator after timeout
            if (Time.time >= shouldResetSendingState)
            {
                isSending = false;
                shouldResetSendingState = float.MaxValue;
            }

            if (!isLocalPlayerActive) return;

            // Determine if we should be recording based on role
            bool shouldRecord = ShouldRecordVoice();
            SteamUser.VoiceRecord = shouldRecord;

            // If we have voice data, send it with role-aware routing
            if (SteamUser.HasVoiceData)
            {
                voiceStream.Position = 0;
                int compressedRead = SteamUser.ReadVoiceData(voiceStream);
                if (compressedRead > 0)
                {
                    voiceStream.Position = 0;
                    byte[] compressedData = new byte[compressedRead];
                    voiceStream.Read(compressedData, 0, compressedRead);

                    // Send with role-aware routing
                    SendVoicePacketWithRoleRouting(compressedData, compressedRead);
                }
            }

            // Receiving: poll for voice packets on voice channel
            while (SteamNetworking.IsP2PPacketAvailable(voiceChannel))
            {
                var packet = SteamNetworking.ReadP2PPacket(voiceChannel);
                if (packet.HasValue)
                {
                    HandleIncomingVoicePacket(packet.Value.Data, packet.Value.Data.Length);
                }
            }
        }

        /// <summary>
        /// Determines if voice should be recorded based on current scene/role.
        /// </summary>
        private bool ShouldRecordVoice()
        {
            if (alwaysRecord) return true;

            var localState = GetLocalPlayerState();
            if (localState == null) return false;

            // Lobby: Auto-voice (always recording, no PTT needed)
            if (localState.Scene == NetworkSceneId.Lobby)
            {
                return true;
            }

            // Ground Control & Space Station: Always recording (routing controls who receives)
            if (localState.Role == PlayerRole.GroundControl || localState.Role == PlayerRole.SpaceStation)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Sends voice packet with role-aware recipient filtering.
        /// </summary>
        private void SendVoicePacketWithRoleRouting(byte[] compressed, int length)
        {
            if (SteamManager.Instance?.currentLobby == null) return;

            var localState = GetLocalPlayerState();
            if (localState == null) return;

            // Prepend local player SteamId to packet
            byte[] packet = new byte[8 + length];
            Buffer.BlockCopy(BitConverter.GetBytes(SteamManager.Instance.PlayerSteamId.Value), 0, packet, 0, 8);
            Buffer.BlockCopy(compressed, 0, packet, 8, length);

            bool sentToAnyone = false;

            // Check each lobby member and send based on role rules
            foreach (var member in SteamManager.Instance.currentLobby.Members)
            {
                if (member.Id == SteamManager.Instance.PlayerSteamId) continue;

                if (ShouldSendVoiceTo(localState, member.Id))
                {
                    bool sent = SteamNetworking.SendP2PPacket(member.Id, packet, packet.Length, voiceChannel, P2PSend.UnreliableNoDelay);
                    if (!sent && debugLogging)
                    {
                        Debug.LogWarning($"[VoiceChatP2P] Failed to send voice to {member.Name}");
                    }
                    else
                    {
                        sentToAnyone = true;
                    }
                }
            }

            if (sentToAnyone)
            {
                isSending = true;
                shouldResetSendingState = Time.time + 0.5f;
            }
        }

        /// <summary>
        /// Determines if voice should be sent to a specific target based on role rules.
        /// </summary>
        private bool ShouldSendVoiceTo(PlayerState localState, SteamId targetId)
        {
            var targetState = PlayerStateManager.Instance?.GetPlayerState(targetId);
            if (targetState == null) return false;

            // Lobby: Send to everyone (PTT already checked in ShouldRecordVoice)
            Debug.Log($"[VoiceChatP2P] Checking voice send from {localState.Role} to {targetState.Role} in scene {localState.Scene}");
            if (localState.Scene == NetworkSceneId.Lobby)
            {
                return true;
            }

            // Ground Control sending rules
            if (localState.Role == PlayerRole.GroundControl)
            {
                // Always send to other Ground Control players (auto-voice)
                if (targetState.Role == PlayerRole.GroundControl)
                {
                    if (debugLogging) Debug.Log($"[VoiceChatP2P] Ground->Ground: sending to {targetId}");
                    return true;
                }

                // Send to Space players ONLY when at console
                if (targetState.Role == PlayerRole.SpaceStation)
                {
                    bool atConsole = VoiceSessionManager.Instance?.IsLocalPlayerAtConsole ?? false;
                    if (debugLogging && atConsole) Debug.Log($"[VoiceChatP2P] Ground->Space (at console): sending to {targetId}");
                    return atConsole;
                }

                return false;
            }

            // Space Station sending rules
            if (localState.Role == PlayerRole.SpaceStation)
            {
                // Send to Ground Control ONLY when pressing 'B' (cross-role PTT)
                if (targetState.Role == PlayerRole.GroundControl)
                {
                    bool pttPressed = Keyboard.current != null && Keyboard.current[crossRolePTTKey].isPressed;
                    if (debugLogging && pttPressed) Debug.Log($"[VoiceChatP2P] Space->Ground (PTT): sending to {targetId}");
                    return pttPressed;
                }

                // Send to nearby Space players (proximity-based auto-voice)
                if (targetState.Role == PlayerRole.SpaceStation)
                {
                    bool inProximity = VoiceSessionManager.Instance?.IsWithinProximityForSending(targetId) ?? false;
                    if (debugLogging && inProximity) Debug.Log($"[VoiceChatP2P] Space->Space (proximity): sending to {targetId}");
                    return inProximity;
                }

                return false;
            }

            return false;
        }

        /// <summary>
        /// Gets the local player's state from PlayerStateManager.
        /// </summary>
        private PlayerState GetLocalPlayerState()
        {
            if (SteamManager.Instance == null || PlayerStateManager.Instance == null)
                return null;

            return PlayerStateManager.Instance.GetPlayerState(SteamManager.Instance.PlayerSteamId);
        }

        private void HandleIncomingVoicePacket(byte[] data, int length)
        {
            if (length < 8) return; // Need at least SteamId

            // Extract sender SteamId
            ulong senderValue = BitConverter.ToUInt64(data, 0);
            SteamId senderId = new SteamId { Value = senderValue };

            // Get or create VoiceRemotePlayer for this sender
            if (!remoteVoicePlayers.TryGetValue(senderId, out var remotePlayer))
            {
                remotePlayer = CreateRemoteVoicePlayer(senderId);
                if (remotePlayer != null)
                {
                    remoteVoicePlayers[senderId] = remotePlayer;
                }
                else
                {
                    return; // Could not create player
                }
            }

            // Forward compressed data (skip SteamId header)
            byte[] compressedData = new byte[length - 8];
            Buffer.BlockCopy(data, 8, compressedData, 0, length - 8);
            remotePlayer.ReceiveVoiceData(compressedData, compressedData.Length);
        }

        private VoiceRemotePlayer CreateRemoteVoicePlayer(SteamId senderId)
        {
            // Try to get from VoiceSessionManager first (preferred)
            if (VoiceSessionManager.Instance != null)
            {
                return VoiceSessionManager.Instance.GetOrCreateVoiceRemotePlayer(senderId);
            }

            // Fallback: Find remote player's avatar by SteamId
            GameObject remoteAvatar = FindRemotePlayerAvatar(senderId);
            if (remoteAvatar == null)
            {
                // Fallback: create empty GameObject with AudioSource
                remoteAvatar = new GameObject($"RemoteVoice_{senderId}");
                remoteAvatar.AddComponent<AudioSource>();
            }

            var remotePlayer = remoteAvatar.GetComponent<VoiceRemotePlayer>();
            if (remotePlayer == null)
            {
                remotePlayer = remoteAvatar.AddComponent<VoiceRemotePlayer>();
            }

            remotePlayer.Initialize(senderId);
            return remotePlayer;
        }

        private GameObject FindRemotePlayerAvatar(SteamId steamId)
        {
            // Try to find an existing remote player GameObject
            var allIdentities = FindObjectsByType<NetworkIdentity>(FindObjectsSortMode.None);
            foreach (var identity in allIdentities)
            {
                if (identity.OwnerSteamId == steamId && identity.CompareTag("Player"))
                {
                    return identity.gameObject;
                }
            }
            return null;
        }

        public void RemoveRemotePlayer(SteamId steamId)
        {
            if (debugLogging) Debug.Log($"[VoiceChatP2P] Removing remote voice player for {steamId}");
            if (remoteVoicePlayers.TryGetValue(steamId, out var remotePlayer))
            {
                if (remotePlayer != null)
                {
                    Destroy(remotePlayer.gameObject);
                }
                remoteVoicePlayers.Remove(steamId);
            }
        }

        private void OnDestroy()
        {
            if (debugLogging) Debug.Log("[VoiceChatP2P] Cleaning up");
            SteamUser.VoiceRecord = false;
            voiceStream?.Dispose();
        }
    }
}
