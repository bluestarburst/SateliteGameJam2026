using System;
using System.Collections.Generic;
using System.Linq;
using Steamworks;
using UnityEngine;
using SatelliteGameJam.Networking.Messages;
using SatelliteGameJam.Networking.Core;

namespace SatelliteGameJam.Networking.State
{
    /// <summary>
    /// Tracks per-player scene, role, and presence across the network.
    /// Provides events for player state changes (scene changes, role assignments, ready states).
    /// Does NOT duplicate SteamManager's peer list - queries it for current members.
    /// </summary>
    public class PlayerStateManager : MonoBehaviour
{
    public static PlayerStateManager Instance { get; private set; }

    // Per-player state cache
    private readonly Dictionary<SteamId, PlayerState> playerStates = new();

    // Events for game logic
    public event Action<SteamId, NetworkSceneId> OnPlayerSceneChanged;
    public event Action<SteamId, PlayerRole> OnRoleChanged;
    public event Action<SteamId> OnPlayerReady;
    public event Action<SteamId> OnPlayerJoined;
    public event Action<SteamId> OnPlayerLeft;
    private bool handlersRegistered;
    private bool steamEventsRegistered;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Register message handlers
        RepeatUntilRegistered();
    }

    private void Start()
    {
        RegisterSteamEvents();
    }

    private void RegisterSteamEvents()
    {
        if (steamEventsRegistered)
        {
            return;
        }

        if (SteamManager.Instance == null)
        {
            Invoke(nameof(RegisterSteamEvents), 0.5f);
            return;
        }

        SteamManager.Instance.RemotePlayerJoined += OnSteamRemotePlayerJoined;
        SteamManager.Instance.RemotePlayerLeft += OnSteamRemotePlayerLeft;
        steamEventsRegistered = true;
    }

    private void RepeatUntilRegistered()
    {
        if (handlersRegistered)
        {
            return;
        }

        if (NetworkConnectionManager.Instance == null)
        {
            Debug.LogWarning("PlayerStateManager: NetworkConnectionManager not found. Retrying...");
            Invoke(nameof(RepeatUntilRegistered), 0.5f);
            return;
        }

        NetworkConnectionManager.Instance.RegisterHandler(NetworkMessageType.PlayerReady, OnReceivePlayerReady);
        NetworkConnectionManager.Instance.RegisterHandler(NetworkMessageType.RoleAssign, OnReceiveRoleAssign);
        NetworkConnectionManager.Instance.RegisterHandler(NetworkMessageType.PlayerSceneState, OnReceivePlayerSceneState);
        handlersRegistered = true;
    }

    private void OnDestroy()
    {
        CancelInvoke(nameof(RepeatUntilRegistered));
        CancelInvoke(nameof(RegisterSteamEvents));
        if (NetworkConnectionManager.Instance != null && handlersRegistered)
        {
            NetworkConnectionManager.Instance.UnregisterHandler(NetworkMessageType.PlayerReady, OnReceivePlayerReady);
            NetworkConnectionManager.Instance.UnregisterHandler(NetworkMessageType.RoleAssign, OnReceiveRoleAssign);
            NetworkConnectionManager.Instance.UnregisterHandler(NetworkMessageType.PlayerSceneState, OnReceivePlayerSceneState);
        }

        if (SteamManager.Instance != null && steamEventsRegistered)
        {
            SteamManager.Instance.RemotePlayerJoined -= OnSteamRemotePlayerJoined;
            SteamManager.Instance.RemotePlayerLeft -= OnSteamRemotePlayerLeft;
        }
    }

    /// <summary>
    /// Gets the current state for a specific player.
    /// </summary>
    public PlayerState GetPlayerState(SteamId steamId)
    {
        if (playerStates.TryGetValue(steamId, out var state))
        {
            return state;
        }
        return new PlayerState { SteamId = steamId, Scene = NetworkSceneId.None, Role = PlayerRole.None };
    }

    public IReadOnlyCollection<PlayerState> GetPlayerStates()
    {
        return playerStates.Values.ToList();
    }

    public void HandleLocalLobbyEntered()
    {
        if (SteamManager.Instance == null || SteamManager.Instance.PlayerSteamId.Value == 0)
        {
            return;
        }

        UpdatePlayerState(SteamManager.Instance.PlayerSteamId, isConnected: true);

        if (!SteamManager.Instance.IsLocalPlayerLobbyHost)
        {
            RequestStateSnapshotFromAuthority();
        }
    }

    /// <summary>
    /// Sets the local player's scene and broadcasts to all peers.
    /// </summary>
    public void SetLocalPlayerScene(NetworkSceneId sceneId)
    {
        SteamId localId = SteamManager.Instance.PlayerSteamId;
        
        if (!playerStates.TryGetValue(localId, out var state))
        {
            state = new PlayerState { SteamId = localId };
            playerStates[localId] = state;
        }

        state.Scene = sceneId;
        state.LastUpdateTime = Time.time;

        // Broadcast to all peers
        BroadcastPlayerSceneState(localId, sceneId, state.Role);
        
        OnPlayerSceneChanged?.Invoke(localId, sceneId);
    }

    /// <summary>
    /// Sets the local player's role and broadcasts to all peers.
    /// </summary>
    public void SetLocalPlayerRole(PlayerRole role)
    {
        SteamId localId = SteamManager.Instance.PlayerSteamId;
        
        if (!playerStates.TryGetValue(localId, out var state))
        {
            state = new PlayerState { SteamId = localId };
            playerStates[localId] = state;
        }

        state.Role = role;
        state.LastUpdateTime = Time.time;

        // Broadcast role assignment
        BroadcastRoleAssign(localId, role);
        
        OnRoleChanged?.Invoke(localId, role);
    }

    /// <summary>
    /// Marks the local player as ready and broadcasts to all peers.
    /// </summary>
    public void SetLocalPlayerReady()
    {
        SteamId localId = SteamManager.Instance.PlayerSteamId;
        
        if (!playerStates.TryGetValue(localId, out var state))
        {
            state = new PlayerState { SteamId = localId };
            playerStates[localId] = state;
        }

        state.IsReady = true;

        // Broadcast ready state
        BroadcastPlayerReady(localId);
        
        OnPlayerReady?.Invoke(localId);
    }

    /// <summary>
    /// Adds or updates a remote player's state (called from message handlers).
    /// </summary>
    private void UpdatePlayerState(SteamId steamId, NetworkSceneId? scene = null, PlayerRole? role = null, bool? isReady = null, bool? isConnected = null)
    {
        bool isNewPlayer = false;
        
        if (!playerStates.TryGetValue(steamId, out var state))
        {
            state = new PlayerState { SteamId = steamId };
            playerStates[steamId] = state;
            isNewPlayer = true;
        }

        if (scene.HasValue) state.Scene = scene.Value;
        if (role.HasValue) state.Role = role.Value;
        if (isReady.HasValue) state.IsReady = isReady.Value;
        if (isConnected.HasValue) state.IsConnected = isConnected.Value;
        state.LastUpdateTime = Time.time;
        
        if (isNewPlayer)
        {
            OnPlayerJoined?.Invoke(steamId);
        }
    }

    /// <summary>
    /// Requests a state snapshot when joining an existing game session.
    /// </summary>
    public void RequestStateSnapshotFromAuthority()
    {
        // Request from SatelliteStateManager
        if (SatelliteStateManager.Instance != null)
        {
            // Small delay to ensure network is fully initialized
            Invoke(nameof(DelayedStateSnapshotRequest), 0.5f);
        }
    }

    private void DelayedStateSnapshotRequest()
    {
        if (SatelliteStateManager.Instance != null)
        {
            SatelliteStateManager.Instance.RequestStateSnapshot();
            Debug.Log("[PlayerStateManager] Requested state snapshot as late-joiner");
        }
    }

    /// <summary>
    /// Removes a player from state tracking (called when they leave).
    /// </summary>
    public void RemovePlayer(SteamId steamId)
    {
        if (playerStates.Remove(steamId))
        {
            OnPlayerLeft?.Invoke(steamId);
        }
    }

    public void SetPlayerConnected(SteamId steamId, bool isConnected)
    {
        if (steamId.Value == 0)
        {
            return;
        }

        bool wasKnown = playerStates.TryGetValue(steamId, out var previousState);
        bool wasConnected = !wasKnown || previousState.IsConnected;
        UpdatePlayerState(steamId, isConnected: isConnected);

        if (!isConnected && wasKnown && wasConnected)
        {
            OnPlayerLeft?.Invoke(steamId);
        }
    }

    public void ApplySnapshotPlayerState(SteamId steamId, NetworkSceneId scene, PlayerRole role, bool isReady)
    {
        PlayerState previous = GetPlayerState(steamId);
        bool sceneChanged = previous.Scene != scene;
        bool roleChanged = previous.Role != role;
        bool readyChanged = previous.IsReady != isReady;

        UpdatePlayerState(steamId, scene, role, isReady, IsCurrentLobbyMember(steamId));

        if (roleChanged)
        {
            OnRoleChanged?.Invoke(steamId, role);
        }

        if (readyChanged && isReady)
        {
            OnPlayerReady?.Invoke(steamId);
        }

        if (sceneChanged)
        {
            OnPlayerSceneChanged?.Invoke(steamId, scene);
        }
    }

    public void SetPlayerRoleFromAuthority(SteamId steamId, PlayerRole role)
    {
        if (!IsLocalAuthority())
        {
            return;
        }

        UpdatePlayerState(steamId, role: role, isConnected: IsCurrentLobbyMember(steamId));
        BroadcastRoleAssign(steamId, role);
        OnRoleChanged?.Invoke(steamId, role);
    }

    public void SetPlayerSceneFromAuthority(SteamId steamId, NetworkSceneId sceneId, PlayerRole role)
    {
        if (!IsLocalAuthority())
        {
            return;
        }

        if (steamId == SteamManager.Instance.PlayerSteamId)
        {
            SetLocalPlayerScene(sceneId);
            return;
        }

        UpdatePlayerState(steamId, scene: sceneId, role: role, isConnected: IsCurrentLobbyMember(steamId));
        BroadcastPlayerSceneState(steamId, sceneId, role);
        OnPlayerSceneChanged?.Invoke(steamId, sceneId);
    }

    // ===== Message Sending =====

    private void BroadcastPlayerReady(SteamId steamId)
    {
        if (NetworkConnectionManager.Instance == null) return;

        byte[] packet = new byte[9];
        packet[0] = (byte)NetworkMessageType.PlayerReady;
        int offset = 1;
        NetworkSerialization.WriteULong(packet, ref offset, steamId);
        NetworkConnectionManager.Instance.SendToAll(packet, 0, P2PSend.Reliable);
    }

    private void BroadcastRoleAssign(SteamId steamId, PlayerRole role)
    {
        if (NetworkConnectionManager.Instance == null) return;

        byte[] packet = new byte[10];
        packet[0] = (byte)NetworkMessageType.RoleAssign;
        int offset = 1;
        NetworkSerialization.WriteULong(packet, ref offset, steamId);
        packet[offset++] = (byte)role;
        NetworkConnectionManager.Instance.SendToAll(packet, 0, P2PSend.Reliable);
    }

    private void BroadcastPlayerSceneState(SteamId steamId, NetworkSceneId sceneId, PlayerRole role)
    {
        if (NetworkConnectionManager.Instance == null) return;

        byte[] packet = new byte[16]; // Type(1) + SteamId(8) + SceneId(2) + Role(1) + Timestamp(4)
        packet[0] = (byte)NetworkMessageType.PlayerSceneState;
        int offset = 1;
        NetworkSerialization.WriteULong(packet, ref offset, steamId);
        packet[offset++] = (byte)((ushort)sceneId >> 8);
        packet[offset++] = (byte)((ushort)sceneId & 0xFF);
        packet[offset++] = (byte)role;
        NetworkSerialization.WriteFloat(packet, ref offset, Time.time);
        NetworkConnectionManager.Instance.SendToAll(packet, 4, P2PSend.Reliable);
    }

    // ===== Message Receiving =====

    private void OnReceivePlayerReady(SteamId sender, byte[] data)
    {
        if (data.Length < 9) return;
        
        int offset = 1;
        SteamId playerId = NetworkSerialization.ReadULong(data, ref offset);
        if (!CanAcceptPlayerStateFrom(sender, playerId)) return;
        
        UpdatePlayerState(playerId, isReady: true);
        OnPlayerReady?.Invoke(playerId);
    }

    private void OnReceiveRoleAssign(SteamId sender, byte[] data)
    {
        if (data.Length < 10) return;
        
        int offset = 1;
        SteamId playerId = NetworkSerialization.ReadULong(data, ref offset);
        PlayerRole role = (PlayerRole)data[offset++];
        if (!CanAcceptPlayerStateFrom(sender, playerId)) return;
        
        UpdatePlayerState(playerId, role: role);
        OnRoleChanged?.Invoke(playerId, role);
    }

    private void OnReceivePlayerSceneState(SteamId sender, byte[] data)
    {
        if (data.Length < 16) return;
        
        int offset = 1;
        SteamId playerId = NetworkSerialization.ReadULong(data, ref offset);
        ushort sceneIdValue = (ushort)((data[offset++] << 8) | data[offset++]);
        NetworkSceneId sceneId = (NetworkSceneId)sceneIdValue;
        PlayerRole role = (PlayerRole)data[offset++];
        float timestamp = NetworkSerialization.ReadFloat(data, ref offset);
        if (!CanAcceptPlayerStateFrom(sender, playerId)) return;
        
        UpdatePlayerState(playerId, scene: sceneId, role: role);
        OnPlayerSceneChanged?.Invoke(playerId, sceneId);

        // If this assignment targets the local player, proactively acknowledge
        if (playerId == SteamManager.Instance.PlayerSteamId && SceneSyncManager.Instance != null)
        {
            SceneSyncManager.Instance.AcknowledgeCurrentScene();
        }
    }

    private void OnSteamRemotePlayerJoined(SteamId steamId, string displayName)
    {
        UpdatePlayerState(steamId, isConnected: true);
    }

    private void OnSteamRemotePlayerLeft(SteamId steamId)
    {
        SetPlayerConnected(steamId, false);
    }

    private bool CanAcceptPlayerStateFrom(SteamId sender, SteamId playerId)
    {
        if (sender.Value == 0 || playerId.Value == 0)
        {
            return false;
        }

        if (!IsCurrentLobbyMember(sender) || !IsCurrentLobbyMember(playerId))
        {
            return false;
        }

        return sender == playerId || (SteamManager.Instance != null && SteamManager.Instance.IsLobbyHost(sender));
    }

    private bool IsLocalAuthority()
    {
        return SteamManager.Instance != null && SteamManager.Instance.IsLocalPlayerLobbyHost;
    }

    private bool IsCurrentLobbyMember(SteamId steamId)
    {
        if (SteamManager.Instance == null || !SteamManager.Instance.HasActiveLobby)
        {
            return steamId == SteamManager.Instance?.PlayerSteamId;
        }

        foreach (var member in SteamManager.Instance.currentLobby.Members)
        {
            if (member.Id == steamId)
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>
/// Represents the network state of a single player.
/// </summary>
public class PlayerState
{
    public SteamId SteamId;
    public NetworkSceneId Scene = NetworkSceneId.None;
    public PlayerRole Role = PlayerRole.None;
    public bool IsReady = false;
    public bool IsConnected = true;
    public float LastUpdateTime = 0f;
}
}
