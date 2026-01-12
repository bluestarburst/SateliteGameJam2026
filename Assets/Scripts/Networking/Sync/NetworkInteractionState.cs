using System;
using Steamworks;
using UnityEngine;
using SatelliteGameJam.Networking.Identity;
using SatelliteGameJam.Networking.Messages;
using SatelliteGameJam.Networking.Core;

namespace SatelliteGameJam.Networking.Sync
{
    /// <summary>
    /// Manages networked interactions for objects (pickup, drop, use).
    /// Tracks ownership and sends discrete events rather than continuous state.
    /// Uses reliable messaging on channel 3 for critical events.
    /// </summary>
    [RequireComponent(typeof(NetworkIdentity))]
    public class NetworkInteractionState : MonoBehaviour
    {
        private NetworkIdentity netIdentity;
    private SteamId currentOwner;
    
    private bool IsOwned => currentOwner != 0;
    
    // Events for game logic
    public Action<SteamId> OnPickedUp;
    public Action<SteamId> OnDropped;
    public Action<SteamId> OnUsed;
    
    private void Awake()
    {
        netIdentity = GetComponent<NetworkIdentity>();
        // Handler registration moved to NetworkSyncManager (centralized)
    }
    
    /// <summary>
    /// Attempts to pick up this object for the specified player.
    /// Returns false if already owned.
    /// </summary>
    public bool TryPickup(SteamId pickerId)
    {
        if (IsOwned)
        {
            return false; // Already owned
        }

        currentOwner = pickerId;
        netIdentity.SetOwner(pickerId);

        // Send pickup event to all peers
        byte[] packet = new byte[13];
        int offset = 0;
        packet[offset++] = (byte)NetworkMessageType.InteractionPickup;
        NetworkSerialization.WriteUInt(packet, ref offset, netIdentity.NetworkId);
        NetworkSerialization.WriteULong(packet, ref offset, pickerId);

        NetworkConnectionManager.Instance.SendToAll(packet, 3, P2PSend.Reliable); // Reliable channel 3

        OnPickedUp?.Invoke(pickerId);
        return true;
    }
    
    /// <summary>
    /// Drops this object at the specified position with velocity.
    /// </summary>
    public void Drop(Vector3 dropPosition, Vector3 dropVelocity)
    {
        if (!IsOwned)
        {
            return; // Not owned, cannot drop
        }

        SteamId dropperId = currentOwner;
        currentOwner = 0;
        netIdentity.SetOwner(0);

        // Send drop event to all peers
        byte[] packet = new byte[37];
        int offset = 0;
        packet[offset++] = (byte)NetworkMessageType.InteractionDrop;
        NetworkSerialization.WriteUInt(packet, ref offset, netIdentity.NetworkId);
        NetworkSerialization.WriteULong(packet, ref offset, dropperId);
        NetworkSerialization.WriteVector3(packet, ref offset, dropPosition);
        NetworkSerialization.WriteVector3(packet, ref offset, dropVelocity);
        
        NetworkConnectionManager.Instance.SendToAll(packet, 3, P2PSend.Reliable);
        
        OnDropped?.Invoke(dropperId);
    }
    
    /// <summary>
    /// Triggers a use interaction on this object.
    /// </summary>
    public void Use(SteamId userId)
    {
        byte[] packet = new byte[13];
        int offset = 0;
        packet[offset++] = (byte)NetworkMessageType.InteractionUse;
        NetworkSerialization.WriteUInt(packet, ref offset, netIdentity.NetworkId);
        NetworkSerialization.WriteULong(packet, ref offset, userId);

        NetworkConnectionManager.Instance.SendToAll(packet, 3, P2PSend.Reliable);

        OnUsed?.Invoke(userId);
    }
    
    /// <summary>
    /// Handles incoming pickup event packets.
    /// Called by NetworkSyncManager after dispatching by NetworkId.
    /// </summary>
    public void HandlePickup(SteamId sender, byte[] data)
    {
        // Packet deserialization and ownership update
        // NetworkSyncManager already validated NetworkId and dispatched to us
        const int expectedLength = 13;
        if (data == null || data.Length < expectedLength)
        {
            Debug.LogWarning($"InteractionPickup packet too small ({data?.Length ?? 0}/{expectedLength})");
            return;
        }

        // Skip past message type (1) and NetworkId (4)
        int offset = 5;

        SteamId pickerId = NetworkSerialization.ReadULong(data, ref offset);
        currentOwner = pickerId;
        netIdentity.SetOwner(pickerId);

        OnPickedUp?.Invoke(pickerId);
    }
    
    /// <summary>
    /// Handles incoming drop event packets.
    /// Called by NetworkSyncManager after dispatching by NetworkId.
    /// </summary>
    public void HandleDrop(SteamId sender, byte[] data)
    {
        // Packet deserialization and object placement
        const int expectedLength = 37;
        if (data == null || data.Length < expectedLength)
        {
            Debug.LogWarning($"InteractionDrop packet too small ({data?.Length ?? 0}/{expectedLength})");
            return;
        }

        // Skip past message type (1) and NetworkId (4)
        int offset = 5;
        
        SteamId dropperId = NetworkSerialization.ReadULong(data, ref offset);
        Vector3 dropPos = NetworkSerialization.ReadVector3(data, ref offset);
        Vector3 dropVel = NetworkSerialization.ReadVector3(data, ref offset);
        
        currentOwner = 0;
        netIdentity.SetOwner(0);
        transform.position = dropPos;
        
        if (TryGetComponent<Rigidbody>(out var rb))
            rb.linearVelocity = dropVel;
        
        OnDropped?.Invoke(dropperId);
    }
    
    /// <summary>
    /// Handles incoming use event packets.
    /// Called by NetworkSyncManager after dispatching by NetworkId.
    /// </summary>
    public void HandleUse(SteamId sender, byte[] data)
    {
        // Packet deserialization and use callback
        const int expectedLength = 13;
        if (data == null || data.Length < expectedLength)
        {
            Debug.LogWarning($"InteractionUse packet too small ({data?.Length ?? 0}/{expectedLength})");
            return;
        }

        // Skip past message type (1) and NetworkId (4)
        int offset = 5;

        SteamId userId = NetworkSerialization.ReadULong(data, ref offset);
        OnUsed?.Invoke(userId);
    }
}
}
