using Steamworks;
using UnityEngine;

/// <summary>
/// Synchronizes transform (position, rotation, velocity) for owner-driven objects.
/// Owner sends state at fixed rate, non-owners interpolate/extrapolate to received state.
/// Suitable for player-held objects or owned game entities.
/// </summary>
[RequireComponent(typeof(NetworkIdentity))]
public class NetworkTransformSync : MonoBehaviour
{
    [SerializeField] private float sendRate = 10f;
    [SerializeField] private bool syncPosition = true;
    [SerializeField] private bool syncRotation = true;
    [SerializeField] private bool syncVelocity = false;
    
    private NetworkIdentity netIdentity;
    private Rigidbody rb;
    private float nextSendTime;
    
    // Interpolation state
    private Vector3 targetPosition;
    private Quaternion targetRotation;
    private Vector3 targetVelocity;
    private float lastReceiveTime;
    
    private void Awake()
    {
        netIdentity = GetComponent<NetworkIdentity>();
        rb = GetComponent<Rigidbody>();
        
        NetworkConnectionManager.Instance.RegisterHandler(
            NetworkMessageType.TransformSync, OnReceiveTransformSync);
    }
    
    private void Update()
    {
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
    /// Returns true if the local player owns this object.
    /// </summary>
    private bool IsOwner()
    {
        // Ownership check
        return netIdentity.OwnerSteamId == SteamManager.Instance?.PlayerSteamId;
    }
    
    /// <summary>
    /// Sends the current transform state to all peers.
    /// </summary>
    private void SendTransformState()
    {
       // Packet: [Type(1)][NetId(4)][OwnerSteamId(8)][Pos(12)][Rot(12)][Vel(12)]
        byte[] packet = new byte[49];
        int offset = 0;
        
        packet[offset++] = (byte)NetworkMessageType.TransformSync;
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
        }
    }
    
    /// <summary>
    /// Interpolates/extrapolates to the target transform state.
    /// </summary>
    private void InterpolateToTarget()
    {
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
