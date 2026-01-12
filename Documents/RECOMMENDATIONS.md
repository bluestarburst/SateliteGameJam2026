# Networking Recommendations

> Comparison of current implementation against `networking-states.md` requirements

## Requirements Checklist

### Voice Chat

#### Lobby Scene
| Requirement | Status | Notes |
|-------------|--------|-------|
| Voice auto-enabled for all (no PTT) | NOT IMPLEMENTED | Currently uses PTT |
| No remote player prefabs | NOT IMPLEMENTED | Currently spawns full prefabs |
| Use voice proxies only | PARTIAL | `VoiceSessionManager` supports proxies but `LobbyNetworkingManager` spawns prefabs |

#### Ground Control
| Requirement | Status | Notes |
|-------------|--------|-------|
| Auto-send voice to other Ground Control players (always on) | NOT IMPLEMENTED | Currently uses PTT for all |
| Send voice to ALL Space players ONLY when at console | NOT IMPLEMENTED | Currently sends to all peers always |
| Only hears Space players when at transmission console | IMPLEMENTED | `VoiceSessionManager.ShouldHearPlayer()` checks `isLocalPlayerAtConsole` |
| Always hears other Ground Control players | IMPLEMENTED | Role check in `ShouldHearPlayer()` |

#### Space Scene
| Requirement | Status | Notes |
|-------------|--------|-------|
| Auto-send voice to nearby Space players (proximity, always on) | NOT IMPLEMENTED | Currently uses PTT for all |
| Send voice to Ground Control ONLY when pressing 'B' (PTT) | NOT IMPLEMENTED | Currently sends to all peers |
| Always hears Ground Control player | IMPLEMENTED | Role check in `ShouldHearPlayer()` |
| Hears other Space players within radius | IMPLEMENTED | `IsWithinProximity()` check |

### Satellite Status Sync
| Requirement | Status | Notes |
|-------------|--------|-------|
| Percent health | IMPLEMENTED | `SatelliteStateManager.health` |
| Damaged component indicators | IMPLEMENTED | `damageBits` bitfield (32 components) |
| Position/rotation of satellite parts | IMPLEMENTED | `PartTransformData` and `SatellitePartTransform` messages |

### Misc Items Sync
| Requirement | Status | Notes |
|-------------|--------|-------|
| Position/rotation of movable objects | IMPLEMENTED | `NetworkPhysicsObject`, `NetworkTransformSync` |
| State of interactable objects | IMPLEMENTED | `NetworkInteractionState` |
| State of consoles | IMPLEMENTED | `ConsoleState` messages |

### Security Camera Feeds (Optional)
| Requirement | Status | Notes |
|-------------|--------|-------|
| Ground control views space camera feeds | NOT IMPLEMENTED | Optional feature |
| Space views ground control camera feeds | NOT IMPLEMENTED | Optional feature |

---

## Critical Issues

### 1. Voice Sending Model Completely Wrong

**Problem:** `VoiceChatP2P` uses a single push-to-talk model. The spec requires:
- **Auto-voice** (always recording) to same-role players
- **Push-to-talk** ('B' key) for cross-role communication

**Spec Requirements:**
- **Ground Control:** Auto-send to other Ground Control, PTT to Space (when at console)
- **Space Station:** Auto-send to nearby Space players, PTT ('B') to Ground Control

**Current Code (VoiceChatP2P.cs:51-52):**
```csharp
// Single PTT key for ALL voice
bool shouldRecord = alwaysRecord || Keyboard.current[pushToTalkAction].isPressed;
SteamUser.VoiceRecord = shouldRecord;
```

**Recommended Fix:**

1. **Add second PTT key for cross-role communication:**
```csharp
[SerializeField] private Key crossRolePTTKey = Key.B; // PTT for Ground<->Space
```

2. **Separate recording logic:**
```csharp
private void Update()
{
    if (!isLocalPlayerActive) return;

    var localState = PlayerStateManager.Instance?.GetPlayerState(
        SteamManager.Instance.PlayerSteamId);

    // Determine if we should be recording
    bool shouldRecord = ShouldRecordVoice(localState);
    SteamUser.VoiceRecord = shouldRecord;

    if (SteamUser.HasVoiceData)
    {
        // ... compress voice data ...

        // Send with role-aware routing
        SendVoicePacketWithRoleRouting(compressedData, compressedRead, localState);
    }
}

private bool ShouldRecordVoice(PlayerState localState)
{
    // Lobby: Always recording (auto-voice to all lobby members)
    if (localState.Scene == NetworkSceneId.Lobby)
        return true;

    // Ground Control: Always record (auto to other Ground, auto to Space when at console)
    if (localState.Role == PlayerRole.GroundControl)
        return true;

    // Space Station: Always record (auto to nearby Space, PTT 'B' to Ground)
    if (localState.Role == PlayerRole.SpaceStation)
        return true;

    return false;
}

private void SendVoicePacketWithRoleRouting(byte[] compressed, int length, PlayerState localState)
{
    bool crossRolePTT = Keyboard.current[crossRolePTTKey].isPressed;
    bool atConsole = VoiceSessionManager.Instance?.IsLocalPlayerAtConsole ?? false;

    foreach (var member in SteamManager.Instance.currentLobby.Members)
    {
        if (member.Id == SteamManager.Instance.PlayerSteamId) continue;

        var targetState = PlayerStateManager.Instance.GetPlayerState(member.Id);

        if (ShouldSendVoiceTo(localState, targetState, crossRolePTT, atConsole))
        {
            SteamNetworking.SendP2PPacket(member.Id, packet, ...);
        }
    }
}

private bool ShouldSendVoiceTo(PlayerState local, PlayerState target, bool crossRolePTT, bool atConsole)
{
    // Lobby: send to everyone (PTT already checked in ShouldRecordVoice)
    if (local.Scene == NetworkSceneId.Lobby)
        return true;

    // Ground Control
    if (local.Role == PlayerRole.GroundControl)
    {
        // Always send to other Ground Control (auto-voice)
        if (target.Role == PlayerRole.GroundControl)
            return true;

        // Send to Space ONLY when at console (auto-voice when tethered)
        if (target.Role == PlayerRole.SpaceStation)
            return atConsole;

        return false;
    }

    // Space Station
    if (local.Role == PlayerRole.SpaceStation)
    {
        // Send to Ground Control ONLY when pressing 'B' (PTT)
        if (target.Role == PlayerRole.GroundControl)
            return crossRolePTT;

        // Auto-send to nearby Space players (proximity-based)
        if (target.Role == PlayerRole.SpaceStation)
            return IsWithinProximity(target.SteamId);

        return false;
    }

    return false;
}
```

### 2. Lobby Should Not Spawn Player Prefabs

**Problem:** `LobbyNetworkingManager.SpawnRemotePlayer()` spawns full remote player prefabs in lobby.

**Spec Requirement:** Lobby should use voice-only proxies, not full player models.

**Current Code (LobbyNetworkingManager.cs:113-128):**
```csharp
private void SpawnRemotePlayer(SteamId steamId, string displayName)
{
    // ... spawns full remote player prefab via NetworkConnectionManager
    NetworkConnectionManager.Instance?.SpawnRemotePlayerFor(steamId, displayName);
}
```

**Recommended Fix:**
```csharp
private void SpawnRemotePlayer(SteamId steamId, string displayName)
{
    if (spawnedPlayers.Contains(steamId)) return;

    // DON'T spawn prefab - just create voice proxy
    // VoiceSessionManager already handles this:
    VoiceSessionManager.Instance?.GetOrCreateVoiceRemotePlayer(steamId);

    spawnedPlayers.Add(steamId);

    if (logDebug)
        Debug.Log($"[Lobby] Created voice proxy for: {displayName}");
}
```

Also remove the `RegisterRemotePlayerAvatar` call since we're not spawning avatars.

### 3. Handler Registration Issues

**Problem:** `NetworkTransformSync` and `NetworkPhysicsObject` register global handlers in `Awake()`. If multiple objects exist, each registers the same handler, causing:
- Multiple handler invocations per packet
- Handlers remaining registered after object destroyed

**Current Code (NetworkTransformSync.cs:38-39):**
```csharp
private void Awake()
{
    NetworkConnectionManager.Instance.RegisterHandler(
        NetworkMessageType.TransformSync, OnReceiveTransformSync);
}
```

**Recommended Fix:**
Option A: Register once globally, dispatch to objects via NetworkIdentity lookup
```csharp
// In a central TransformSyncManager (new component)
public class TransformSyncManager : MonoBehaviour
{
    public static TransformSyncManager Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
        NetworkConnectionManager.Instance.RegisterHandler(
            NetworkMessageType.TransformSync, OnReceiveTransformSync);
    }

    private void OnReceiveTransformSync(SteamId sender, byte[] data)
    {
        uint netId = NetworkSerialization.ReadUInt(data, ref offset);
        var identity = NetworkIdentity.GetById(netId);
        if (identity != null)
        {
            identity.GetComponent<NetworkTransformSync>()?.HandleSync(data);
        }
    }
}
```

Option B: Register/unregister in OnEnable/OnDisable and check NetworkId before processing

### 3. No Late-Join Synchronization

**Problem:** Players joining mid-game don't receive current state snapshot.

**Impact:**
- New players don't see correct satellite health
- New players don't see existing players' positions until next update
- Component damage states missing

**Recommended Fix:**
Add state snapshot request/response:
```csharp
// When player joins, request full state
public void RequestStateSnapshot(SteamId newPlayer)
{
    // Authority sends current satellite state
    // Each scene manager sends player positions
    // etc.
}
```

### 4. Missing Push-to-Talk State Sync

**Problem:** VoiceSessionManager should know when remote Ground Control players are at their console (for the "Ground control player only sends voice data when interacting with transmission console" rule).

**Current:** `playersAtConsole` HashSet exists but is only updated locally.

**Recommended Fix:**
Broadcast console interaction state:
```csharp
public void SetLocalPlayerAtConsole(bool atConsole)
{
    isLocalPlayerAtConsole = atConsole;

    // Broadcast to all peers
    byte[] packet = new byte[10];
    packet[0] = (byte)NetworkMessageType.ConsoleInteraction; // New message type
    // ... serialize state
    NetworkConnectionManager.Instance.SendToAll(packet, 0, P2PSend.Reliable);
}
```

---

## Architecture Improvements

### 1. Separate Voice Sending Logic from VoiceChatP2P

Create `VoiceSendingPolicy` to encapsulate role-based sending rules:
```csharp
public interface IVoiceSendingPolicy
{
    bool ShouldSendTo(PlayerState localState, PlayerState targetState);
}

public class SatelliteGameVoicePolicy : IVoiceSendingPolicy
{
    public bool ShouldSendTo(PlayerState local, PlayerState target)
    {
        // Implement spec requirements
    }
}
```

### 2. Centralize Sync Component Management

Create `NetworkSyncManager` to:
- Register handlers once
- Route packets to correct objects
- Handle object lifecycle (spawn/destroy)

### 3. Add Connection State Machine

Current implementation lacks:
- Heartbeat/keepalive
- Disconnect detection
- Reconnection logic

### 4. Consider State Authority Model

Current: Lowest SteamId is authority for satellite state.

Consider: Host authority or explicit authority assignment for clearer ownership.

---

## Priority Order

### P0 - Critical (Spec Violations)
1. **Voice model rewrite** - Implement dual-mode voice (auto + PTT)
   - Lobby: auto-voice to all (no PTT)
   - Ground Control: auto-voice to Ground, console-gated to Space
   - Space: auto-voice to nearby Space (proximity), 'B' PTT to Ground
2. **Lobby: no player prefabs** - Use voice proxies only
3. **Add 'B' key for cross-role PTT** in VoiceChatP2P

### P1 - Important
4. Handler registration cleanup
5. Console interaction state sync
6. Late-join synchronization

### P2 - Nice to Have
7. Connection state machine
8. Authority model refinement
9. Security camera feeds (optional feature)

---

## Implementation Estimate

| Task | Complexity | Files Changed |
|------|------------|---------------|
| Voice model rewrite (auto + PTT) | High | VoiceChatP2P.cs, VoiceSessionManager.cs |
| Lobby voice-only (no prefabs) | Low | LobbyNetworkingManager.cs |
| Add proximity check to voice sending | Medium | VoiceChatP2P.cs (needs player position access) |
| Console state sync | Low | VoiceSessionManager.cs, NetworkMessageType.cs |
| Handler registration fix | Medium | All Sync components, new manager |
| Late-join sync | High | Multiple state managers |
| Connection state machine | High | NetworkConnectionManager.cs, new component |
