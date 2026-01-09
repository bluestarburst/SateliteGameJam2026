using System;
using System.Collections.Generic;
using System.IO;
using Steamworks;
using UnityEngine;
using UnityEngine.InputSystem;
using SatelliteGameJam.Networking.Identity;

namespace SatelliteGameJam.Networking.Voice
{
    /// <summary>
    /// Voice chat over Steam P2P using Facepunch Steamworks.
    /// Records voice once, compresses it, and sends to all connected peers.
    /// Routes incoming voice to per-sender VoiceRemotePlayer components for playback.
    /// Attach to a GameObject in your gameplay scene.
    /// </summary>
    public class VoiceChatP2P : MonoBehaviour
{
    [Header("Voice Settings")]
    // [SerializeField] private KeyCode pushToTalkKey = KeyCode.V;
    [SerializeField] private InputAction pushToTalkAction;
    [SerializeField] private bool alwaysRecord = false; // For testing; use push-to-talk in production
    [SerializeField] private int voiceChannel = 2; // Separate P2P channel for voice data

    private MemoryStream voiceStream;
    private Dictionary<SteamId, VoiceRemotePlayer> remoteVoicePlayers = new();

    public bool isTalking => SteamUser.HasVoiceData;
    public bool isRecording => SteamUser.VoiceRecord;
    public bool isSending = false;
    public bool pressingButton = false;

    private bool isLocalPlayerActive => SteamManager.Instance != null && SteamManager.Instance.currentLobby.MemberCount > 1;

    private void Awake()
    {
        voiceStream = new MemoryStream();
    }

    private void Update()
    {
        // Recording: capture voice and send via P2P
        if (isLocalPlayerActive)
        {
            // remove legacy getKey
            bool shouldRecord = alwaysRecord || pushToTalkAction.IsPressed();
            SteamUser.VoiceRecord = shouldRecord;
            pressingButton = pushToTalkAction.IsPressed();

            if (SteamUser.HasVoiceData)
            {
                voiceStream.Position = 0;
                int compressedRead = SteamUser.ReadVoiceData(voiceStream);
                if (compressedRead > 0)
                {
                    voiceStream.Position = 0;
                    byte[] compressedData = new byte[compressedRead];
                    voiceStream.Read(compressedData, 0, compressedRead);

                    // Send compressed voice via P2P on dedicated voice channel
                    SendVoicePacket(compressedData, compressedRead);
                    isSending = true;
                }
            }
            else
            {
                isSending = false;
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

    private void SendVoicePacket(byte[] compressed, int length)
    {
        if (SteamManager.Instance?.currentLobby == null) return;

        // Prepend local player SteamId to packet
        byte[] packet = new byte[8 + length];
        System.Buffer.BlockCopy(BitConverter.GetBytes(SteamManager.Instance.PlayerSteamId.Value), 0, packet, 0, 8);
        System.Buffer.BlockCopy(compressed, 0, packet, 8, length);

        // Fan out to all peers
        foreach (var member in SteamManager.Instance.currentLobby.Members)
        {
            if (member.Id != SteamManager.Instance.PlayerSteamId)
            {
                bool sent = SteamNetworking.SendP2PPacket(member.Id, packet, packet.Length, voiceChannel, P2PSend.UnreliableNoDelay);
                if (!sent)
                {
                    Debug.LogWarning($"Failed to send voice to {member.Name}");
                }
            }
        }
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
        System.Buffer.BlockCopy(data, 8, compressedData, 0, length - 8);
        remotePlayer.ReceiveVoiceData(compressedData, compressedData.Length);
    }

    private VoiceRemotePlayer CreateRemoteVoicePlayer(SteamId senderId)
    {
        // Find remote player's avatar by SteamId
        GameObject remoteAvatar = FindRemotePlayerAvatar(senderId);
        if (remoteAvatar == null)
        {
            // Fallback: create empty GameObject
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
        // This would typically query your player manager or find by NetworkIdentity
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
        Debug.Log($"Removing remote voice player for {steamId}");
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
        Debug.Log("Cleaning up VoiceChatP2P");
        SteamUser.VoiceRecord = false;
        voiceStream?.Dispose();
    }
}
}
