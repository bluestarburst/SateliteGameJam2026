# Player Networking Setup Guide

This guide covers how to set up both **local players** (the character you control) and **remote players** (other players' characters) for multiplayer synchronization.

---

## Table of Contents

1. [Local Player Setup](#local-player-setup)
2. [Remote Player Setup](#remote-player-setup)
3. [How Networking Works](#how-networking-works)
4. [Verification & Testing](#verification--testing)
5. [Troubleshooting](#troubleshooting)

---

# Local Player Setup

The **local player** is the character the user controls. It sends position/rotation/velocity data to all other players.

## 1. Create the Local Player GameObject

1. In Unity Hierarchy: `GameObject > 3D Object > Capsule` (or your character model)
2. Rename it to **"LocalPlayer"**
3. Add movement controller:
   - **Option A**: Character Controller (for kinematic movement like FPS)
   - **Option B**: Rigidbody (for physics-based movement)
4. Tag it as **"Player"**

## 2. Add Network Components

### Required: NetworkIdentity Component

- **Add Component** > Search for "NetworkIdentity"
- The Network ID will be auto-assigned when spawned
- Owner SteamId will be set to your local SteamId automatically

### Required: Choose ONE Sync Component

#### Option A: NetworkTransformSync (for Character Controller)

Use this for kinematic movement (walking, flying, FPS-style):

- **Add Component** > Search for "NetworkTransformSync"
- Configure in Inspector:
  - **Send Rate**: `10` Hz (adjusts network traffic)
  - **Sync Position**: ✓ Enabled
  - **Sync Rotation**: ✓ Enabled
  - **Sync Velocity**: ✓ Enable if you want smooth movement prediction

**How it works:**
- Every `1/sendRate` seconds (default 0.1s), your position/rotation is broadcast
- Uses **Channel 1** (unreliable/fast) for low-latency updates
- Other players interpolate to your last known position

#### Option B: NetworkPhysicsObject (for Rigidbody)

Use this for physics-based movement (soccer ball, ragdoll, vehicles):

- **Add Component** > Search for "NetworkPhysicsObject"
- **Requires**: Rigidbody component
- Configure in Inspector:
  - **Send Rate**: `10` Hz
  - **Authority Handoff Cooldown**: `0.2` seconds

**How it works:**
- Uses **last-touch authority** model
- Whoever last collided with the object controls its physics
- Broadcasts position, rotation, velocity, and angular velocity
- Authority transfers automatically on collision

### Optional: Audio Source (for Voice Chat)

If players can talk:

- **Add Component** > Audio Source
- Set **Spatial Blend** to `1` (3D positional audio)
- Enable **Loop**: `true`
- Set **Play On Awake**: `false`
- Adjust **Min Distance** and **Max Distance** for voice range

## 3. Set Ownership

The local player must have its NetworkIdentity ownership set at spawn:

```csharp
using SatelliteGameJam.Networking.Identity;
using Steamworks;

// When spawning the local player:
void SpawnLocalPlayer()
{
    GameObject playerObj = Instantiate(localPlayerPrefab, spawnPoint.position, Quaternion.identity);
    
    // Set network ownership to local player
    NetworkIdentity identity = playerObj.GetComponent<NetworkIdentity>();
    SteamId localSteamId = SteamManager.Instance.PlayerSteamId;
    identity.SetOwner(localSteamId);
    
    // Optional: Set a unique network ID
    identity.SetNetworkId((uint)localSteamId.Value);
}
```

## 4. Local Player Prefab (Optional)

You can create a prefab for consistent spawning:

1. Drag the LocalPlayer GameObject to your **Prefabs** folder
2. Delete the instance from the scene
3. Spawn it dynamically in your scene managers

---

# Remote Player Setup

The **remote player** is a visual representation of other players in the game. It receives and displays network data.

## 1. Create the Remote Player Prefab

1. In Unity Hierarchy: `GameObject > 3D Object > Capsule` (or character model)
2. Rename it to **"RemotePlayer"**
3. Add visual components (meshes, animations, name tags)
4. Tag it as **"Player"**

## 2. Add Required Network Components

### Required: NetworkIdentity Component

- **Add Component** > Search for "NetworkIdentity"
- Network ID will be set automatically at runtime (using SteamId)
- Owner SteamId is assigned by NetworkConnectionManager

### Required: Audio Source (for Voice Chat)

- **Add Component** > Audio Source
- Set **Spatial Blend** to `1` (3D positional audio)
- Enable **Loop**: `true`
- Set **Play On Awake**: `false`
- Adjust **Min Distance** and **Max Distance** for voice range

### Optional: NetworkTransformSync or NetworkPhysicsObject

Only add these if you want the remote player to:
- **Send** their own state (for player-owned objects like held items)
- **Receive** interpolated data from NetworkTransformSync/NetworkPhysicsObject on the local player

**Note**: Remote players automatically receive position data through their NetworkIdentity. You typically don't need sync components on the remote prefab itself unless they own networked objects.

## 3. Create the Prefab

1. Drag the **RemotePlayer** GameObject to your **Prefabs** folder
2. Delete the RemotePlayer instance from the scene
3. Your prefab is ready!

## 4. Assign to NetworkConnectionManager

### Via Inspector (Recommended)

1. Find the **SteamPack** GameObject in your scene
2. Expand to find **NetworkConnectionManager** component
3. In Inspector:
   - Check **Auto Spawn Player**: `true`
   - Drag **RemotePlayer** prefab into **Player Prefab** field

### Via Code

```csharp
using SatelliteGameJam.Networking.Core;

// In initialization script
NetworkConnectionManager.Instance.SetPlayerPrefab(remotePlayerPrefab, autoSpawn: true);
```

---

# How Networking Works

## Data Flow Overview

```
Local Player (You)
    ↓
NetworkTransformSync/NetworkPhysicsObject
    ↓
SendToAll() on Channel 1 (Unreliable)
    ↓
Steam P2P Network
    ↓
Other Players' Clients
    ↓
OnReceiveTransformSync/OnReceivePhysicsSync
    ↓
Remote Player GameObject (Interpolation)
```

## Components Explained

### NetworkIdentity

**Purpose**: Unique identification for networked objects

- **Network ID**: Unique uint identifier (scene objects use hash, players use SteamId)
- **Owner SteamId**: The Steam player who controls this object
- **Static Registry**: Global lookup table for all networked objects

**Key Methods:**
- `SetOwner(SteamId)` - Assigns ownership
- `SetNetworkId(uint)` - Assigns unique ID
- `GetById(uint)` - Finds objects by network ID

### NetworkTransformSync

**Purpose**: Syncs position/rotation for owner-driven objects

**Ownership Model:**
- **Owner**: The player whose `SteamId == NetworkIdentity.OwnerSteamId`
- Owner sends state at `sendRate` Hz
- Non-owners interpolate to received state

**Packet Structure** (53 bytes on Channel 1):
```
[Type(1)] [NetId(4)] [OwnerSteamId(8)] [Position(12)] [Rotation(16)] [Velocity(12)]
```

**Update Loop:**
```csharp
void Update()
{
    if (IsOwner())
    {
        // Send my position to everyone
        SendTransformState();
    }
    else
    {
        // Interpolate to received position
        InterpolateToTarget();
    }
}
```

### NetworkPhysicsObject

**Purpose**: Syncs physics state for free-standing objects (soccer ball, debris)

**Authority Model:**
- **Last-Touch Authority**: Whoever last collided with the object controls it
- Authority transfers on collision (with cooldown to prevent thrashing)
- Authority player simulates physics and broadcasts state

**Packet Structure** (65 bytes on Channel 1):
```
[Type(1)] [NetId(4)] [AuthSteamId(8)] [Position(12)] [Rotation(16)] 
[Velocity(12)] [AngularVelocity(12)]
```

**Authority Transfer:**
```csharp
private void OnCollisionEnter(Collision collision)
{
    if (collision.gameObject.CompareTag("Player"))
    {
        SteamId colliderId = GetPlayerSteamId(collision.gameObject);
        RequestAuthority(colliderId); // Deterministic handoff
    }
}
```

## Channel Usage

- **Channel 0**: Reliable control messages (lobby, scene changes)
- **Channel 1**: **Unreliable position/physics data** (low latency, high frequency)
- **Channel 2**: Voice chat (handled separately)
- **Channel 3**: Reliable interactions (button presses, pickups)
- **Channel 4**: State updates (player roles, satellite health)

## Interpolation & Extrapolation

Remote players don't directly set positions—they **interpolate** smoothly:

```csharp
// Extrapolate with velocity for smooth prediction
Vector3 extrapolatedPos = targetPosition + targetVelocity * timeSinceReceive;

// Lerp to extrapolated position
rb.position = Vector3.Lerp(rb.position, extrapolatedPos, Time.deltaTime * 10f);
```

This creates smooth movement even with packet loss or delay.

---

# Verification & Testing

## Local Player Checklist

- [ ] LocalPlayer GameObject has **NetworkIdentity** component
- [ ] LocalPlayer has **NetworkTransformSync** OR **NetworkPhysicsObject**
- [ ] NetworkIdentity ownership is set to local SteamId at spawn
- [ ] Player is tagged as **"Player"**
- [ ] If using NetworkTransformSync, Send Rate is configured (default 10 Hz)
- [ ] If using NetworkPhysicsObject, Rigidbody is attached

## Remote Player Checklist

- [ ] RemotePlayer prefab has **NetworkIdentity** component
- [ ] RemotePlayer prefab has **Audio Source** (Spatial Blend = 1)
- [ ] RemotePlayer prefab is assigned to **NetworkConnectionManager**
- [ ] **Auto Spawn Player** is checked on NetworkConnectionManager
- [ ] RemotePlayer is tagged as **"Player"**

## Runtime Test

### Single Player Test
1. Start the game and move your local player
2. Check Unity Console for: `Sending transform state at 10 Hz` (if debug enabled)
3. No errors should appear

### Multiplayer Test (2+ Players)
1. Player 1 starts a lobby
2. Player 2 joins the lobby
3. Both players transition to a scene (GroundControl or SpaceStation)
4. **Expected behavior:**
   - Player 1 sees Player 2's avatar appear
   - Player 2 sees Player 1's avatar appear
   - Both players see smooth movement when the other player moves
   - Voice chat works (positional audio based on distance)

### Debug Console Messages
```
[NetworkConnectionManager] Spawning remote player for 76561199245989703
[NetworkTransformSync] Received state from 76561199245989703
[VoiceSessionManager] Registered remote player 76561199245989703
```

---

# Troubleshooting

## Local Player Not Sending Data

**Symptom**: Other players don't see you move

**Fixes:**
- Verify `NetworkIdentity.OwnerSteamId == SteamManager.Instance.PlayerSteamId`
- Check that NetworkTransformSync or NetworkPhysicsObject is attached
- Ensure NetworkConnectionManager exists in scene (SteamPack prefab)
- Check Unity Console for packet send errors

## Remote Players Not Appearing

**Symptom**: You don't see other players

**Fixes:**
- Verify `Auto Spawn Player` is checked on NetworkConnectionManager
- Ensure RemotePlayer prefab reference isn't missing (shows as "None")
- Check scene managers (GroundControlSceneManager, SpaceStationSceneManager) are calling `SpawnExistingRemotePlayers()`
- Look for `Spawning remote player for [SteamId]` in console

## Jittery/Laggy Movement

**Symptom**: Remote players teleport or stutter

**Fixes:**
- Increase `sendRate` on NetworkTransformSync (try 20 Hz)
- Check network latency (Steam P2P usually <50ms)
- Ensure interpolation is enabled (it's automatic in both sync components)
- Verify you're using Channel 1 (UnreliableNoDelay) not Channel 0

## Players at Wrong Positions

**Symptom**: Remote players spawn in wrong locations

**Fixes:**
- Set spawn points in scene managers:
  ```csharp
  playerObj.transform.position = GetNextSpawnPoint();
  ```
- Check that `targetPosition` is being set correctly in `OnReceiveTransformSync`
- Ensure all clients are in the same scene (check PlayerStateManager.GetPlayerScene)

## Voice Not Working

**Symptom**: Can't hear other players

**Fixes:**
- Verify Audio Source has `Spatial Blend = 1` on RemotePlayer prefab
- Check VoiceSessionManager is registering players (console logs)
- Ensure players are in same scene
- Check voice gating rules in VoiceSessionManager.ShouldSendVoiceTo()
- Verify VoiceRemotePlayer component is attached at runtime (auto-added)

## NetworkIdentity ID Collisions

**Symptom**: Multiple players with same network ID

**Fixes:**
- For remote players: NetworkConnectionManager automatically assigns SteamId as network ID
- For scene objects: Ensure unique names and positions
- Check for duplicate prefab instances in scene
- Use explicit IDs: `identity.SetNetworkId((uint)steamId.Value)`

---

## Performance Tips

### Optimize Send Rate

Lower send rates save bandwidth but reduce smoothness:
- **High-frequency objects** (local player): 10-20 Hz
- **Low-frequency objects** (doors, pickups): 2-5 Hz
- **Static objects**: Don't sync at all

### Use Appropriate Sync Component

- **Character Controller players**: NetworkTransformSync
- **Physics objects** (soccer ball): NetworkPhysicsObject
- **Static/triggered objects**: Custom interaction packets (Channel 3)

### Culling & Interest Management

For large worlds, only sync objects near the player:
```csharp
// Only send if within range
if (Vector3.Distance(transform.position, playerPosition) < 50f)
{
    SendTransformState();
}
```

---

## Summary

### Local Player Requirements:
1. **NetworkIdentity** - ownership set to local SteamId
2. **NetworkTransformSync** OR **NetworkPhysicsObject** - broadcasts state
3. **Ownership assignment** - `identity.SetOwner(localSteamId)` at spawn

### Remote Player Requirements:
1. **NetworkIdentity** - receives network ID from NetworkConnectionManager
2. **Audio Source** - for voice chat (Spatial Blend = 1)
3. **Assigned to NetworkConnectionManager** - auto-spawns on player join

### Data Flow:
- Local player sends position/rotation/velocity on **Channel 1** at `sendRate` Hz
- Remote players receive and interpolate to smooth positions
- Voice chat handled separately by VoiceSessionManager
- All synced through Steam P2P networking
