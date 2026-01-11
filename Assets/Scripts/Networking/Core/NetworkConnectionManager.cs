using System;
using System.Collections.Generic;
using System.Linq;
using Steamworks;
using Steamworks.Data;
using UnityEngine;
using SatelliteGameJam.Networking.Messages;
using SatelliteGameJam.Networking.Identity;

namespace SatelliteGameJam.Networking.Core
{
    /// <summary>
    /// Central packet router for all network messages.
    /// Routes packets by channel and message type to registered handlers.
    /// Does NOT track peers - queries SteamManager.Instance.currentLobby.Members for peer list.
    /// </summary>
    public class NetworkConnectionManager : MonoBehaviour
{
    public static NetworkConnectionManager Instance { get; private set; }

    // Handler registration - maps message types to handlers
    private Dictionary<NetworkMessageType, Action<SteamId, byte[]>> messageHandlers;

    // include info about channels to poll for incoming packets
    [Header("Channels to Poll")]
    [SerializeField] private int[] channelsToPoll = new[] { 0, 1, 3, 4 }; // Exclude voice channel 2

    // Player asset to create for new connections (optional)
    [Header("Player Prefab")]
    [SerializeField] private bool autoSpawnPlayer = true;
    [SerializeField] private GameObject playerPrefab;

    // Track spawned remote player instances to prevent duplicates and allow cleanup
    private readonly Dictionary<SteamId, GameObject> spawnedRemotePlayers = new();


    private void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Multiple instances of NetworkConnectionManager detected. Destroying duplicate.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        messageHandlers = new Dictionary<NetworkMessageType, Action<SteamId, byte[]>>();
    }

    private void Update()
    {
        // Poll channels 0, 1, 3 (voice channel 2 is handled by VoiceChatP2P)
        foreach (int channel in channelsToPoll)
        {
            if (channel == 2) continue; // Skip voice channel
            PollChannel(channel);
        }
    }

    /// <summary>
    /// Polls a specific channel for incoming packets and routes them to handlers.
    /// </summary>
    private void PollChannel(int channel)
    {
        // Packet polling and routing
        while (SteamNetworking.IsP2PPacketAvailable(channel))
        {
            P2Packet? packet = SteamNetworking.ReadP2PPacket(channel);
            if (packet == null)
            {
                Debug.LogWarning($"Failed to read packet on channel {channel}");
                continue;
            }
            RoutePacket(packet.Value.SteamId, packet.Value.Data);
        }

    }

    /// <summary>
    /// Routes a received packet to the appropriate handler based on message type.
    /// </summary>
    private void RoutePacket(SteamId sender, byte[] data)
    {
        // Packet routing by parsing message type byte
        if (data.Length < 1)
        {
            Debug.LogWarning("Received empty packet");
            return;
        }

        NetworkMessageType msgType = (NetworkMessageType)data[0];
        if (messageHandlers.TryGetValue(msgType, out var handler))
        {
            handler.Invoke(sender, data); // Pass full data including message type
        }
        else
        {
            Debug.LogWarning($"No handler registered for message type {msgType}");
        }

    }

    /// <summary>
    /// Sends data to all connected peers (queries SteamManager for peer list).
    /// </summary>
    public void SendToAll(byte[] data, int channel, P2PSend sendType)
    {
        // Send to all peers via SteamManager.Instance.currentLobby.Members
        if (SteamManager.Instance == null || SteamManager.Instance.currentLobby.MemberCount == 0)
        {
            Debug.LogWarning("Cannot send data - not connected to a lobby.");
            return;
        }
        foreach (var member in SteamManager.Instance.currentLobby.Members)
        {
            if (member.Id != SteamManager.Instance.PlayerSteamId) // Don't send to self
            {
                SendTo(member.Id, data, channel, sendType);
            }
        }
    }

    /// <summary>
    /// Sends data to a specific peer.
    /// </summary>
    public void SendTo(SteamId targetId, byte[] data, int channel, P2PSend sendType)
    {
        // Send to specific peer
        if (SteamManager.Instance == null || SteamManager.Instance.currentLobby.MemberCount == 0)
        {
            Debug.LogWarning("Cannot send data - not connected to a lobby.");
            return;
        }
        if (!SteamManager.Instance.currentLobby.Members.ToList().Any(m => m.Id == targetId))
        {
            Debug.LogWarning($"Cannot send data - target {targetId} is not in the current lobby.");
            return;
        }

        SteamNetworking.SendP2PPacket(targetId, data, data.Length, channel, sendType);
    }

    /// <summary>
    /// Registers a handler for a specific message type.
    /// </summary>
    public void RegisterHandler(NetworkMessageType msgType, Action<SteamId, byte[]> handler)
    {
        // Handler registration
        if (!messageHandlers.ContainsKey(msgType))
        {
            messageHandlers[msgType] = handler;
        }
        else
        {
            messageHandlers[msgType] += handler;
        }
    }

    /// <summary>
    /// Unregisters a handler for a specific message type.
    /// </summary>
    public void UnregisterHandler(NetworkMessageType msgType, Action<SteamId, byte[]> handler)
    {
        // Handler unregistration
        if (messageHandlers.ContainsKey(msgType))
        {
            messageHandlers[msgType] -= handler;
            if (messageHandlers[msgType] == null)
            {
                messageHandlers.Remove(msgType);
            }
        }
    }

    /// <summary>
    /// Spawns a remote player prefab and tags ownership for the provided SteamId.
    /// </summary>
    public void SpawnRemotePlayerFor(SteamId steamId, string displayName = null)
    {
        if (!autoSpawnPlayer) return;
        if (playerPrefab == null)
        {
            Debug.LogWarning("NetworkConnectionManager has no playerPrefab assigned; cannot auto-spawn remote players.");
            return;
        }

        if (steamId == SteamManager.Instance?.PlayerSteamId) return;
        if (spawnedRemotePlayers.ContainsKey(steamId)) return;

        var instance = Instantiate(playerPrefab);

        DontDestroyOnLoad(instance);

        instance.name = string.IsNullOrEmpty(displayName)
            ? $"RemotePlayer_{steamId}"
            : $"RemotePlayer_{displayName}";

        var identity = instance.GetComponent<NetworkIdentity>();
        if (identity != null)
        {
            identity.SetOwner(steamId);
            identity.SetNetworkId((uint)steamId.Value); // Use SteamId as network ID for simplicity
        }

        spawnedRemotePlayers[steamId] = instance;
    }

    /// <summary>
    /// Destroys a previously spawned remote player instance for the given SteamId.
    /// </summary>
    public void DespawnRemotePlayer(SteamId steamId)
    {
        if (spawnedRemotePlayers.TryGetValue(steamId, out var instance))
        {
            Debug.Log($"Despawning remote player for {steamId}");
            if (instance != null)
            {
                Destroy(instance);
            }
            spawnedRemotePlayers.Remove(steamId);
        }
    }

    /// <summary>
    /// Cleans up all spawned remote player models.
    /// Call this on scene transitions to prevent player model leaks.
    /// Quick Fix #1: Player model cleanup on scene transition.
    /// </summary>
    public void CleanupAllRemotePlayers()
    {
        Debug.Log($"[NetworkConnectionManager] Cleaning up {spawnedRemotePlayers.Count} remote player models");
        
        foreach (var kvp in spawnedRemotePlayers)
        {
            if (kvp.Value != null)
            {
                Destroy(kvp.Value);
            }
        }
        
        spawnedRemotePlayers.Clear();
    }
}
}
