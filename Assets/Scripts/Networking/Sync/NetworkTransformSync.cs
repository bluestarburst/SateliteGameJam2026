using Steamworks;
using UnityEngine;
using SatelliteGameJam.Networking.Identity;
using SatelliteGameJam.Networking.Messages;
using SatelliteGameJam.Networking.Core;

namespace SatelliteGameJam.Networking.Sync
{
    /// <summary>
    /// Synchronizes transform (position, rotation, velocity) for owner-driven objects.
    /// Owner sends state at fixed rate, non-owners interpolate/extrapolate to received state.
    /// Suitable for player-held objects or owned game entities.
    /// Auto-adds NetworkIdentity if missing.
    /// </summary>
    public class NetworkTransformSync : NetworkSyncBase
{
    [Header("Sync Settings")]
    [SerializeField] private float sendRate = 10f;
    [SerializeField] private bool syncPosition = true;
    [SerializeField] private bool syncRotation = true;
    [SerializeField] private bool syncVelocity = false;
    
    private Rigidbody rb;
    private float nextSendTime;
    
    // Interpolation state
    private Vector3 targetPosition;
    private Quaternion targetRotation = Quaternion.identity;
    private Vector3 targetVelocity;
    private float lastReceiveTime;
    private bool hasReceivedState;
    
    protected override void OnNetworkSetupComplete()
    {
        rb = GetComponent<Rigidbody>();
        
        DependencyHelper.RetryUntilSuccess(
            this,
            TryRegisterHandlers,
            "NetworkConnectionManager",
            retryInterval: 0.1f,
            maxAttempts: 10
        );
    }

    private bool TryRegisterHandlers()
    {
        if (NetworkConnectionManager.Instance == null)
        {
            return false; // Retry
        }

        NetworkConnectionManager.Instance.RegisterHandler(
            NetworkMessageType.TransformSync, OnReceiveTransformSync);
        return true; // Success
    }
    
    private void Update()
    {
        if (!isInitialized) return;

        if (IsOwner())
        {
            if (Time.time >= nextSendTime)
            {
                SendTransformState();
                nextSendTime = Time.time + (1f / sendRate);
            }
        }
        else
        {
            InterpolateToTarget();
        }
    }
    
    /// <summary>
    /// Sends the current transform state to all peers.
    /// </summary>
    private void SendTransformState()
    {
       // Packet: [Type(1)][NetId(4)][OwnerSteamId(8)][Pos(12)][Rot(16)][Vel(12)]
        const int packetSize = 53;
        byte[] packet = new byte[packetSize];
        packet[0] = (byte)NetworkMessageType.TransformSync;
        int offset = 1;
        
        NetworkSerialization.WriteUInt(packet, ref offset, netIdentity.NetworkId);
        NetworkSerialization.WriteULong(packet, ref offset, netIdentity.OwnerSteamId);
        NetworkSerialization.WriteVector3(packet, ref offset, transform.position);
        NetworkSerialization.WriteQuaternion(packet, ref offset, transform.rotation);
        NetworkSerialization.WriteVector3(packet, ref offset, rb ? rb.linearVelocity : Vector3.zero);
        
        NetworkConnectionManager.Instance.SendToAll(packet, 1, P2PSend.UnreliableNoDelay);
    }
    
    /// <summary>
    /// Handles incoming transform sync packets.
    /// </summary>
    private void OnReceiveTransformSync(SteamId sender, byte[] data)
    {
        // Packet deserialization and state update
        const int expectedLength = 53;
        if (data == null || data.Length < expectedLength)
        {
            Debug.LogWarning($"TransformSync packet too small ({data?.Length ?? 0}/{expectedLength})");
            return;
        }

        int offset = 1;
        uint netId = NetworkSerialization.ReadUInt(data, ref offset);
        if (netId != netIdentity.NetworkId) return;

        SteamId ownerSteamId = NetworkSerialization.ReadULong(data, ref offset);
        netIdentity.SetOwner(ownerSteamId);
        
        if (!IsOwner())
        {
            targetPosition = NetworkSerialization.ReadVector3(data, ref offset);
            targetRotation = NetworkSerialization.ReadQuaternion(data, ref offset);
            targetVelocity = NetworkSerialization.ReadVector3(data, ref offset);
            lastReceiveTime = Time.time;
            hasReceivedState = true;
        }
    }
    
    /// <summary>
    /// Interpolates/extrapolates to the target transform state.
    /// </summary>
    private void InterpolateToTarget()
    {
        if (!hasReceivedState)
        {
            return;
        }

        // Smooth interpolation with extrapolation
        if (syncPosition)
        {
            float timeSinceReceive = Time.time - lastReceiveTime;
            Vector3 extrapolated = targetPosition + targetVelocity * timeSinceReceive;
            transform.position = Vector3.Lerp(transform.position, extrapolated, Time.deltaTime * 10f);
        }
        
        if (syncRotation)
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 10f);
    }
}
}
