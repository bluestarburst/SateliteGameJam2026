using System;
using System.Collections.Generic;
using System.Linq;
using Steamworks;
using Steamworks.Data;
using UnityEngine;
using SatelliteGameJam.Networking.Messages;
using SatelliteGameJam.Networking.Identity;
using SatelliteGameJam.Networking.Debugging;
using SatelliteGameJam.Networking.Core.Abstractions;

namespace SatelliteGameJam.Networking.Core
{
    /// <summary>
    /// Central packet router for all network messages.
    /// Routes packets by channel and message type to registered handlers.
    /// Does NOT track peers - queries SteamManager.Instance.currentLobby.Members for peer list.
    /// 
    /// Part 6 Extensibility Features:
    /// - Supports both legacy NetworkMessageType enum handlers and new INetworkMessage handlers
    /// - Uses NetworkHandlerRegistry for extensible message handling
    /// - Uses INetworkTransport abstraction for pluggable transport layer
    /// 
    /// Reference: DeveloperExperienceImprovements.md Part 6
    /// </summary>
    public class NetworkConnectionManager : MonoBehaviour
{
    public static NetworkConnectionManager Instance { get; private set; }

    // Legacy handler registration - maps message types to handlers (for backward compatibility)
    private Dictionary<NetworkMessageType, Action<SteamId, byte[]>> messageHandlers;

    // Part 6: Extensible handler system
    private NetworkHandlerRegistry handlerRegistry = new NetworkHandlerRegistry();
    private NetworkMessageRegistry messageRegistry = NetworkMessageRegistry.Instance;
    
    // Part 6: Transport abstraction
    private INetworkTransport transport;

    [Header("Configuration")]
    [SerializeField] private NetworkingConfiguration config;

    // Fallback settings if no config is assigned
    [Header("Fallback Settings (used if no config assigned)")]
    [SerializeField] private int[] channelsToPoll = new[] { 0, 1, 3, 4 }; // Exclude voice channel 2
    [SerializeField] private bool autoSpawnPlayer = true;
    [SerializeField] private GameObject playerPrefab;

    // Track spawned remote player instances to prevent duplicates and allow cleanup
    private readonly Dictionary<SteamId, GameObject> spawnedRemotePlayers = new();

    // Properties for accessing configuration
    private int[] ChannelsToPoll => config != null ? config.channelsToPoll : channelsToPoll;
    private bool AutoSpawnPlayer => config != null ? config.autoSpawnPlayers : autoSpawnPlayer;
    private GameObject PlayerPrefab => config != null ? config.remotePlayerPrefab : playerPrefab;


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

        // Part 6: Initialize transport layer
        InitializeTransport();

        // Validate configuration
        if (config != null)
        {
            config.ValidateConfiguration();
            
            // Part 6: Load custom message types if configured
            if (config.useExtensibleMessageSystem)
            {
                config.LoadCustomMessages();
            }
        }
        else
        {
            Debug.LogWarning("[NetworkConnectionManager] No NetworkingConfiguration assigned. Using fallback settings.");
        }
    }

    /// <summary>
    /// Part 6: Initialize transport layer based on configuration.
    /// </summary>
    private void InitializeTransport()
    {
        // Default to Steam P2P transport
        // In future, could be configured via NetworkingConfiguration
        transport = new SteamP2PTransport();
        
        uint appId = config != null ? config.steamAppId : 480;
        transport.Initialize(appId);
        
        Debug.Log($"[NetworkConnectionManager] Initialized transport: {transport.GetType().Name}");
    }

    private void Update()
    {
        // Poll channels from configuration
        foreach (int channel in ChannelsToPoll)
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
    /// Part 6: Now supports both legacy handlers and extensible INetworkMessage handlers.
    /// </summary>
    private void RoutePacket(SteamId sender, byte[] data)
    {
        // Record packet for debug overlay
        if (config != null && config.showPacketStatistics)
        {
            NetworkDebugOverlay.Instance?.RecordPacketReceived(data.Length);
        }

        // Packet routing by parsing message type byte
        if (data.Length < 1)
        {
            Debug.LogWarning("Received empty packet");
            return;
        }

        byte messageTypeId = data[0];

        // Part 6: Try extensible handler system first if enabled
        if (config != null && config.useExtensibleMessageSystem && handlerRegistry.HasHandlers(messageTypeId))
        {
            handlerRegistry.InvokeHandlers(sender, messageTypeId, data);
            return;
        }

        // Legacy handler system (backward compatibility)
        NetworkMessageType msgType = (NetworkMessageType)messageTypeId;
        if (messageHandlers.TryGetValue(msgType, out var handler))
        {
            handler.Invoke(sender, data); // Pass full data including message type
        }
        else
        {
            if (config != null && config.verboseLogging)
            {
                Debug.LogWarning($"[NetworkConnectionManager] No handler registered for message type {msgType} (ID: {messageTypeId})");
            }
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

        // Record packet for debug overlay
        if (config != null && config.showPacketStatistics)
        {
            NetworkDebugOverlay.Instance?.RecordPacketSent(data.Length);
        }
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
        if (!AutoSpawnPlayer) return;
        if (PlayerPrefab == null)
        {
            Debug.LogWarning("[NetworkConnectionManager] No playerPrefab assigned; cannot auto-spawn remote players.");
            return;
        }

        if (steamId == SteamManager.Instance?.PlayerSteamId) return;
        if (spawnedRemotePlayers.ContainsKey(steamId)) return;

        var instance = Instantiate(PlayerPrefab);

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
        
        if (config != null && config.verboseLogging)
        {
            Debug.Log($"[NetworkConnectionManager] Spawned remote player for {displayName ?? steamId.ToString()}");
        }
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

    // ===== Part 6: Extensible Message System API =====

    /// <summary>
    /// Part 6: Register a handler for an extensible message type.
    /// Use this for new message types implementing INetworkMessage.
    /// </summary>
    /// <example>
    /// <code>
    /// NetworkConnectionManager.Instance.RegisterHandler&lt;PlayerReadyMessage&gt;(OnPlayerReady);
    /// 
    /// void OnPlayerReady(SteamId sender, PlayerReadyMessage msg)
    /// {
    ///     Debug.Log($"Player {sender} is ready: {msg.IsReady}");
    /// }
    /// </code>
    /// </example>
    public void RegisterHandler<T>(NetworkMessageHandler<T> handler) where T : INetworkMessage, new()
    {
        handlerRegistry.RegisterHandler(handler);
    }

    /// <summary>
    /// Part 6: Unregister a handler for an extensible message type.
    /// </summary>
    public void UnregisterHandler<T>(NetworkMessageHandler<T> handler) where T : INetworkMessage, new()
    {
        handlerRegistry.UnregisterHandler(handler);
    }

    /// <summary>
    /// Part 6: Send a message to a specific peer using the extensible message system.
    /// </summary>
    /// <example>
    /// <code>
    /// var msg = new PlayerReadyMessage(steamId, true);
    /// NetworkConnectionManager.Instance.SendMessage(targetId, msg);
    /// </code>
    /// </example>
    public void SendMessage<T>(SteamId target, T message) where T : INetworkMessage
    {
        byte[] data = message.Serialize();
        P2PSend sendType = message.RequireReliable ? P2PSend.Reliable : P2PSend.UnreliableNoDelay;
        
        if (transport != null)
        {
            transport.SendPacket(target, data, message.Channel, message.RequireReliable);
        }
        else
        {
            // Fallback to legacy system
            SendTo(target, data, message.Channel, sendType);
        }

        // Record packet for debug overlay
        if (config != null && config.showPacketStatistics)
        {
            NetworkDebugOverlay.Instance?.RecordPacketSent(data.Length);
        }
    }

    /// <summary>
    /// Part 6: Broadcast a message to all peers using the extensible message system.
    /// </summary>
    /// <example>
    /// <code>
    /// var msg = new SatelliteHealthMessage(currentHealth, damageBits);
    /// NetworkConnectionManager.Instance.SendMessageToAll(msg);
    /// </code>
    /// </example>
    public void SendMessageToAll<T>(T message) where T : INetworkMessage
    {
        if (SteamManager.Instance?.currentLobby.MemberCount == 0)
        {
            Debug.LogWarning("[NetworkConnectionManager] Cannot broadcast message - not in lobby");
            return;
        }

        foreach (var member in SteamManager.Instance.currentLobby.Members)
        {
            if (member.Id != SteamManager.Instance.PlayerSteamId)
            {
                SendMessage(member.Id, message);
            }
        }
    }

    /// <summary>
    /// Part 6: Get the transport layer instance.
    /// Useful for testing or advanced use cases.
    /// </summary>
    public INetworkTransport GetTransport() => transport;

    /// <summary>
    /// Part 6: Set a custom transport layer.
    /// Useful for testing (e.g., MockTransport) or switching to different backends.
    /// </summary>
    /// <example>
    /// <code>
    /// // For testing:
    /// var mockTransport = new MockTransport();
    /// NetworkConnectionManager.Instance.SetTransport(mockTransport);
    /// </code>
    /// </example>
    public void SetTransport(INetworkTransport newTransport)
    {
        if (transport != null)
        {
            transport.Shutdown();
        }
        
        transport = newTransport;
        Debug.Log($"[NetworkConnectionManager] Switched to transport: {transport.GetType().Name}");
    }
}
}
