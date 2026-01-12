# Networking Recommendations

> Comparison of current implementation against `networking-states.md` requirements

## Requirements Checklist

### Voice Chat

#### Lobby Scene
| Requirement | Status | Notes |
|-------------|--------|-------|
| Voice auto-enabled for all (no PTT) | IMPLEMENTED | `VoiceChatP2P.ShouldRecordVoice()` returns true for Lobby |
| No remote player prefabs | IMPLEMENTED | `LobbyNetworkingManager` creates voice proxies only |
| Use voice proxies only | IMPLEMENTED | `VoiceSessionManager.GetOrCreateVoiceRemotePlayer()` |

#### Ground Control
| Requirement | Status | Notes |
|-------------|--------|-------|
| Auto-send voice to other Ground Control players (always on) | IMPLEMENTED | `VoiceChatP2P.ShouldSendVoiceTo()` always true for Ground→Ground |
| Send voice to ALL Space players ONLY when at console | IMPLEMENTED | `VoiceChatP2P.ShouldSendVoiceTo()` checks `IsLocalPlayerAtConsole` |
| Only hears Space players when at transmission console | IMPLEMENTED | `VoiceSessionManager.ShouldHearPlayer()` checks `isLocalPlayerAtConsole` |
| Always hears other Ground Control players | IMPLEMENTED | Role check in `ShouldHearPlayer()` |

#### Space Scene
| Requirement | Status | Notes |
|-------------|--------|-------|
| Auto-send voice to nearby Space players (proximity, always on) | IMPLEMENTED | `VoiceChatP2P.ShouldSendVoiceTo()` checks `IsWithinProximityForSending()` |
| Send voice to Ground Control ONLY when pressing 'B' (PTT) | IMPLEMENTED | `VoiceChatP2P.ShouldSendVoiceTo()` checks `crossRolePTTKey` |
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

### 3. Handler Registration Issues ✅ RESOLVED

**Problem:** `NetworkTransformSync` and `NetworkPhysicsObject` were registering global handlers in `Awake()`. If multiple objects existed, each registered the same handler, causing:
- Multiple handler invocations per packet
- Handlers remaining registered after object destroyed

**Solution Implemented:**
Created `NetworkSyncManager` (see `Assets/Scripts/Networking/Sync/NetworkSyncManager.cs`) which:

1. **Registers handlers once** in its own Awake():
   - TransformSync → `OnReceiveTransformSync()`
   - PhysicsSync → `OnReceivePhysicsSync()`
   - InteractionPickup/Drop/Use → `OnReceiveInteraction*()`

2. **Dispatches to components** via NetworkIdentity lookup:
```csharp
private void OnReceiveTransformSync(SteamId sender, byte[] data)
{
    int offset = 1;
    uint netId = NetworkSerialization.ReadUInt(data, ref offset);
    
    var identity = NetworkIdentity.GetById(netId);
    if (identity != null)
    {
        identity.GetComponent<NetworkTransformSync>()?.HandleTransformSync(sender, data);
    }
}
```

3. **Individual components** no longer register handlers - they only implement `Handle*()` methods that are called by NetworkSyncManager

This eliminates duplicate registrations and ensures clean handler lifecycle management.

### 4. No Late-Join Synchronization ✅ COMPLETED

**Problem:** Players joining mid-game didn't receive current state snapshot.

**Impact:**
- New players didn't see correct satellite health
- New players didn't see existing players' positions until next update
- Component damage states missing

**Solution Implemented:**
See P1 Implementation Details section below for complete implementation.

### 5. Missing Push-to-Talk State Sync ✅ COMPLETED

**Problem:** VoiceSessionManager needed to know when remote Ground Control players are at their console.

**Solution Implemented:**
See P1 Implementation Details section below for complete implementation.

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

### 2. Centralize Sync Component Management ✅ IMPLEMENTED

`NetworkSyncManager` has been implemented to:
- Register handlers once (prevents duplicate registrations)
- Route packets to correct objects via NetworkIdentity lookup
- Eliminates per-component handler registration issues

See `Assets/Scripts/Networking/Sync/NetworkSyncManager.cs`

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

### P0 - Critical (Spec Violations) - COMPLETED
1. ~~**Voice model rewrite** - Implement dual-mode voice (auto + PTT)~~ DONE
   - Lobby: auto-voice to all (no PTT)
   - Ground Control: auto-voice to Ground, console-gated to Space
   - Space: auto-voice to nearby Space (proximity), 'B' PTT to Ground
2. ~~**Lobby: no player prefabs** - Use voice proxies only~~ DONE
3. ~~**Add 'B' key for cross-role PTT** in VoiceChatP2P~~ DONE

### P1 - Important - COMPLETED
4. ~~Handler registration cleanup~~ DONE - NetworkSyncManager centralizes all sync handlers
5. ~~Console interaction state sync (broadcast to peers when at console)~~ DONE
   - Added ConsoleInteraction message type (0x45)
   - VoiceSessionManager broadcasts console state to all peers
   - Remote console states tracked in playersAtConsole HashSet
6. ~~Late-join synchronization~~ DONE
   - Added StateSnapshotRequest (0x50) and StateSnapshotResponse (0x51) messages
   - SatelliteStateManager handles snapshot requests/responses
   - Authority sends full satellite state (health, damage bits, console states)
   - PlayerStateManager automatically requests snapshot on join

### P2 - Nice to Have
7. Connection state machine
8. Authority model refinement
9. Security camera feeds (optional feature)

---

## Implementation Status

| Task | Complexity | Status |
|------|------------|--------|
| Voice model rewrite (auto + PTT) | High | DONE |
| Lobby voice-only (no prefabs) | Low | DONE |
| Add proximity check to voice sending | Medium | DONE |
| Console state sync | Low | DONE |
| Handler registration fix | Medium | DONE |
| Late-join sync | High | DONE |
| Connection state machine | High | Pending |

---

## P1 Implementation Details

### Console Interaction State Sync
**Status:** ✅ COMPLETED

**Changes Made:**
1. Added `ConsoleInteraction` message type (0x45) to NetworkMessageType enum
2. Updated VoiceSessionManager:
   - `SetLocalPlayerAtConsole()` now broadcasts state to all peers via `BroadcastConsoleInteraction()`
   - Added `OnReceiveConsoleInteraction()` handler to receive remote console states
   - Console state tracked in `playersAtConsole` HashSet for all players
3. NetworkConnectionManager handler registration in VoiceSessionManager.Awake()

**Message Format:**
```
ConsoleInteraction: [Type(1)][SteamId(8)][AtConsole(1)]
```

### Late-Join Synchronization
**Status:** ✅ COMPLETED

**Changes Made:**
1. Added message types to NetworkMessageType enum:
   - `StateSnapshotRequest` (0x50)
   - `StateSnapshotResponse` (0x51)

2. Updated SatelliteStateManager:
   - Added `RequestStateSnapshot()` - sends request to all peers
   - Added `OnReceiveStateSnapshotRequest()` - authority responds with full state
   - Added `SendStateSnapshot()` - packages and sends complete state to requester
   - Added `OnReceiveStateSnapshotResponse()` - applies received snapshot
   - Registered new message handlers in Awake()

3. Updated PlayerStateManager:
   - Modified `UpdatePlayerState()` to detect new players joining
   - Added automatic state snapshot request for late-joiners
   - Added `RequestStateSnapshotFromAuthority()` and `DelayedStateSnapshotRequest()`

**State Snapshot Contents:**
- Satellite health (float)
- Component damage bits (uint32)
- Console states (count + data for each console)
  - Console ID, state byte, payload length, payload
- Player state count (reserved for future expansion)

**Message Format:**
```
StateSnapshotRequest: [Type(1)][RequesterSteamId(8)]
StateSnapshotResponse: [Type(1)][Health(4)][DamageBits(4)][ConsoleCount(2)][ConsoleData(N)][PlayerCount(1)][PlayerData(N)]
```

---

## Implementation Estimate
