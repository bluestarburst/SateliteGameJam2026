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
    /// Manages the authoritative state of the satellite (health, damaged components, console states).
    /// Sends deltas on Channel 4 when state changes. UI and consoles subscribe to events.
    /// Authority can be host-based or peer-agreed (recommend lowest SteamId as authority).
    /// </summary>
    public class SatelliteStateManager : MonoBehaviour
{
    public static SatelliteStateManager Instance { get; private set; }

    [Header("Satellite State")]
    [SerializeField] private float health = 100f;
    [SerializeField] private float sendRate = 1f; // Update rate in Hz for non-critical state

    // Component damage flags (bitfield for efficiency)
    private uint damageBits = 0;
    
    // Console states
    private readonly Dictionary<uint, ConsoleStateData> consoleStates = new();

    // Part transforms (if not handled by NetworkTransformSync/PhysicsSync)
    private readonly Dictionary<uint, PartTransformData> partTransforms = new();

    private float nextSendTime;
    private bool isDirty = false; // Track if state has changed

    // Events for UI/game logic
    public event Action<float> OnHealthChanged;
    public event Action<uint> OnComponentDamaged; // Damage bit index
    public event Action<uint> OnComponentRepaired; // Damage bit index
    public event Action<uint, ConsoleStateData> OnConsoleStateChanged;
    public event Action<uint, PartTransformData> OnPartTransformChanged;

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
            NetworkConnectionManager.Instance.RegisterHandler(NetworkMessageType.SatelliteHealth, OnReceiveSatelliteHealth);
            NetworkConnectionManager.Instance.RegisterHandler(NetworkMessageType.SatellitePartTransform, OnReceiveSatellitePartTransform);
            NetworkConnectionManager.Instance.RegisterHandler(NetworkMessageType.ConsoleState, OnReceiveConsoleState);
            NetworkConnectionManager.Instance.RegisterHandler(NetworkMessageType.StateSnapshotRequest, OnReceiveStateSnapshotRequest);
            NetworkConnectionManager.Instance.RegisterHandler(NetworkMessageType.StateSnapshotResponse, OnReceiveStateSnapshotResponse);
        }
    }

    private void Update()
    {
        // Send state updates at fixed rate if dirty
        if (IsAuthority() && isDirty && Time.time >= nextSendTime)
        {
            SendHealthUpdate();
            nextSendTime = Time.time + (1f / sendRate);
            isDirty = false;
        }
    }

    /// <summary>
    /// Determines if this client is the authority for satellite state.
    /// Using lowest SteamId as authority for simplicity.
    /// </summary>
    private bool IsAuthority()
    {
        if (SteamManager.Instance?.currentLobby == null) return false;
        
        var members = SteamManager.Instance.currentLobby.Members;
        if (members.Count() == 0) return false;

        SteamId lowestId = members.Min(m => m.Id.Value);
        return lowestId == SteamManager.Instance.PlayerSteamId;
    }

    // ===== Public API for game logic =====

    /// <summary>
    /// Sets the satellite health. Only authority should call this.
    /// </summary>
    public void SetHealth(float newHealth)
    {
        if (!IsAuthority())
        {
            Debug.LogWarning("[SatelliteStateManager] Non-authority attempted to set health");
            return;
        }

        health = Mathf.Clamp(newHealth, 0f, 100f);
        isDirty = true;
        OnHealthChanged?.Invoke(health);
    }

    public float GetHealth() => health;

    /// <summary>
    /// Marks a component as damaged. componentIndex is a bit position (0-31).
    /// </summary>
    public void SetComponentDamaged(int componentIndex)
    {
        if (!IsAuthority()) return;
        if (componentIndex < 0 || componentIndex >= 32) return;

        uint mask = 1u << componentIndex;
        if ((damageBits & mask) == 0)
        {
            damageBits |= mask;
            isDirty = true;
            OnComponentDamaged?.Invoke((uint)componentIndex);
        }
    }

    /// <summary>
    /// Marks a component as repaired.
    /// </summary>
    public void SetComponentRepaired(int componentIndex)
    {
        if (!IsAuthority()) return;
        if (componentIndex < 0 || componentIndex >= 32) return;

        uint mask = 1u << componentIndex;
        if ((damageBits & mask) != 0)
        {
            damageBits &= ~mask;
            isDirty = true;
            OnComponentRepaired?.Invoke((uint)componentIndex);
        }
    }

    public bool IsComponentDamaged(int componentIndex)
    {
        if (componentIndex < 0 || componentIndex >= 32) return false;
        uint mask = 1u << componentIndex;
        return (damageBits & mask) != 0;
    }

    /// <summary>
    /// Sets the state of a console. Authority broadcasts to all.
    /// </summary>
    public void SetConsoleState(uint consoleId, byte stateByte, byte[] payload = null)
    {
        if (!IsAuthority()) return;

        var state = new ConsoleStateData
        {
            ConsoleId = consoleId,
            StateByte = stateByte,
            Payload = payload
        };

        consoleStates[consoleId] = state;
        BroadcastConsoleState(state);
        OnConsoleStateChanged?.Invoke(consoleId, state);
    }

    public ConsoleStateData GetConsoleState(uint consoleId)
    {
        return consoleStates.TryGetValue(consoleId, out var state) ? state : null;
    }

    /// <summary>
    /// Sets the transform of a satellite part. Used if not synced via NetworkTransformSync.
    /// </summary>
    public void SetPartTransform(uint partId, Vector3 position, Quaternion rotation)
    {
        if (!IsAuthority()) return;

        var part = new PartTransformData
        {
            PartId = partId,
            Position = position,
            Rotation = rotation
        };

        partTransforms[partId] = part;
        BroadcastPartTransform(part);
        OnPartTransformChanged?.Invoke(partId, part);
    }

    public PartTransformData GetPartTransform(uint partId)
    {
        return partTransforms.TryGetValue(partId, out var part) ? part : null;
    }

    // ===== Message Sending =====

    private void SendHealthUpdate()
    {
        byte[] packet = new byte[9];
        packet[0] = (byte)NetworkMessageType.SatelliteHealth;
        int offset = 1;
        NetworkSerialization.WriteFloat(packet, ref offset, health);
        NetworkSerialization.WriteUInt(packet, ref offset, damageBits);
        
        // Quick Fix #3: Only send satellite health to Space Station players
        if (PlayerStateManager.Instance != null && SteamManager.Instance?.currentLobby != null)
        {
            foreach (var member in SteamManager.Instance.currentLobby.Members)
            {
                if (member.Id == SteamManager.Instance.PlayerSteamId)
                    continue;
                    
                var playerState = PlayerStateManager.Instance.GetPlayerState(member.Id);
                
                // Only send to players in Space Station scene
                if (playerState.Scene == NetworkSceneId.SpaceStation)
                {
                    NetworkConnectionManager.Instance.SendTo(member.Id, packet, 4, P2PSend.Reliable);
                }
            }
        }
        else
        {
            // Fallback: send to all if player state not available
            NetworkConnectionManager.Instance.SendToAll(packet, 4, P2PSend.Reliable);
        }
    }

    private void BroadcastPartTransform(PartTransformData part)
    {
        byte[] packet = new byte[37]; // Type(1) + PartId(4) + Pos(12) + Rot(16) + Timestamp(4)
        packet[0] = (byte)NetworkMessageType.SatellitePartTransform;
        int offset = 1;
        NetworkSerialization.WriteUInt(packet, ref offset, part.PartId);
        NetworkSerialization.WriteVector3(packet, ref offset, part.Position);
        NetworkSerialization.WriteQuaternion(packet, ref offset, part.Rotation);
        NetworkConnectionManager.Instance.SendToAll(packet, 4, P2PSend.Reliable);
    }

    private void BroadcastConsoleState(ConsoleStateData state)
    {
        int payloadSize = state.Payload?.Length ?? 0;
        byte[] packet = new byte[6 + payloadSize]; // Type(1) + ConsoleId(4) + StateByte(1) + Payload(N)
        packet[0] = (byte)NetworkMessageType.ConsoleState;
        int offset = 1;
        NetworkSerialization.WriteUInt(packet, ref offset, state.ConsoleId);
        packet[offset++] = state.StateByte;
        if (payloadSize > 0)
        {
            Buffer.BlockCopy(state.Payload, 0, packet, offset, payloadSize);
        }
        NetworkConnectionManager.Instance.SendToAll(packet, 4, P2PSend.Reliable);
    }

    // ===== Message Receiving =====

    private void OnReceiveSatelliteHealth(SteamId sender, byte[] data)
    {
        if (data.Length < 9) return;
        
        int offset = 1;
        float newHealth = NetworkSerialization.ReadFloat(data, ref offset);
        uint newDamageBits = NetworkSerialization.ReadUInt(data, ref offset);

        // Update local state
        health = newHealth;
        
        // Check for damage bit changes
        uint changedBits = damageBits ^ newDamageBits;
        for (int i = 0; i < 32; i++)
        {
            uint mask = 1u << i;
            if ((changedBits & mask) != 0)
            {
                bool isNowDamaged = (newDamageBits & mask) != 0;
                if (isNowDamaged)
                    OnComponentDamaged?.Invoke((uint)i);
                else
                    OnComponentRepaired?.Invoke((uint)i);
            }
        }
        
        damageBits = newDamageBits;
        OnHealthChanged?.Invoke(health);
    }

    private void OnReceiveSatellitePartTransform(SteamId sender, byte[] data)
    {
        if (data.Length < 37) return;
        
        int offset = 1;
        uint partId = NetworkSerialization.ReadUInt(data, ref offset);
        Vector3 position = NetworkSerialization.ReadVector3(data, ref offset);
        Quaternion rotation = NetworkSerialization.ReadQuaternion(data, ref offset);

        var part = new PartTransformData
        {
            PartId = partId,
            Position = position,
            Rotation = rotation
        };

        partTransforms[partId] = part;
        OnPartTransformChanged?.Invoke(partId, part);
    }

    private void OnReceiveConsoleState(SteamId sender, byte[] data)
    {
        if (data.Length < 6) return;
        
        int offset = 1;
        uint consoleId = NetworkSerialization.ReadUInt(data, ref offset);
        byte stateByte = data[offset++];
        
        byte[] payload = null;
        int payloadSize = data.Length - offset;
        if (payloadSize > 0)
        {
            payload = new byte[payloadSize];
            Buffer.BlockCopy(data, offset, payload, 0, payloadSize);
        }

        var state = new ConsoleStateData
        {
            ConsoleId = consoleId,
            StateByte = stateByte,
            Payload = payload
        };

        consoleStates[consoleId] = state;
        OnConsoleStateChanged?.Invoke(consoleId, state);
    }

    // ===== Late-Join Synchronization =====

    /// <summary>
    /// Requests a full state snapshot from authority (called by late-joining players).
    /// </summary>
    public void RequestStateSnapshot()
    {
        byte[] packet = new byte[9]; // Type(1) + SteamId(8)
        int offset = 0;
        packet[offset++] = (byte)NetworkMessageType.StateSnapshotRequest;
        NetworkSerialization.WriteULong(packet, ref offset, SteamManager.Instance.PlayerSteamId);

        // Send to all peers (authority will respond)
        NetworkConnectionManager.Instance.SendToAll(packet, 0, P2PSend.Reliable);
        
        Debug.Log("[SatelliteStateManager] Requested state snapshot");
    }

    /// <summary>
    /// Handles state snapshot request from a late-joining player.
    /// Only authority responds with full state.
    /// </summary>
    private void OnReceiveStateSnapshotRequest(SteamId sender, byte[] data)
    {
        if (!IsAuthority()) return;
        if (data.Length < 9) return;

        int offset = 1;
        SteamId requesterId = NetworkSerialization.ReadULong(data, ref offset);

        Debug.Log($"[SatelliteStateManager] Received state snapshot request from {requesterId}");
        SendStateSnapshot(requesterId);
    }

    /// <summary>
    /// Sends a full state snapshot to a specific player.
    /// </summary>
    private void SendStateSnapshot(SteamId targetSteamId)
    {
        // Calculate packet size: Type(1) + Health(4) + DamageBits(4) + ConsoleCount(2) + ConsoleData + PlayerStateCount(1)
        int consoleDataSize = 0;
        foreach (var console in consoleStates.Values)
        {
            consoleDataSize += 5; // ConsoleId(4) + StateByte(1)
            if (console.Payload != null)
                consoleDataSize += 2 + console.Payload.Length; // PayloadLength(2) + Payload(N)
            else
                consoleDataSize += 2; // PayloadLength(2) = 0
        }

        // For now, we'll keep player state simple (can be expanded later)
        int playerStateDataSize = 0;

        byte[] packet = new byte[1 + 4 + 4 + 2 + consoleDataSize + 1 + playerStateDataSize];
        int offset = 0;

        packet[offset++] = (byte)NetworkMessageType.StateSnapshotResponse;
        NetworkSerialization.WriteFloat(packet, ref offset, health);
        NetworkSerialization.WriteUInt(packet, ref offset, damageBits);
        NetworkSerialization.WriteUShort(packet, ref offset, (ushort)consoleStates.Count);

        // Serialize console states
        foreach (var console in consoleStates.Values)
        {
            NetworkSerialization.WriteUInt(packet, ref offset, console.ConsoleId);
            packet[offset++] = console.StateByte;

            ushort payloadLength = (ushort)(console.Payload?.Length ?? 0);
            NetworkSerialization.WriteUShort(packet, ref offset, payloadLength);

            if (payloadLength > 0)
            {
                Buffer.BlockCopy(console.Payload, 0, packet, offset, payloadLength);
                offset += payloadLength;
            }
        }

        // Player state count (for future expansion)
        packet[offset++] = 0;

        // Send directly to requester
        NetworkConnectionManager.Instance.SendTo(targetSteamId, packet, 0, P2PSend.Reliable);
        
        Debug.Log($"[SatelliteStateManager] Sent state snapshot to {targetSteamId}");
    }

    /// <summary>
    /// Handles received state snapshot response.
    /// </summary>
    private void OnReceiveStateSnapshotResponse(SteamId sender, byte[] data)
    {
        if (data.Length < 12) return; // Minimum: Type(1) + Health(4) + DamageBits(4) + ConsoleCount(2) + PlayerCount(1)

        int offset = 1;

        // Read satellite health and damage
        health = NetworkSerialization.ReadFloat(data, ref offset);
        uint newDamageBits = NetworkSerialization.ReadUInt(data, ref offset);

        // Check for damage bit changes and fire events
        uint changedBits = damageBits ^ newDamageBits;
        for (int i = 0; i < 32; i++)
        {
            uint mask = 1u << i;
            if ((changedBits & mask) != 0)
            {
                bool isNowDamaged = (newDamageBits & mask) != 0;
                if (isNowDamaged)
                    OnComponentDamaged?.Invoke((uint)i);
                else
                    OnComponentRepaired?.Invoke((uint)i);
            }
        }
        
        damageBits = newDamageBits;
        OnHealthChanged?.Invoke(health);

        // Read console states
        ushort consoleCount = NetworkSerialization.ReadUShort(data, ref offset);
        for (int i = 0; i < consoleCount; i++)
        {
            if (offset + 7 > data.Length) break; // Minimum: ConsoleId(4) + StateByte(1) + PayloadLength(2)

            uint consoleId = NetworkSerialization.ReadUInt(data, ref offset);
            byte stateByte = data[offset++];
            ushort payloadLength = NetworkSerialization.ReadUShort(data, ref offset);

            byte[] payload = null;
            if (payloadLength > 0 && offset + payloadLength <= data.Length)
            {
                payload = new byte[payloadLength];
                Buffer.BlockCopy(data, offset, payload, 0, payloadLength);
                offset += payloadLength;
            }

            var consoleState = new ConsoleStateData
            {
                ConsoleId = consoleId,
                StateByte = stateByte,
                Payload = payload
            };

            consoleStates[consoleId] = consoleState;
            OnConsoleStateChanged?.Invoke(consoleId, consoleState);
        }

        // Read player state count (for future use)
        if (offset < data.Length)
        {
            byte playerStateCount = data[offset++];
            // Future: read player states here
        }

        Debug.Log($"[SatelliteStateManager] Received and applied state snapshot from {sender}");
    }
}

/// <summary>
/// Console state data structure
/// </summary>
public class ConsoleStateData
{
    public uint ConsoleId;
    public byte StateByte; // Active screen, mode, etc.
    public byte[] Payload; // Additional data if needed
}

/// <summary>
/// Satellite part transform data
/// </summary>
public class PartTransformData
{
    public uint PartId;
    public Vector3 Position;
    public Quaternion Rotation;
}
}
