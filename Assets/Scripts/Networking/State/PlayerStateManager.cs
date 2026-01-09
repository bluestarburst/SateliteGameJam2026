using System;
using System.Collections.Generic;
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
        if (NetworkConnectionManager.Instance != null)
        {
            NetworkConnectionManager.Instance.RegisterHandler(NetworkMessageType.PlayerReady, OnReceivePlayerReady);
            NetworkConnectionManager.Instance.RegisterHandler(NetworkMessageType.RoleAssign, OnReceiveRoleAssign);
            NetworkConnectionManager.Instance.RegisterHandler(NetworkMessageType.PlayerSceneState, OnReceivePlayerSceneState);
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
    private void UpdatePlayerState(SteamId steamId, NetworkSceneId? scene = null, PlayerRole? role = null, bool? isReady = null)
    {
        if (!playerStates.TryGetValue(steamId, out var state))
        {
            state = new PlayerState { SteamId = steamId };
            playerStates[steamId] = state;
            OnPlayerJoined?.Invoke(steamId);
        }

        if (scene.HasValue) state.Scene = scene.Value;
        if (role.HasValue) state.Role = role.Value;
        if (isReady.HasValue) state.IsReady = isReady.Value;
        state.LastUpdateTime = Time.time;
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

    // ===== Message Sending =====

    private void BroadcastPlayerReady(SteamId steamId)
    {
        byte[] packet = new byte[9];
        packet[0] = (byte)NetworkMessageType.PlayerReady;
        int offset = 1;
        NetworkSerialization.WriteULong(packet, ref offset, steamId);
        NetworkConnectionManager.Instance.SendToAll(packet, 0, P2PSend.Reliable);
    }

    private void BroadcastRoleAssign(SteamId steamId, PlayerRole role)
    {
        byte[] packet = new byte[10];
        packet[0] = (byte)NetworkMessageType.RoleAssign;
        int offset = 1;
        NetworkSerialization.WriteULong(packet, ref offset, steamId);
        packet[offset++] = (byte)role;
        NetworkConnectionManager.Instance.SendToAll(packet, 0, P2PSend.Reliable);
    }

    private void BroadcastPlayerSceneState(SteamId steamId, NetworkSceneId sceneId, PlayerRole role)
    {
        byte[] packet = new byte[15];
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
        
        UpdatePlayerState(playerId, isReady: true);
        OnPlayerReady?.Invoke(playerId);
    }

    private void OnReceiveRoleAssign(SteamId sender, byte[] data)
    {
        if (data.Length < 10) return;
        
        int offset = 1;
        SteamId playerId = NetworkSerialization.ReadULong(data, ref offset);
        PlayerRole role = (PlayerRole)data[offset++];
        
        UpdatePlayerState(playerId, role: role);
        OnRoleChanged?.Invoke(playerId, role);
    }

    private void OnReceivePlayerSceneState(SteamId sender, byte[] data)
    {
        if (data.Length < 15) return;
        
        int offset = 1;
        SteamId playerId = NetworkSerialization.ReadULong(data, ref offset);
        ushort sceneIdValue = (ushort)((data[offset++] << 8) | data[offset++]);
        NetworkSceneId sceneId = (NetworkSceneId)sceneIdValue;
        PlayerRole role = (PlayerRole)data[offset++];
        float timestamp = NetworkSerialization.ReadFloat(data, ref offset);
        
        UpdatePlayerState(playerId, scene: sceneId, role: role);
        OnPlayerSceneChanged?.Invoke(playerId, sceneId);
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
    public float LastUpdateTime = 0f;
}
}
