# Networking Architecture - Current Implementation

> Last updated: January 2026

## Overview

The Satellite Game uses a **peer-to-peer networking architecture** built on Steam P2P via Facepunch Steamworks. The system supports role-based gameplay with Ground Control and Space Station scenes, voice chat with spatial awareness, and synchronized game state.

## Layer Architecture

```
┌─────────────────────────────────────────────────────┐
│ Game Logic Layer                                     │
│ (PlayerControllers, UI, Interactions)                │
└─────────────────────┬───────────────────────────────┘
                      │
┌─────────────────────▼───────────────────────────────┐
│ Scene Managers                                       │
│ ├── LobbyNetworkingManager                          │
│ ├── GroundControlSceneManager                       │
│ └── SpaceStationSceneManager                        │
└─────────────────────┬───────────────────────────────┘
                      │
┌─────────────────────▼───────────────────────────────┐
│ State Managers                                       │
│ ├── PlayerStateManager (scene, role, ready)         │
│ ├── SatelliteStateManager (health, components)      │
│ └── SceneSyncManager (scene transitions)            │
└─────────────────────┬───────────────────────────────┘
                      │
┌─────────────────────▼───────────────────────────────┐
│ NetworkConnectionManager                             │
│ ├── Packet routing by message type                  │
│ ├── Handler registration                            │
│ └── Remote player spawning                          │
└─────────────────────┬───────────────────────────────┘
                      │
┌─────────────────────▼───────────────────────────────┐
│ INetworkTransport Abstraction                       │
│ └── SteamP2PTransport (current implementation)      │
└─────────────────────┬───────────────────────────────┘
                      │
┌─────────────────────▼───────────────────────────────┐
│ Steam P2P (Facepunch Steamworks)                     │
└─────────────────────────────────────────────────────┘
```

## Network Channels

| Channel | Purpose | Reliability | Frequency |
|---------|---------|-------------|-----------|
| 0 | Control messages (ready, roles, scene) | Reliable | Low |
| 1 | Transform/Physics sync | Unreliable | High (10-20 Hz) |
| 2 | Voice data | Unreliable | Real-time |
| 3 | Interactions (pickup/drop/use) | Reliable | Event-driven |
| 4 | Game state (satellite health) | Reliable | Low (1 Hz) |

## Message Types

```csharp
// Control (Channel 0)
PlayerReady = 0x01          // Player ready state
SceneChangeRequest = 0x02   // Request scene transition
SceneChangeAck = 0x03       // Acknowledge scene change
RoleAssign = 0x04           // Role assignment

// Transform/Physics (Channel 1)
TransformSync = 0x10        // Owner-driven transform sync
PhysicsSync = 0x11          // Last-touch authority physics

// Interactions (Channel 3)
InteractionPickup = 0x20    // Object pickup
InteractionDrop = 0x21      // Object drop with velocity
InteractionUse = 0x22       // Object use/activate

// Game State (Channel 4)
PlayerSceneState = 0x40     // Player's current scene/role
SatelliteHealth = 0x42      // Satellite health + damage bits
SatellitePartTransform = 0x43 // Satellite part positions
ConsoleState = 0x44         // Console screen states
```

## Core Components

### NetworkConnectionManager
**Location:** `Assets/Scripts/Networking/Core/NetworkConnectionManager.cs`

Central packet router. Responsibilities:
- Polls channels 0, 1, 3, 4 (voice channel 2 handled by VoiceChatP2P)
- Routes packets to registered handlers based on message type byte
- Manages remote player prefab spawning/despawning
- Provides `SendTo()` and `SendToAll()` APIs

### NetworkSyncManager
**Location:** `Assets/Scripts/Networking/Sync/NetworkSyncManager.cs`

Centralized sync component dispatcher. Responsibilities:
- Registers sync message handlers ONCE (TransformSync, PhysicsSync, Interaction messages)
- Routes packets to correct sync components using NetworkIdentity lookup
- Prevents duplicate handler registrations from multiple component instances
- Eliminates need for individual components to register/unregister handlers

**Why it exists:** Without this, each NetworkTransformSync/NetworkPhysicsObject would register the same global handler in Awake(), causing multiple invocations per packet and handlers lingering after object destruction. NetworkSyncManager uses a centralized dispatch pattern - one manager receives all sync packets and forwards them to the appropriate component based on NetworkId.

### PlayerStateManager
**Location:** `Assets/Scripts/Networking/State/PlayerStateManager.cs`

Tracks per-player state. Responsibilities:
- Maintains `PlayerState` for each SteamId (scene, role, ready)
- Broadcasts state changes to all peers
- Fires events: `OnPlayerJoined`, `OnPlayerLeft`, `OnPlayerSceneChanged`, `OnRoleChanged`

### SatelliteStateManager
**Location:** `Assets/Scripts/Networking/State/SatelliteStateManager.cs`

Manages satellite game state. Responsibilities:
- Health percentage (0-100)
- Component damage bits (32 components max)
- Console states
- Part transforms (rotating solar panels, etc.)
- Authority: lowest SteamId in lobby

### VoiceSessionManager
**Location:** `Assets/Scripts/Networking/Voice/VoiceSessionManager.cs`

Controls voice chat routing. Responsibilities:
- Role-based voice gating (who can hear whom)
- Proximity-based filtering for Space Station
- Console interaction tracking for Ground Control
- Manages VoiceRemotePlayer components on avatars

### VoiceChatP2P
**Location:** `Assets/Scripts/Networking/Voice/VoiceChatP2P.cs`

Voice recording and transmission. Responsibilities:
- Push-to-talk recording via Steam Voice API
- Sends compressed audio on channel 2
- Routes incoming voice to VoiceRemotePlayer for playback

## Sync Components

**Note:** All sync components use a centralized handler pattern via NetworkSyncManager. Components no longer register their own packet handlers - NetworkSyncManager receives all sync packets and dispatches them to the appropriate component instance using NetworkIdentity lookup.

### NetworkTransformSync
**Location:** `Assets/Scripts/Networking/Sync/NetworkTransformSync.cs`

For owner-driven objects (player avatars). Features:
- Owner sends at configurable rate (default 10 Hz)
- Non-owners interpolate with velocity extrapolation
- Requires `NetworkIdentity` component
- Receives packets via `HandleTransformSync()` called by NetworkSyncManager

### NetworkPhysicsObject
**Location:** `Assets/Scripts/Networking/Sync/NetworkPhysicsObject.cs`

For physics objects (tools, balls). Features:
- Last-touch authority (whoever touched it last controls it)
- Sends position, rotation, linear velocity, angular velocity
- Higher sync rate (default 20 Hz) for responsive physics
- Receives packets via `HandlePhysicsSync()` called by NetworkSyncManager

### NetworkInteractionState
**Location:** `Assets/Scripts/Networking/Sync/NetworkInteractionState.cs`

For interactable objects. Features:
- Pickup/drop/use events (not continuous sync)
- Ownership tracking
- Reliable delivery on channel 3
- Receives packets via `HandlePickup()`, `HandleDrop()`, `HandleUse()` called by NetworkSyncManager

## Scene Managers

### LobbyNetworkingManager
- Sets player to `Lobby` role/scene
- Spawns all remote player models
- Voice: everyone hears everyone

### GroundControlSceneManager
- Sets player to `GroundControl` role/scene
- Spawns only Ground Control players (not Space)
- Voice gating: only hear Space when at console
- Subscribes to satellite state for UI updates

### SpaceStationSceneManager
- Sets player to `SpaceStation` role/scene
- Spawns only Space Station players (not Ground)
- Can repair/damage satellite components

## Voice Gating Rules

### Required Model (from networking-states.md)

**Sending:**
| Local Role | Target Role | When to Send |
|------------|-------------|--------------|
| Lobby | Any | **Auto** (always on, no PTT) |
| Ground Control | Ground Control | **Auto** (always recording) |
| Ground Control | Space Station | **Only when at console** (auto when tethered) |
| Space Station | Space Station | **Auto** (proximity-based) |
| Space Station | Ground Control | **Push-to-talk** ('B' key) |

**Lobby Notes:**
- Do NOT spawn remote player prefabs in lobby
- Use lightweight voice proxies (AudioSource only) via `VoiceSessionManager.GetOrCreateVoiceRemotePlayer()`

**Receiving (Implemented):**
| Local Role | Remote Role | Can Hear? |
|------------|-------------|-----------|
| Lobby | Any | Always |
| Ground Control | Ground Control | Always |
| Ground Control | Space Station | Only when at console |
| Space Station | Ground Control | Always |
| Space Station | Space Station | Within proximity radius |

### Current Implementation Status
- **Receiving:** Implemented correctly in `VoiceSessionManager.ShouldHearPlayer()`
- **Sending:** NOT implemented - currently sends to ALL peers with single PTT key

See `RECOMMENDATIONS.md` for the fix.

## Configuration

**NetworkingConfiguration** (`Assets/Scripts/Networking/NetworkingConfiguration.asset`)

ScriptableObject with Inspector-editable settings:
- Steam App ID
- Channels to poll
- Auto-spawn settings
- Remote player prefab
- Voice settings (proximity radius, etc.)
- Sync rates
- Debug options

## Extensibility

The system supports custom message types via `INetworkMessage` interface:

```csharp
public class CustomMessage : INetworkMessage
{
    public byte MessageTypeId => 0x70;
    public int Channel => 1;
    public bool RequireReliable => false;

    public byte[] Serialize() { /* ... */ }
    public void Deserialize(byte[] data) { /* ... */ }
}
```

Register handlers:
```csharp
NetworkConnectionManager.Instance.RegisterHandler<CustomMessage>(OnCustomMessage);
```

## File Structure

```
Assets/Scripts/Networking/
├── Core/
│   ├── SteamManager.cs              # Steam initialization, lobby management
│   ├── NetworkConnectionManager.cs   # Packet routing
│   ├── NetworkingConfiguration.cs    # ScriptableObject config
│   └── Abstractions/
│       ├── INetworkMessage.cs        # Message interface
│       ├── INetworkTransport.cs      # Transport abstraction
│       ├── NetworkHandlerRegistry.cs # Handler registration
│       └── NetworkMessageRegistry.cs # Message type registry
├── Messages/
│   ├── NetworkMessageType.cs         # Message type enum
│   ├── NetworkSerialization.cs       # Binary serialization helpers
│   └── SatelliteMessages.cs          # Game-specific messages
├── State/
│   ├── PlayerStateManager.cs         # Per-player state
│   ├── SatelliteStateManager.cs      # Satellite game state
│   └── SceneSyncManager.cs           # Scene transitions
├── Sync/
│   ├── NetworkSyncManager.cs         # Centralized sync dispatcher
│   ├── NetworkTransformSync.cs       # Transform sync
│   ├── NetworkPhysicsObject.cs       # Physics sync
│   └── NetworkInteractionState.cs    # Interaction events
├── Voice/
│   ├── VoiceChatP2P.cs              # Voice recording/sending
│   ├── VoiceSessionManager.cs        # Voice routing/gating
│   └── VoiceRemotePlayer.cs          # Voice playback
├── Identity/
│   └── NetworkIdentity.cs            # Network object identity
├── Debugging/
│   └── NetworkDebugOverlay.cs        # In-game debug UI
└── UI/
    ├── LobbyPlayersView.cs           # Lobby player list
    └── CreateLobbyButton.cs          # Lobby creation UI

Assets/Scripts/SceneManagers/
├── LobbyNetworkingManager.cs         # Lobby scene setup
├── GroundControlSceneManager.cs      # Ground Control scene setup
└── SpaceStationSceneManager.cs       # Space Station scene setup
```
