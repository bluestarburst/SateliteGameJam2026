using Steamworks;
using UnityEngine;
using SatelliteGameJam.Networking.Identity;
using SatelliteGameJam.Networking.Messages;
using SatelliteGameJam.Networking.Core;

namespace SatelliteGameJam.Networking.Sync
{
    /// <summary>
    /// Synchronizes physics state for free-standing objects (soccer ball, etc.).
    /// Uses last-touch authority model - whoever last collided with the object becomes authoritative.
    /// Authority player simulates physics and broadcasts state; others interpolate/extrapolate.
    /// Auto-adds NetworkIdentity if missing. Requires Rigidbody.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class NetworkPhysicsObject : NetworkSyncBase
{
    [Header("Physics Sync Settings")]
    [SerializeField] private float sendRate = 10f; // Hz
    [SerializeField] private float authorityHandoffCooldown = 0.2f;
    [SerializeField] private bool editorRequestAuthority = false; // For testing in editor

    private Rigidbody rb;
    private SteamId currentAuthority;
    private float nextSendTime;
    private float lastHandoffTime;

    // Interpolation state
    private Vector3 targetPosition;
    private Quaternion targetRotation;
    private Vector3 targetVelocity;
    private Vector3 targetAngularVelocity;
    private float lastReceiveTime;

    [ContextMenu("Perform Action")]
    void MyAction()
    {
        Debug.Log("Action performed!");
    }

    protected override void OnNetworkSetupComplete()
    {
        rb = GetComponent<Rigidbody>();
        
        if (rb == null)
        {
            Debug.LogError($"[NetworkPhysicsObject] Rigidbody is required on {gameObject.name}");
            enabled = false;
            return;
        }

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

        NetworkConnectionManager.Instance.RegisterHandler(NetworkMessageType.PhysicsSync, OnReceivePhysicsSync);
        return true; // Success
    }

    private void Start()
    {
        // set initial target state to current state
        targetPosition = rb.position;
        targetRotation = rb.rotation;
        targetVelocity = rb.linearVelocity;
        targetAngularVelocity = rb.angularVelocity;
    }

    private void FixedUpdate()
    {
        if (!isInitialized) return;

        if (editorRequestAuthority)
        {
            RequestAuthority(SteamManager.Instance.PlayerSteamId);
            editorRequestAuthority = false;
        }
        // Send/receive logic based on authority
        if (IsAuthority())
        {
            // Send state at fixed rate
            if (Time.time >= nextSendTime)
            {
                SendPhysicsState();
                nextSendTime = Time.time + (1f / sendRate);
            }
        }
        else
        {
            // Interpolate to received state
            InterpolateToTarget();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!isInitialized) return;

        // Request authority on player collision
        if (collision.gameObject.CompareTag("Player"))
        {
            SteamId colliderId = GetPlayerSteamId(collision.gameObject);
            if (colliderId != 0 && Time.time - lastHandoffTime > authorityHandoffCooldown)
            {
                RequestAuthority(colliderId);
            }
        }
    }

    /// <summary>
    /// Returns true if the local player is the authority for this object.
    /// </summary>
    private bool IsAuthority()
    {
        if (!isInitialized) return false;
        // Authority check
        return currentAuthority == SteamManager.Instance.PlayerSteamId;
    }

    /// <summary>
    /// Requests authority transfer to a new player (deterministic tie-breaking).
    /// </summary>
    private void RequestAuthority(SteamId newAuthority)
    {
        // Deterministic: higher SteamId wins
        if (newAuthority > currentAuthority || currentAuthority == 0)
        {
            currentAuthority = newAuthority;
            lastHandoffTime = Time.time;
        }
    }

    /// <summary>
    /// Sends the current physics state to all peers.
    /// </summary>
    private void SendPhysicsState()
    {
        // Packet: [Type(1)][NetId(4)][AuthSteamId(8)][Pos(12)][Rot(16)][Vel(12)][AngVel(12)]
        const int packetSize = 65;
        byte[] packet = new byte[packetSize];
        packet[0] = (byte)NetworkMessageType.PhysicsSync;
        int offset = 1;
        
        NetworkSerialization.WriteUInt(packet, ref offset, netIdentity.NetworkId);
        NetworkSerialization.WriteULong(packet, ref offset, currentAuthority);
        NetworkSerialization.WriteVector3(packet, ref offset, rb.position);
        NetworkSerialization.WriteQuaternion(packet, ref offset, rb.rotation);
        NetworkSerialization.WriteVector3(packet, ref offset, rb.linearVelocity);
        NetworkSerialization.WriteVector3(packet, ref offset, rb.angularVelocity);
        
        NetworkConnectionManager.Instance.SendToAll(packet, 1, P2PSend.UnreliableNoDelay);
    }

    /// <summary>
    /// Handles incoming physics sync packets.
    /// </summary>
    private void OnReceivePhysicsSync(SteamId sender, byte[] data)
    {
        // Packet: [Type(1)][NetId(4)][AuthSteamId(8)][Pos(12)][Rot(16)][Vel(12)][AngVel(12)]
        const int expectedLength = 65;
        if (data == null || data.Length < expectedLength)
        {
            Debug.LogWarning($"PhysicsSync packet too small ({data?.Length ?? 0}/{expectedLength})");
            return;
        }

        int offset = 1;
        uint netId = NetworkSerialization.ReadUInt(data, ref offset);
        
        if (netId != netIdentity.NetworkId) return;
        
        SteamId authSteamId = NetworkSerialization.ReadULong(data, ref offset);
        currentAuthority = authSteamId;
        
        if (!IsAuthority())
        {
            targetPosition = NetworkSerialization.ReadVector3(data, ref offset);
            targetRotation = NetworkSerialization.ReadQuaternion(data, ref offset);
            targetVelocity = NetworkSerialization.ReadVector3(data, ref offset);
            targetAngularVelocity = NetworkSerialization.ReadVector3(data, ref offset);
            lastReceiveTime = Time.time;
        }
    }

    /// <summary>
    /// Interpolates/extrapolates to the received physics state.
    /// </summary>
    private void InterpolateToTarget()
    {
        float timeSinceReceive = Time.time - lastReceiveTime;
        
        // Extrapolate with velocity
        Vector3 extrapolatedPos = targetPosition + targetVelocity * timeSinceReceive;
        
        rb.position = Vector3.Lerp(rb.position, extrapolatedPos, Time.deltaTime * 10f);
        rb.rotation = Quaternion.Slerp(rb.rotation, targetRotation, Time.deltaTime * 10f);
        rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, targetVelocity, Time.deltaTime * 5f);
        rb.angularVelocity = Vector3.Lerp(rb.angularVelocity, targetAngularVelocity, Time.deltaTime * 5f);
    }

    /// <summary>
    /// Gets the SteamId of a player from their GameObject.
    /// </summary>
    private SteamId GetPlayerSteamId(GameObject playerObject)
    {
        // Try to get NetworkIdentity from player object
        var identity = playerObject.GetComponent<NetworkIdentity>();
        if (identity != null)
        {
            return identity.OwnerSteamId;
        }
        
        // Fallback: return local player if it's the local player's object
        return SteamManager.Instance?.PlayerSteamId ?? default;
    }
}
}
