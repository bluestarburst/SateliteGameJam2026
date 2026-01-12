# Network Architecture Plan

## Architecture Overview

```
SteamManager (existing - DontDestroyOnLoad)
├── Authoritative peer list: currentLobby.Members
├── OpponentSteamId (legacy 1v1, will remain for backwards compat)
└── Handles P2P session setup/teardown

NetworkConnectionManager (NEW - DontDestroyOnLoad)
├── Packet Router/Dispatcher ONLY (no peer tracking)
├── Routes by Channel + MessageType
├── Polls packets on multiple channels
├── Delegates to registered handlers
└── Creates new remote player prefab (optional)

NetworkIdentity (NEW - per networked object)
├── Unique NetworkId (uint)
├── OwnerSteamId
└── Registry: static Dictionary<uint, NetworkIdentity>

NetworkPhysicsObject (NEW - for soccer ball, etc.)
├── Requires: NetworkIdentity, Rigidbody
├── Authority: last-touch wins
├── Sends: Position, Rotation, Velocity, AngularVelocity @ 10Hz
└── Receives: Interpolates to authority state

NetworkInteractionState (NEW - for pickup/drop objects)
├── Requires: NetworkIdentity
├── Authority: current owner or null (free)
├── Sends: Discrete events (pickup, drop, use)
└── Receives: Locks object, transfers ownership

NetworkTransformSync (NEW - for player-owned objects)
├── Requires: NetworkIdentity
├── Authority: owner SteamId
├── Sends: Position, Rotation, Velocity @ 10Hz if owned
└── Receives: Interpolates if not owned

VoiceRemotePlayer (NEW - per remote speaker)
├── Requires: AudioSource
├── Manages per-sender voice buffer
├── Decompresses and plays audio from specific SteamId
└── Attached dynamically to remote player avatars
```

---

## Channel Assignments

- **Channel 0**: Game state (default, used by P2PGameObject)
- **Channel 1**: Transform/Physics sync (high frequency)
- **Channel 2**: Voice data (existing)
- **Channel 3**: Interactions/Events (reliable)

---

## Message Type Prefixes (1 byte)

```csharp
public enum NetworkMessageType : byte
{
    // Voice (Channel 2)
    VoiceData = 0x00,           // [SenderSteamId(8)][CompressedAudio(N)]
    
    // Transform/Physics (Channel 1)
    TransformSync = 0x10,        // [NetId(4)][OwnerSteamId(8)][Pos(12)][Rot(12)][Vel(12)]
    PhysicsSync = 0x11,          // [NetId(4)][AuthSteamId(8)][Pos(12)][Rot(12)][Vel(12)][AngVel(12)]
    
    // Interactions (Channel 3 - Reliable)
    InteractionPickup = 0x20,    // [NetId(4)][OwnerSteamId(8)]
    InteractionDrop = 0x21,      // [NetId(4)][OwnerSteamId(8)][Pos(12)][Vel(12)]
    InteractionUse = 0x22,       // [NetId(4)][UserSteamId(8)]
    
    // Authority changes
    AuthorityRequest = 0x30,     // [NetId(4)][RequesterSteamId(8)]
    AuthorityGrant = 0x31,       // [NetId(4)][NewAuthSteamId(8)]
}
```

---

## Component Responsibilities

### 1. NetworkConnectionManager (NEW)
**Location**: `Assets/Scripts/Networking/NetworkConnectionManager.cs`

**Purpose**: Central packet router - NO state duplication

```csharp
public class NetworkConnectionManager : MonoBehaviour
{
    public static NetworkConnectionManager Instance { get; private set; }
    
    // NO peer list - use SteamManager.Instance.currentLobby.Members
    
    // Handler registration
    private Dictionary<NetworkMessageType, Action<SteamId, byte[]>> messageHandlers;
    
    void Update()
    {
        // Poll channels 0, 1, 3 (voice already polled by VoiceChatP2P)
        PollChannel(0);
        PollChannel(1);
        PollChannel(3);
    }
    
    // Send to all peers (queries SteamManager for peer list)
    public void SendToAll(byte[] data, int channel, P2PSend sendType)
    {
        if (SteamManager.Instance?.currentLobby == null) return;
        
        foreach (var member in SteamManager.Instance.currentLobby.Members)
        {
            if (member.Id != SteamManager.Instance.PlayerSteamId)
                SteamNetworking.SendP2PPacket(member.Id, data, data.Length, channel, sendType);
        }
    }
    
    // Send to specific peer
    public void SendTo(SteamId targetId, byte[] data, int channel, P2PSend sendType)
    
    // Register handler for message type
    public void RegisterHandler(NetworkMessageType msgType, Action<SteamId, byte[]> handler)
}
```

**Key Points**:
- Polls channels 1, 3 (voice stays in VoiceChatP2P on channel 2)
- Uses `SteamManager.Instance.currentLobby.Members` for peer list
- Routes messages by parsing first byte (NetworkMessageType)
- NO state storage - pure dispatcher

---

### 2. NetworkIdentity (NEW)
**Location**: `Assets/Scripts/Networking/NetworkIdentity.cs`

**Purpose**: Unique ID per networked object

```csharp
public class NetworkIdentity : MonoBehaviour
{
    [SerializeField] private uint networkId;
    public uint NetworkId => networkId;
    
    public SteamId OwnerSteamId { get; set; }
    
    // Static registry for lookups
    private static Dictionary<uint, NetworkIdentity> registry = new();
    
    void Awake()
    {
        if (networkId == 0)
            networkId = GenerateNetworkId(); // Hash-based or scene index
        
        RegisterObject();
    }
    
    public static NetworkIdentity GetById(uint id)
    public void SetOwner(SteamId newOwner)
}
```

**Key Points**:
- Assigned in editor or generated at runtime
- Global registry for packet routing by NetworkId
- Tracks current owner SteamId

---

### 3. NetworkPhysicsObject (NEW)
**Location**: `Assets/Scripts/Networking/NetworkPhysicsObject.cs`

**Purpose**: Free-standing physics objects (soccer ball)

```csharp
[RequireComponent(typeof(NetworkIdentity), typeof(Rigidbody))]
public class NetworkPhysicsObject : MonoBehaviour
{
    [SerializeField] private float sendRate = 10f; // Hz
    [SerializeField] private float authorityHandoffCooldown = 0.2f;
    
    private NetworkIdentity netIdentity;
    private Rigidbody rb;
    private SteamId currentAuthority;
    private float nextSendTime;
    private float lastHandoffTime;
    
    // Interpolation
    private Vector3 targetPosition;
    private Quaternion targetRotation;
    private Vector3 targetVelocity;
    private Vector3 targetAngularVelocity;
    private float lastReceiveTime;
    
    void Awake()
    {
        netIdentity = GetComponent<NetworkIdentity>();
        rb = GetComponent<Rigidbody>();
        
        // Register for physics sync messages
        NetworkConnectionManager.Instance.RegisterHandler(
            NetworkMessageType.PhysicsSync, 
            OnReceivePhysicsSync
        );
    }
    
    void FixedUpdate()
    {
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
    
    void OnCollisionEnter(Collision collision)
    {
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
    
    private bool IsAuthority()
    {
        return currentAuthority == SteamManager.Instance.PlayerSteamId;
    }
    
    private void RequestAuthority(SteamId newAuthority)
    {
        // Deterministic: higher SteamId wins
        if (newAuthority > currentAuthority || currentAuthority == 0)
        {
            currentAuthority = newAuthority;
            lastHandoffTime = Time.time;
        }
    }
    
    private void SendPhysicsState()
    {
        // Packet: [Type(1)][NetId(4)][AuthSteamId(8)][Pos(12)][Rot(12)][Vel(12)][AngVel(12)]
        byte[] packet = new byte[61];
        int offset = 0;
        
        packet[offset++] = (byte)NetworkMessageType.PhysicsSync;
        WriteUInt(packet, ref offset, netIdentity.NetworkId);
        WriteULong(packet, ref offset, currentAuthority);
        WriteVector3(packet, ref offset, rb.position);
        WriteQuaternion(packet, ref offset, rb.rotation);
        WriteVector3(packet, ref offset, rb.velocity);
        WriteVector3(packet, ref offset, rb.angularVelocity);
        
        NetworkConnectionManager.Instance.SendToAll(packet, 1, P2PSend.UnreliableNoDelay);
    }
    
    private void OnReceivePhysicsSync(SteamId sender, byte[] data)
    {
        int offset = 1; // Skip message type
        uint netId = ReadUInt(data, ref offset);
        
        if (netId != netIdentity.NetworkId) return;
        
        SteamId authSteamId = ReadULong(data, ref offset);
        currentAuthority = authSteamId;
        
        if (!IsAuthority())
        {
            targetPosition = ReadVector3(data, ref offset);
            targetRotation = ReadQuaternion(data, ref offset);
            targetVelocity = ReadVector3(data, ref offset);
            targetAngularVelocity = ReadVector3(data, ref offset);
            lastReceiveTime = Time.time;
        }
    }
    
    private void InterpolateToTarget()
    {
        float timeSinceReceive = Time.time - lastReceiveTime;
        
        // Extrapolate with velocity
        Vector3 extrapolatedPos = targetPosition + targetVelocity * timeSinceReceive;
        
        rb.position = Vector3.Lerp(rb.position, extrapolatedPos, Time.deltaTime * 10f);
        rb.rotation = Quaternion.Slerp(rb.rotation, targetRotation, Time.deltaTime * 10f);
        rb.velocity = Vector3.Lerp(rb.velocity, targetVelocity, Time.deltaTime * 5f);
        rb.angularVelocity = Vector3.Lerp(rb.angularVelocity, targetAngularVelocity, Time.deltaTime * 5f);
    }
    
    // Serialization helpers (WriteVector3, ReadUInt, etc.) - see below
}
```

**Key Points**:
- Last-touch authority with deterministic tie-break
- Sends @ 10Hz on channel 1 (unreliable)
- Extrapolates with velocity for smooth remote view
- Cooldown prevents authority ping-pong

---

### 4. NetworkInteractionState (NEW)
**Location**: `Assets/Scripts/Networking/NetworkInteractionState.cs`

**Purpose**: Interactable objects with ownership (pickups)

```csharp
[RequireComponent(typeof(NetworkIdentity))]
public class NetworkInteractionState : MonoBehaviour
{
    private NetworkIdentity netIdentity;
    private SteamId currentOwner;
    private bool isOwned => currentOwner != 0;
    
    public System.Action<SteamId> OnPickedUp;
    public System.Action<SteamId> OnDropped;
    public System.Action<SteamId> OnUsed;
    
    void Awake()
    {
        netIdentity = GetComponent<NetworkIdentity>();
        
        NetworkConnectionManager.Instance.RegisterHandler(
            NetworkMessageType.InteractionPickup, OnReceivePickup);
        NetworkConnectionManager.Instance.RegisterHandler(
            NetworkMessageType.InteractionDrop, OnReceiveDrop);
        NetworkConnectionManager.Instance.RegisterHandler(
            NetworkMessageType.InteractionUse, OnReceiveUse);
    }
    
    public bool TryPickup(SteamId pickerId)
    {
        if (isOwned) return false; // Already owned
        
        currentOwner = pickerId;
        netIdentity.SetOwner(pickerId);
        
        // Send pickup event (reliable)
        byte[] packet = new byte[13];
        int offset = 0;
        packet[offset++] = (byte)NetworkMessageType.InteractionPickup;
        WriteUInt(packet, ref offset, netIdentity.NetworkId);
        WriteULong(packet, ref offset, pickerId);
        
        NetworkConnectionManager.Instance.SendToAll(packet, 3, P2PSend.Reliable);
        
        OnPickedUp?.Invoke(pickerId);
        return true;
    }
    
    public void Drop(Vector3 dropPosition, Vector3 dropVelocity)
    {
        if (!isOwned) return;
        
        SteamId dropperId = currentOwner;
        currentOwner = 0;
        netIdentity.SetOwner(0);
        
        // Send drop event with position/velocity (reliable)
        byte[] packet = new byte[37];
        int offset = 0;
        packet[offset++] = (byte)NetworkMessageType.InteractionDrop;
        WriteUInt(packet, ref offset, netIdentity.NetworkId);
        WriteULong(packet, ref offset, dropperId);
        WriteVector3(packet, ref offset, dropPosition);
        WriteVector3(packet, ref offset, dropVelocity);
        
        NetworkConnectionManager.Instance.SendToAll(packet, 3, P2PSend.Reliable);
        
        OnDropped?.Invoke(dropperId);
    }
    
    public void Use(SteamId userId)
    {
        // Send use event
        byte[] packet = new byte[13];
        int offset = 0;
        packet[offset++] = (byte)NetworkMessageType.InteractionUse;
        WriteUInt(packet, ref offset, netIdentity.NetworkId);
        WriteULong(packet, ref offset, userId);
        
        NetworkConnectionManager.Instance.SendToAll(packet, 3, P2PSend.Reliable);
        
        OnUsed?.Invoke(userId);
    }
    
    private void OnReceivePickup(SteamId sender, byte[] data)
    {
        int offset = 1;
        uint netId = ReadUInt(data, ref offset);
        if (netId != netIdentity.NetworkId) return;
        
        SteamId pickerId = ReadULong(data, ref offset);
        currentOwner = pickerId;
        netIdentity.SetOwner(pickerId);
        
        OnPickedUp?.Invoke(pickerId);
    }
    
    private void OnReceiveDrop(SteamId sender, byte[] data)
    {
        int offset = 1;
        uint netId = ReadUInt(data, ref offset);
        if (netId != netIdentity.NetworkId) return;
        
        SteamId dropperId = ReadULong(data, ref offset);
        Vector3 dropPos = ReadVector3(data, ref offset);
        Vector3 dropVel = ReadVector3(data, ref offset);
        
        currentOwner = 0;
        netIdentity.SetOwner(0);
        transform.position = dropPos;
        
        if (TryGetComponent<Rigidbody>(out var rb))
            rb.velocity = dropVel;
        
        OnDropped?.Invoke(dropperId);
    }
    
    private void OnReceiveUse(SteamId sender, byte[] data)
    {
        int offset = 1;
        uint netId = ReadUInt(data, ref offset);
        if (netId != netIdentity.NetworkId) return;
        
        SteamId userId = ReadULong(data, ref offset);
        OnUsed?.Invoke(userId);
    }
}
```

**Key Points**:
- Discrete events only (pickup, drop, use)
- Channel 3 with Reliable delivery
- First-come-first-served ownership
- Callbacks for game logic

---

### 5. NetworkTransformSync (NEW)
**Location**: `Assets/Scripts/Networking/NetworkTransformSync.cs`

**Purpose**: Owner-driven transform sync (player-held objects)

```csharp
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
    
    // Interpolation
    private Vector3 targetPosition;
    private Quaternion targetRotation;
    private Vector3 targetVelocity;
    private float lastReceiveTime;
    
    void Awake()
    {
        netIdentity = GetComponent<NetworkIdentity>();
        rb = GetComponent<Rigidbody>();
        
        NetworkConnectionManager.Instance.RegisterHandler(
            NetworkMessageType.TransformSync, OnReceiveTransformSync);
    }
    
    void Update()
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
    
    private bool IsOwner()
    {
        return netIdentity.OwnerSteamId == SteamManager.Instance.PlayerSteamId;
    }
    
    private void SendTransformState()
    {
        // Packet: [Type(1)][NetId(4)][OwnerSteamId(8)][Pos(12)][Rot(12)][Vel(12)]
        byte[] packet = new byte[49];
        int offset = 0;
        
        packet[offset++] = (byte)NetworkMessageType.TransformSync;
        WriteUInt(packet, ref offset, netIdentity.NetworkId);
        WriteULong(packet, ref offset, netIdentity.OwnerSteamId);
        WriteVector3(packet, ref offset, transform.position);
        WriteQuaternion(packet, ref offset, transform.rotation);
        WriteVector3(packet, ref offset, rb ? rb.velocity : Vector3.zero);
        
        NetworkConnectionManager.Instance.SendToAll(packet, 1, P2PSend.UnreliableNoDelay);
    }
    
    private void OnReceiveTransformSync(SteamId sender, byte[] data)
    {
        int offset = 1;
        uint netId = ReadUInt(data, ref offset);
        if (netId != netIdentity.NetworkId) return;
        
        SteamId ownerSteamId = ReadULong(data, ref offset);
        netIdentity.SetOwner(ownerSteamId);
        
        if (!IsOwner())
        {
            targetPosition = ReadVector3(data, ref offset);
            targetRotation = ReadQuaternion(data, ref offset);
            targetVelocity = ReadVector3(data, ref offset);
            lastReceiveTime = Time.time;
        }
    }
    
    private void InterpolateToTarget()
    {
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
```

**Key Points**:
- Owner sends state @ 10Hz
- Non-owners interpolate/extrapolate
- Optional velocity sync for prediction
- Channel 1, unreliable

---

### 6. VoiceRemotePlayer (NEW)
**Location**: `Assets/Scripts/Networking/VoiceRemotePlayer.cs`

**Purpose**: Per-sender voice playback (isolates audio sources)

```csharp
[RequireComponent(typeof(AudioSource))]
public class VoiceRemotePlayer : MonoBehaviour
{
    [SerializeField] private float playbackLatencySeconds = 0.1f;
    
    private AudioSource audioSource;
    private SteamId senderSteamId;
    
    private float[] audioclipBuffer;
    private int audioclipBufferSize;
    private int audioPlayerPosition;
    private int playbackBuffer;
    
    private Queue<PendingAudioBuffer> pendingBuffers = new();
    private MemoryStream uncompressedStream;
    
    public void Initialize(SteamId senderId)
    {
        senderSteamId = senderId;
        
        audioSource = GetComponent<AudioSource>();
        uncompressedStream = new MemoryStream();
        
        int optimalRate = (int)SteamUser.OptimalSampleRate;
        audioclipBufferSize = optimalRate * 5;
        audioclipBuffer = new float[audioclipBufferSize];
        
        audioSource.clip = AudioClip.Create($"Voice_{senderId}", optimalRate * 2, 1, optimalRate, true, OnAudioRead, null);
        audioSource.loop = true;
        audioSource.spatialBlend = 1.0f; // 3D audio
        audioSource.Play();
    }
    
    public void ReceiveVoiceData(byte[] compressed, int length)
    {
        try
        {
            var compressedStream = new MemoryStream(compressed, 0, length);
            uncompressedStream.Position = 0;
            
            int uncompressedWritten = SteamUser.DecompressVoice(compressedStream, length, uncompressedStream);
            
            if (uncompressedWritten > 0)
            {
                byte[] outputBuffer = new byte[uncompressedWritten];
                uncompressedStream.Position = 0;
                uncompressedStream.Read(outputBuffer, 0, uncompressedWritten);
                
                pendingBuffers.Enqueue(new PendingAudioBuffer
                {
                    ReadTime = Time.time + playbackLatencySeconds,
                    Buffer = outputBuffer,
                    Size = uncompressedWritten
                });
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to process voice from {senderSteamId}: {e}");
        }
    }
    
    void Update()
    {
        while (pendingBuffers.Count > 0 && pendingBuffers.Peek().ReadTime <= Time.time)
        {
            var pending = pendingBuffers.Dequeue();
            WriteToPlaybackBuffer(pending.Buffer, pending.Size);
        }
    }
    
    private void WriteToPlaybackBuffer(byte[] uncompressed, int size)
    {
        for (int i = 0; i < size; i += 2)
        {
            if (i + 1 >= size) break;
            
            short sample = (short)(uncompressed[i] | (uncompressed[i + 1] << 8));
            audioclipBuffer[audioPlayerPosition] = sample / 32767.0f;
            
            audioPlayerPosition = (audioPlayerPosition + 1) % audioclipBufferSize;
            playbackBuffer++;
        }
    }
    
    private void OnAudioRead(float[] data)
    {
        for (int i = 0; i < data.Length; ++i)
        {
            data[i] = 0;
            
            if (playbackBuffer > 0)
            {
                int readPosition = (audioPlayerPosition - playbackBuffer + audioclipBufferSize) % audioclipBufferSize;
                data[i] = audioclipBuffer[readPosition];
                playbackBuffer--;
            }
        }
    }
    
    private void OnDestroy()
    {
        uncompressedStream?.Dispose();
    }
    
    private class PendingAudioBuffer
    {
        public float ReadTime { get; set; }
        public byte[] Buffer { get; set; }
        public int Size { get; set; }
    }
}
```

**Key Points**:
- One instance per remote speaker
- Attached to remote player avatar for positional audio
- Isolated audio buffer/playback per sender
- Managed by updated VoiceChatP2P

---

### 7. VoiceChatP2P Updates (MODIFY EXISTING)
**Location**: `Assets/Scripts/Networking/VoiceChatP2P.cs`

**Changes**:
1. Remove single-opponent logic, use peer list from SteamManager
2. Prepend sender SteamId to voice packets
3. Route incoming voice to appropriate VoiceRemotePlayer
4. Remove local playback (only capture/send)
5. Manage VoiceRemotePlayer lifecycle

```csharp
// NEW: Dictionary to track remote voice players
private Dictionary<SteamId, VoiceRemotePlayer> remoteVoicePlayers = new();

// MODIFIED: SendVoicePacket - fan out to all peers
private void SendVoicePacket(byte[] compressed, int length)
{
    if (SteamManager.Instance?.currentLobby == null) return;
    
    // Prepend local player SteamId to packet
    byte[] packet = new byte[8 + length];
    Buffer.BlockCopy(BitConverter.GetBytes(SteamManager.Instance.PlayerSteamId.Value), 0, packet, 0, 8);
    Buffer.BlockCopy(compressed, 0, packet, 8, length);
    
    foreach (var member in SteamManager.Instance.currentLobby.Members)
    {
        if (member.Id != SteamManager.Instance.PlayerSteamId)
        {
            bool sent = SteamNetworking.SendP2PPacket(member.Id, packet, packet.Length, voiceChannel, P2PSend.UnreliableNoDelay);
            if (!sent)
                Debug.LogWarning($"Failed to send voice to {member.Name}");
        }
    }
}

// MODIFIED: HandleIncomingVoicePacket - route to correct remote player
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
        remoteVoicePlayers[senderId] = remotePlayer;
    }
    
    // Forward compressed data (skip SteamId header)
    byte[] compressedData = new byte[length - 8];
    Buffer.BlockCopy(data, 8, compressedData, 0, length - 8);
    remotePlayer.ReceiveVoiceData(compressedData, compressedData.Length);
}

// NEW: Create VoiceRemotePlayer for remote speaker
private VoiceRemotePlayer CreateRemoteVoicePlayer(SteamId senderId)
{
    // Find remote player's avatar by SteamId (you'll need a player manager)
    GameObject remoteAvatar = FindRemotePlayerAvatar(senderId);
    if (remoteAvatar == null)
    {
        // Fallback: create empty GameObject
        remoteAvatar = new GameObject($"RemoteVoice_{senderId}");
    }
    
    var remotePlayer = remoteAvatar.AddComponent<VoiceRemotePlayer>();
    remotePlayer.Initialize(senderId);
    return remotePlayer;
}

// NEW: Cleanup when players leave
public void RemoveRemotePlayer(SteamId steamId)
{
    if (remoteVoicePlayers.TryGetValue(steamId, out var remotePlayer))
    {
        Destroy(remotePlayer);
        remoteVoicePlayers.Remove(steamId);
    }
}

// REMOVED: All local playback code (audioclipBuffer, OnAudioRead, etc.)
// VoiceChatP2P now only captures/sends, doesn't play
```

**Key Points**:
- Multi-peer support via lobby member iteration
- Prepends sender SteamId (8 bytes) to voice packets
- Creates VoiceRemotePlayer per sender dynamically
- Removed local playback - now send-only

---

## Serialization Utilities

Create `Assets/Scripts/Networking/NetworkSerialization.cs`:

```csharp
using System;
using UnityEngine;

public static class NetworkSerialization
{
    // Write primitives
    public static void WriteUInt(byte[] buffer, ref int offset, uint value)
    {
        Buffer.BlockCopy(BitConverter.GetBytes(value), 0, buffer, offset, 4);
        offset += 4;
    }
    
    public static void WriteULong(byte[] buffer, ref int offset, ulong value)
    {
        Buffer.BlockCopy(BitConverter.GetBytes(value), 0, buffer, offset, 8);
        offset += 8;
    }
    
    public static void WriteFloat(byte[] buffer, ref int offset, float value)
    {
        Buffer.BlockCopy(BitConverter.GetBytes(value), 0, buffer, offset, 4);
        offset += 4;
    }
    
    public static void WriteVector3(byte[] buffer, ref int offset, Vector3 value)
    {
        WriteFloat(buffer, ref offset, value.x);
        WriteFloat(buffer, ref offset, value.y);
        WriteFloat(buffer, ref offset, value.z);
    }
    
    public static void WriteQuaternion(byte[] buffer, ref int offset, Quaternion value)
    {
        // Compress to 3 floats (smallest 3 components)
        // Or full 4 floats for simplicity
        WriteFloat(buffer, ref offset, value.x);
        WriteFloat(buffer, ref offset, value.y);
        WriteFloat(buffer, ref offset, value.z);
        WriteFloat(buffer, ref offset, value.w);
    }
    
    // Read primitives
    public static uint ReadUInt(byte[] buffer, ref int offset)
    {
        uint value = BitConverter.ToUInt32(buffer, offset);
        offset += 4;
        return value;
    }
    
    public static ulong ReadULong(byte[] buffer, ref int offset)
    {
        ulong value = BitConverter.ToUInt64(buffer, offset);
        offset += 8;
        return value;
    }
    
    public static float ReadFloat(byte[] buffer, ref int offset)
    {
        float value = BitConverter.ToSingle(buffer, offset);
        offset += 4;
        return value;
    }
    
    public static Vector3 ReadVector3(byte[] buffer, ref int offset)
    {
        return new Vector3(
            ReadFloat(buffer, ref offset),
            ReadFloat(buffer, ref offset),
            ReadFloat(buffer, ref offset)
        );
    }
    
    public static Quaternion ReadQuaternion(byte[] buffer, ref int offset)
    {
        return new Quaternion(
            ReadFloat(buffer, ref offset),
            ReadFloat(buffer, ref offset),
            ReadFloat(buffer, ref offset),
            ReadFloat(buffer, ref offset)
        );
    }
}
```

---

## Implementation Order

1. ✅ Create `NetworkSerialization.cs` - shared utilities
2. ✅ Create `NetworkConnectionManager.cs` - packet router
3. ✅ Create `NetworkIdentity.cs` - object identification
4. ✅ Create `VoiceRemotePlayer.cs` - per-sender voice playback
5. ✅ Update `VoiceChatP2P.cs` - multi-peer support
6. ✅ Create `NetworkTransformSync.cs` - owner-driven sync
7. ✅ Create `NetworkInteractionState.cs` - discrete events
8. ✅ Create `NetworkPhysicsObject.cs` - shared physics

---

## Key Design Principles

✅ **No Duplication**: SteamManager remains authoritative for peer list
✅ **Separation of Concerns**: Each component has one job
✅ **Authority-Based**: Clear ownership prevents conflicts
✅ **Efficient**: 10Hz updates, extrapolation, unreliable where safe
✅ **Scalable**: Multi-peer ready, not hardcoded to 1v1
✅ **Clean**: No spaghetti dependencies between systems

---

## Usage Examples

### Soccer Ball (Free Physics)
```csharp
GameObject ball = Instantiate(soccerBallPrefab);
ball.AddComponent<NetworkIdentity>().SetNetworkId(GenerateId());
ball.AddComponent<NetworkPhysicsObject>();
// Automatically syncs to all players, authority switches on collision
```

### Pickup Item
```csharp
GameObject sword = Instantiate(swordPrefab);
var netId = sword.AddComponent<NetworkIdentity>();
netId.SetNetworkId(GenerateId());

var interaction = sword.AddComponent<NetworkInteractionState>();
interaction.OnPickedUp += (ownerId) => {
    if (ownerId == SteamManager.Instance.PlayerSteamId)
        AttachToPlayerHand(sword);
};

// When player interacts:
interaction.TryPickup(SteamManager.Instance.PlayerSteamId);
```

### Player Avatar
```csharp
GameObject remotePlayer = Instantiate(playerPrefab);
var netId = remotePlayer.AddComponent<NetworkIdentity>();
netId.SetNetworkId(remoteSteamId);
netId.SetOwner(remoteSteamId);

remotePlayer.AddComponent<NetworkTransformSync>();
remotePlayer.AddComponent<VoiceRemotePlayer>().Initialize(remoteSteamId);
// Remote player now syncs position and voice automatically
```
