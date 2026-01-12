# Networking Prefab Setup Guide

This guide explains how to configure Unity prefabs for networking in the Satellite Game.

---

## Remote Player Prefab

The remote player prefab represents other players in the game world. It's automatically spawned by scene managers when players join.

### Required Components

#### 1. NetworkIdentity
- **Network Id**: 0 (auto-assigned at runtime)
- **Owner Steam Id**: Set automatically when spawned
- **Purpose**: Uniquely identifies this object across the network

#### 2. NetworkTransformSync
- **Sync Position**: TRUE
- **Sync Rotation**: TRUE
- **Sync Velocity**: FALSE (avatars don't need velocity sync)
- **Send Rate**: 20 Hz (configured in NetworkingConfiguration)
- **Purpose**: Synchronizes position and rotation across all clients

#### 3. Animator (Optional but Recommended)
- Attach your character animation controller
- Animations are local-only (not networked)
- Use NetworkAnimatorSync if you need synced animations

#### 4. AudioSource (For Voice Chat)
- **Spatial Blend**: 1.0 (full 3D audio)
- **Min Distance**: 0.5
- **Max Distance**: 100
- **Doppler Level**: 0
- **Play On Awake**: FALSE
- **Purpose**: Receives and plays voice chat audio from this player

### Setup Steps

1. Create a new GameObject named "RemotePlayerPrefab"
2. Add all required components
3. Configure AudioSource for 3D spatial audio
4. Assign to NetworkingConfiguration.remotePlayerPrefab
5. Save as prefab in `Assets/Prefabs/Networking/`

---

## Networked Tool Prefab (Pickupable)

Tools and objects that players can pick up, carry, and drop.

### Required Components

#### 1. NetworkIdentity
- **Network Id**: 0 (auto-assigned)
- **Purpose**: Identifies this object on the network

#### 2. NetworkInteractionState
- **Purpose**: Handles pickup/drop events and ownership changes
- No inspector configuration needed

#### 3. Rigidbody
- **Mass**: Depends on tool (e.g., 1.0 for wrench, 5.0 for heavy tool)
- **Drag**: 0.1
- **Angular Drag**: 0.05
- **Use Gravity**: TRUE
- **Is Kinematic**: FALSE (when not held)
- **Purpose**: Physics simulation when dropped

#### 4. Collider
- Use appropriate shape: BoxCollider, SphereCollider, MeshCollider
- **Is Trigger**: FALSE
- **Purpose**: Physical interaction with world and players

### Optional Components

- **NetworkTransformSync**: Add if using ownership-based movement (player holds tool)
  - Enable when object is picked up
  - Disable when object is dropped
  
- **NetworkPhysicsObject**: Add if using authority-based physics (shared physics objects)
  - Use for objects that multiple players can interact with simultaneously
  - Example: Soccer ball, pushable crates

### Setup Steps

1. Create GameObject for your tool
2. Add 3D model and materials
3. Add all required components
4. Configure Rigidbody mass based on tool weight
5. Add appropriate Collider
6. Save as prefab in `Assets/Prefabs/Tools/`

---

## Networked Physics Object (Shared Objects)

Objects with physics that multiple players can interact with (e.g., soccer ball, floating debris).

### Required Components

#### 1. NetworkIdentity
- **Network Id**: 0 (auto-assigned)
- **Purpose**: Network identification

#### 2. NetworkPhysicsObject
- **Send Rate**: 10 Hz (configured in NetworkingConfiguration)
- **Authority Handoff Cooldown**: 0.2 seconds
- **Purpose**: Synchronizes physics state and handles authority transfer

#### 3. Rigidbody
- **Mass**: Appropriate for object (e.g., 0.5 for ball, 10.0 for crate)
- **Drag**: 0.1
- **Angular Drag**: 0.05
- **Use Gravity**: TRUE
- **Is Kinematic**: FALSE
- **Interpolate**: Interpolate (for smooth movement)
- **Collision Detection**: Continuous (for fast-moving objects)
- **Constraints**: None (unless object should not rotate)

#### 4. Collider
- Use appropriate shape
- **Is Trigger**: FALSE
- **Physics Material**: Optional (for bounce, friction)

### Setup Steps

1. Create GameObject for physics object
2. Add 3D model
3. Add all required components
4. Tune Rigidbody mass and drag for desired feel
5. Test authority handoff by having multiple players interact
6. Save as prefab in `Assets/Prefabs/Physics/`

---

## Satellite Component Prefab

Components of the satellite that can be damaged and repaired.

### Required Components

#### 1. NetworkIdentity
- **Network Id**: Unique per component (assigned in inspector)
- **Purpose**: Identifies which satellite component this is

#### 2. NetworkTransformSync (If Component Moves)
- Use for rotating dishes, extendable solar panels, etc.
- **Sync Position**: TRUE
- **Sync Rotation**: TRUE
- **Send Rate**: 10 Hz (lower rate for satellite parts)

### Optional Components

- **Visual Feedback**: Particle systems, material swapping for damage states
- **Audio**: AudioSource for damage/repair sounds

### Setup Steps

1. Create GameObject for satellite component
2. Add 3D model
3. Add NetworkIdentity with unique ID
4. Add NetworkTransformSync if component moves/rotates
5. Connect to SatelliteStateManager for damage/repair events
6. Save as prefab in `Assets/Prefabs/Satellite/`

---

## Testing Your Prefabs

### Single Player Testing
1. Place prefab in scene manually
2. Verify components are configured correctly
3. Test physics behavior (if applicable)

### Network Testing
1. Build and run two instances of the game
2. Connect both to same lobby
3. Verify prefab spawns correctly
4. Test interactions (pickup, throw, etc.)
5. Monitor NetworkDebugOverlay (Tab key) for issues

### Common Issues

**Prefab not spawning:**
- Check NetworkingConfiguration.remotePlayerPrefab is assigned
- Verify autoSpawnPlayers is TRUE in config

**Physics jittering:**
- Increase Rigidbody mass
- Adjust drag values
- Check authority handoff cooldown

**Voice not working on remote players:**
- Verify AudioSource spatial blend is 1.0
- Check VoiceSessionManager is registering player avatar
- Confirm AudioSource is not set to Play On Awake

**Tools not pickupable:**
- Ensure NetworkInteractionState component exists
- Check NetworkIdentity is assigned
- Verify Collider is not set to Is Trigger

---

## Best Practices

1. **Keep prefabs lightweight**: Only add necessary components
2. **Use object pooling**: For frequently spawned objects
3. **Test early**: Verify networking behavior as you build
4. **Profile performance**: Monitor packet statistics in NetworkDebugOverlay
5. **Version control**: Keep prefabs in source control, track changes
6. **Naming convention**: Use clear, consistent prefab names
7. **Documentation**: Comment complex prefab setups in scene notes

---

## Reference: NetworkingConfiguration Settings

These prefab behaviors are controlled by NetworkingConfiguration:

- `autoSpawnPlayers`: Auto-spawn remote player prefabs
- `remotePlayerPrefab`: Prefab to spawn for remote players
- `playerTransformSyncRate`: Hz for player movement sync
- `physicsObjectSyncRate`: Hz for physics object sync
- `useInterpolation`: Smooth remote object movement
- `interpolationDelayMs`: Delay for interpolation buffer

Access via: Right-click in Project → Create → Networking → Configuration

---

For scene setup instructions, see [SCENE_SETUP.md](SCENE_SETUP.md)
