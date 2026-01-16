# Satellite Game Jam - Game Flow Architecture Document

**Version:** 1.0  
**Date:** January 10, 2026  
**Status:** Analysis & Verification

---

## Overview

This document outlines the game flow from initial connection through gameplay, explaining how each networking script contributes to the overall multiplayer experience. It serves as both a verification of current implementation and a blueprint for future enhancements.

---

## High-Level Game Flow

```
┌─────────────────────────────────────────────────────────────────┐
│ MATCHMAKING SCENE (Canvas UI Only)                               │
│ - No player models                                               │
│ - No voice chat                                                  │
│ - UI: Find Game / Create Lobby                                  │
│ - When game found → Load Lobby Scene                            │
└──────────────────┬──────────────────────────────────────────────┘
                   │
                   ↓
┌─────────────────────────────────────────────────────────────────┐
│ LOBBY SCENE (Canvas UI + Remote Player Models)                   │
│ - Remote player models SPAWNED                                  │
│ - Voice chat ENABLED (Push-to-talk)                             │
│ - UI: Ready button, Role selection, Chat                        │
│ - All players hear each other                                   │
│ - Remote player transforms synced                               │
│ - When Start Game clicked → Transition to Role Scenes            │
└──────────────────┬──────────────────────────────────────────────┘
                   │
         ┌─────────┴─────────┐
         ↓                   ↓
┌────────────────────┐  ┌─────────────────────┐
│ GROUND CONTROL     │  │ SPACE STATION       │
│ Scene              │  │ Scene               │
│                    │  │                     │
│ -Only Ground       │  │ -Only Space         │
│  Control players   │  │  Station players    │
│ -Remote players    │  │ -Remote players     │
│  FROM this scene   │  │  FROM this scene    │
│  spawned           │  │  spawned            │
│ -NO Space Station  │  │ -NO Ground Control  │
│  player models     │  │  player models      │
│ -Voice: Always     │  │ -Voice: Always      │
│  hear other Ground │  │  hear Ground        │
│  players           │  │  Control, hear      │
│ -Voice: Hear       │  │  Space peers        │
│  Space only when   │  │  within proximity   │
│  at console        │  │                     │
│ -Network sync:     │  │ -Network sync:      │
│  Only sync objects │  │  Only sync objects  │
│  in this scene     │  │  in this scene      │
└────────────────────┘  └─────────────────────┘
         │                   │
         └─────────┬─────────┘
                   ↓
        ┌──────────────────────┐
        │ End Game / Return to  │
        │ Lobby (via Lobby      │
        │ Owner action)         │
        └──────────────────────┘
```

---

## Scene Breakdown

### 1. **Matchmaking Scene**
**Status:** ✅ Canvas UI Only  
**Networking Involvement:** None (SteamManager handles lobby creation/joining)

**What happens:**
- User presses "Create Lobby" or "Find Game"
- SteamManager creates/joins a Lobby (via Steamworks API)
- Scene loads Lobby scene
- NO networked objects spawned yet

**Scripts:** None (gameplay mechanics handle this)

---

### 2. **Lobby Scene** 
**Status:** ✅ All players present, shared scene

**What happens:**
1. **Scene Load:**
   - `ObjectRegistryBridge.OnSceneLoaded()` is called
   - All `NetworkIdentity` objects in scene register themselves
   - Any `NetworkPhysicsObject` components start sending physics state

2. **Player Spawning:**
   - `NetworkConnectionManager.SpawnRemotePlayerFor(steamId)` is called
   - Creates player prefab instance
   - Tags with `NetworkIdentity` (OwnerSteamId = steamId)
   - Adds to `spawnedRemotePlayers` dictionary
   - **Transforms ARE synced** (via `NetworkTransformSync`)

3. **Voice Chat:**
   - `VoiceChatP2P` starts recording on push-to-talk
   - `VoiceSessionManager` applies gating rules:
     - Lobby role → All players hear all other players
   - `VoiceRemotePlayer` components attached to avatars
   - Remote voice played through spatial audio

4. **Player State:**
   - Local player sets scene: `PlayerStateManager.SetLocalPlayerScene(NetworkSceneId.Lobby)`
   - Local player role: `PlayerStateManager.SetLocalPlayerRole(PlayerRole.Lobby)`
   - `PlayerStateManager` broadcasts `PlayerSceneState` message (Channel 4)
   - Remote players receive and cache state

5. **UI Interactions:**
   - Ready button → `PlayerStateManager.SetLocalPlayerReady()`
   - Role selection → `PlayerStateManager.SetLocalPlayerRole(role)`
   - Start Game button → `SceneSyncManager.RequestStartGame()` (Host only)

---

### 3. **Ground Control Scene**
**Status:** ✅ Only Ground Control players present

**What happens:**

1. **Scene Transition:**
   - Host calls `SceneSyncManager.RequestStartGame()`
   - Host calls `BroadcastRoleBasedScenes()`
   - Ground Control players receive `PlayerSceneState` with `NetworkSceneId.GroundControl`
   - Players call `SceneManager.LoadScene("GroundControl")`

2. **Scene Load:**
   - `ObjectRegistryBridge.OnSceneLoaded()` called
   - All `NetworkIdentity` objects in scene register
   - Scene-specific objects with `NetworkTransformSync` / `NetworkPhysicsObject` components start syncing

3. **Player Spawning:**
   - `NetworkConnectionManager.SpawnRemotePlayerFor(steamId)` called for each Ground Control peer
   - `SpaceStationSceneManager.cs` NOT called (not in Space Station scene)
   - **Space Station player models NOT spawned**
   - **Space Station data NOT synced**

4. **Voice Chat:**
   - `VoiceSessionManager.ApplyVoiceGating()` runs each frame
   - **Ground players always hear other Ground players**
   - **Ground players ONLY hear Space players when at console**
   - `SetLocalPlayerAtConsole(true/false)` controls Space voice gating
   - Event: `GroundConsoleBlink.cs` or `TransmissionInteract.cs` calls this

5. **Network Synchronization:**
   - Only Ground Control scene objects synced
   - `NetworkPhysicsObject` sends position/rotation/velocity
   - `NetworkTransformSync` sends player positions
   - `NetworkInteractionState` sends pickup/drop events
   - **Space Station objects NOT sent to this scene**

6. **Game Logic:**
   - `GroundControlSceneManager.Start()` sets local player role/scene
   - Subscribes to `SatelliteStateManager` events for UI updates
   - UI shows satellite health, component damage, console states

---

### 4. **Space Station Scene**
**Status:** ✅ Only Space Station players present

**What happens:**

1. **Scene Transition:**
   - Same as Ground Control, but with `NetworkSceneId.SpaceStation`
   - Only Space Station role players load this scene

2. **Scene Load:**
   - `ObjectRegistryBridge.OnSceneLoaded()` called
   - All `NetworkIdentity` objects register
   - Space-specific networked objects start syncing

3. **Player Spawning:**
   - Remote player models created for other Space Station players only
   - **Ground Control player models NOT spawned**
   - **Ground Control data NOT synced**

4. **Voice Chat:**
   - `VoiceSessionManager.ApplyVoiceGating()` runs each frame
   - **Space players always hear Ground Control players**
   - **Space players hear other Space players within proximity radius**
   - Proximity checked via `IsWithinProximity()` using avatar positions

5. **Network Synchronization:**
   - Only Space Station scene objects synced
   - Same message types as Ground Control, but only for Space objects
   - **Ground Control objects NOT sent**

6. **Game Logic:**
   - `SpaceStationSceneManager.Start()` sets local player role/scene
   - Repair/damage functions control `SatelliteStateManager`

---

## Message Flow by Scene

### **Lobby Scene - Message Channels**

| Channel | Message Type | Direction | Sender | Receiver | Purpose |
|---------|-------------|-----------|--------|----------|---------|
| 0 | PlayerReady | Both | Player | All | Mark player ready |
| 0 | RoleAssign | Both | Player | All | Announce role selection |
| 1 | TransformSync | Continuous | Owner | All | Player position/rotation |
| 2 | VoiceData | Continuous | Talker | All | Voice audio packets |
| 3 | InteractionPickup | Event | Player | All | Pickup object |
| 3 | InteractionDrop | Event | Player | All | Drop object |
| 4 | PlayerSceneState | Event | Player | All | Announce player state |

---

### **Ground Control Scene - Message Channels**

| Channel | Message Type | Direction | Sender | Receiver | Purpose |
|---------|-------------|-----------|--------|----------|---------|
| 0 | (none) | - | - | - | Unused in-game |
| 1 | TransformSync | Continuous | Owner | All in scene | Ground players + Ground objects |
| 1 | PhysicsSync | Continuous | Authority | All in scene | Ground physics objects |
| 2 | VoiceData | Continuous | Talker | Gated | Only Ground + Space at console |
| 3 | InteractionPickup | Event | Player | All in scene | Ground object pickups |
| 3 | InteractionDrop | Event | Player | All in scene | Ground object drops |
| 4 | SatelliteHealth | Periodic | Authority | All | Satellite health delta |
| 4 | SatellitePartTransform | Periodic | Authority | All | Satellite part rotations |
| 4 | ConsoleState | Event | Player | All | Console screen updates |

**🚨 Important:** Ground Control players do NOT receive Space Station transform/physics messages. These are simply never sent to them.

---

### **Space Station Scene - Message Channels**

| Channel | Message Type | Direction | Sender | Receiver | Purpose |
|---------|-------------|-----------|--------|----------|---------|
| 0 | (none) | - | - | - | Unused in-game |
| 1 | TransformSync | Continuous | Owner | All in scene | Space players + Space objects |
| 1 | PhysicsSync | Continuous | Authority | All in scene | Space physics objects (satellite parts) |
| 2 | VoiceData | Continuous | Talker | Gated | Space players + Ground always |
| 3 | InteractionPickup | Event | Player | All in scene | Space object pickups |
| 3 | InteractionDrop | Event | Player | All in scene | Space object drops |
| 4 | SatelliteHealth | Periodic | Authority | All | Satellite state sync |
| 4 | SatellitePartTransform | Periodic | Authority | All | Satellite part positions |
| 4 | ConsoleState | Event | Player | All | Console state sync |

**🚨 Important:** Space players receive ALL messages because they need satellite state. Ground Control players also receive satellite state on Channel 4, but NOT the TransformSync/PhysicsSync for Space objects.

---

## Script Responsibilities by Phase

### **Phase 1: Lobby Initialization**

| Script | Method | Responsibility |
|--------|--------|-----------------|
| `SteamManager` | `OnLobbyCreated()` / `OnLobbyJoined()` | Create/join lobby, trigger Lobby scene load |
| `ObjectRegistryBridge` | `OnSceneLoaded()` | Track scene loads (logging) |
| `NetworkIdentity` | `Awake()` | Register all lobby objects in global registry |
| `NetworkConnectionManager` | (polling) | Poll packets from peers, route to handlers |
| `SceneSyncManager` | `RegisterHandlers()` | Register for scene change messages |
| `PlayerStateManager` | `RegisterHandlers()` | Register for player state messages |
| `VoiceSessionManager` | `OnPlayerJoined()` | Create voice remote player on peer join |

---

### **Phase 2: Lobby Gameplay**

| Script | Method | Responsibility |
|--------|--------|-----------------|
| `NetworkPhysicsObject` | `FixedUpdate()` + `OnReceivePhysicsSync()` | Send/receive avatar physics (if Rigidbody present) |
| `NetworkTransformSync` | `Update()` + `OnReceiveTransformSync()` | Send/receive avatar positions continuously |
| `VoiceChatP2P` | `Update()` | Capture voice on push-to-talk, poll voice packets |
| `VoiceSessionManager` | `ApplyVoiceGating()` | Lobby role → enable all AudioSources |
| `VoiceRemotePlayer` | `PlayAudio()` | Decompress and play received voice packets |
| `NetworkConnectionManager` | `SpawnRemotePlayerFor()` | Called when new player joins, spawns model |

---

### **Phase 3: Scene Transition (Start Game)**

| Script | Method | Responsibility |
|--------|--------|-----------------|
| `SceneSyncManager` | `RequestStartGame()` | Host only: broadcast role-based scene assignments |
| `PlayerStateManager` | `SetLocalPlayerScene()` | Local player: store scene change, broadcast state |
| `SceneSyncManager` | `OnPlayerSceneChanged()` | Listen to scene change events, trigger `SceneManager.LoadScene()` |
| `ObjectRegistryBridge` | `OnSceneUnloaded()` | Lobby objects are unregistered via `NetworkIdentity.OnDestroy()` |
| `NetworkConnectionManager` | `DespawnRemotePlayer()` | **Optional:** Clean up Lobby player models before loading new scene |

⚠️ **Current Issue:** Player models from Lobby are NOT cleaned up before loading game scenes. They remain as DontDestroyOnLoad objects. This could cause voice desync or UI confusion.

---

### **Phase 4: Ground Control Scene**

| Script | Method | Responsibility |
|--------|--------|-----------------|
| `ObjectRegistryBridge` | `OnSceneLoaded()` | Ground Control objects register in NetworkIdentity registry |
| `NetworkConnectionManager` | `SpawnRemotePlayerFor()` | Spawn Ground Control remote players only (not Space players) |
| `GroundControlSceneManager` | `Start()` | Set local player role/scene, subscribe to satellite events |
| `NetworkTransformSync` | `SendTransformState()` | Send Ground player positions only to peers in this scene |
| `NetworkPhysicsObject` | `SendPhysicsState()` | Send Ground object physics (tools, crates, etc.) |
| `VoiceSessionManager` | `ApplyVoiceGating()` | Ground rule: always hear Ground, hear Space only at console |
| `VoiceSessionManager` | `SetLocalPlayerAtConsole(true)` | Called by transmission console interaction, enables Space voice |
| `SatelliteStateManager` | `SendHealthUpdate()` | Authority broadcasts satellite health/damage state |

⚠️ **Current Issue:** No mechanism prevents Space Station objects from appearing in Ground Control scene registry if a player doesn't fully disconnect. The registry would need to be scene-scoped or explicitly cleaned.

---

### **Phase 5: Space Station Scene**

| Script | Method | Responsibility |
|--------|--------|-----------------|
| `ObjectRegistryBridge` | `OnSceneLoaded()` | Space objects register |
| `NetworkConnectionManager` | `SpawnRemotePlayerFor()` | Spawn Space remote players only |
| `SpaceStationSceneManager` | `Start()` | Set local player role/scene, provide repair/damage helpers |
| `NetworkTransformSync` | `SendTransformState()` | Send Space player positions |
| `NetworkPhysicsObject` | `SendPhysicsState()` | Send Space physics (satellite parts, objects) |
| `VoiceSessionManager` | `ApplyVoiceGating()` | Space rule: always hear Ground, hear Space within radius |
| `NetworkInteractionState` | `OnReceivePickup()` | Sync tool pickups/drops |
| `SatelliteStateManager` | `SendHealthUpdate()` | Sync satellite state to all peers |

⚠️ **Current Issue:** Satellite part transforms sent via `SatellitePartTransform` but Ground Control players receive them too. They can't see the parts visually but are processing unnecessary network data.

---

## Cross-Scene Voice Chat

**Current Implementation:**

```
VoiceSessionManager.ApplyVoiceGating() runs every frame and applies these rules:

┌─────────────────┐
│ LOCAL PLAYER    │
│ in Ground       │
│ Control         │
└────────┬────────┘
         │
         ├─→ Remote Ground Control player
         │   └─→ voicePlayer.GetComponent<AudioSource>().enabled = TRUE
         │
         └─→ Remote Space Station player
             └─→ IF local player at console:
                     voicePlayer.GetComponent<AudioSource>().enabled = TRUE
                 ELSE:
                     voicePlayer.GetComponent<AudioSource>().enabled = FALSE
```

**Voice Data Flow:**

1. **Space player talks** → `VoiceChatP2P` captures voice
2. **Sends via Channel 2** → Unreliable to all peers
3. **Ground player's VoiceRemotePlayer** receives decompressed audio
4. **VoiceSessionManager** enables/disables AudioSource each frame based on rules
5. **If Ground at console** → Audio plays
6. **If Ground not at console** → AudioSource disabled (silent)

**How to Signal "At Console":**

Ground Control console interaction (e.g., `TransmissionInteract.cs`):

```csharp
void OnConsoleInteract()
{
    VoiceSessionManager.Instance?.SetLocalPlayerAtConsole(true);
}

void OnConsoleExit()
{
    VoiceSessionManager.Instance?.SetLocalPlayerAtConsole(false);
}
```

---

## Redundancies & Issues

### 🔴 **CRITICAL**

1. **Player Model Cleanup on Scene Transition**
   - **Issue:** When transitioning from Lobby to game scenes, Lobby player models remain as DontDestroyOnLoad
   - **Impact:** Memory leak, confusing state, potential voice routing issues
   - **Fix:** Call `NetworkConnectionManager.DespawnRemotePlayer(steamId)` for all players when transitioning
   - **Location:** `SceneSyncManager.RequestStartGame()` should trigger cleanup

2. **NetworkIdentity ID Generation Not Synchronized**
   - **Issue:** Two clients can generate the same ID for newly spawned objects
   - **Impact:** Registry collisions, objects not syncing properly
   - **Fix:** Use host-based ID allocation (detailed in Production Readiness Report)

3. **No Late-Join Synchronization**
   - **Issue:** Players joining after lobby scene don't receive existing object state
   - **Impact:** Late joiners see incomplete world state
   - **Fix:** Implement snapshot/state transfer protocol

---

### 🟠 **MODERATE**

4. **Object Registry Not Scene-Scoped**
   - **Issue:** Ground Control and Space Station objects coexist in global registry
   - **Impact:** If sync logic breaks, Ground players might see Space object data
   - **Fix:** Separate registry by scene or add scene validation to lookup
   - **Location:** `NetworkIdentity.GetById()` should check scene context

5. **Satellite State Sent to All Scenes**
   - **Issue:** Ground Control players receive `SatellitePartTransform` messages they can't use
   - **Impact:** Unnecessary bandwidth, potential for buggy UI
   - **Fix:** Only send to players in Space Station scene (add scene filter)
   - **Location:** `SatelliteStateManager.SendPartTransform()` should check remote player scenes

6. **No Explicit "Player Left Scene" Notification**
   - **Issue:** Voice remote players in other scene not cleaned up on disconnect
   - **Impact:** Voice ghosts (receiving audio from players no longer in game)
   - **Fix:** On `OnPlayerLeft()`, call `VoiceSessionManager.UnregisterRemotePlayer()`
   - **Location:** `PlayerStateManager.OnPlayerLeft()` should trigger cleanup

7. **Multiple Console State Managers**
   - **Issue:** Both `SatelliteStateManager` and gameplay code track console state
   - **Impact:** Redundant messages, potential desync
   - **Fix:** Centralize in `SatelliteStateManager` or create `ConsoleStateManager`
   - **Location:** `ConsoleState` message handling

---

### 🟡 **MINOR**

8. **Magic Numbers in Scene Names**
   - **Issue:** Scene names hardcoded in multiple locations
   - **Impact:** Easy to break if scene is renamed
   - **Fix:** Create `SceneNames` constant class
   - **Location:** `SceneSyncManager`, `GroundControlSceneManager`, etc.

9. **No Clear "Game Started" Signal**
   - **Issue:** Hard to know when transition from Lobby to game is complete
   - **Impact:** UI can get out of sync with network state
   - **Fix:** Add `OnGameStarted` event to `SceneSyncManager`
   - **Location:** After all players acknowledge scene load

10. **VoiceSessionManager Uses `FindGameObjectWithTag("Player")`**
    - **Issue:** Magic tag, assumes only one local player
    - **Impact:** Fragile, doesn't scale to multiple local players (spectators, etc.)
    - **Fix:** Provide explicit local player reference setter
    - **Location:** `VoiceSessionManager.FindLocalPlayerAvatar()`

---

## Developer Experience Improvements

### 1. **Clear Prefab Setup Instructions**

Create a `PREFAB_SETUP.md` document explaining:

**Player Prefab Requirements:**
```
Player_Remote (prefab)
├── NetworkIdentity (required)
│   ├── networkId: 0 (auto-generated)
│   └── OwnerSteamId: Set at runtime
├── NetworkTransformSync (recommended for avatars)
│   ├── syncPosition: TRUE
│   ├── syncRotation: TRUE
│   └── sendRate: 20 Hz
├── Animator (for animation state)
├── AudioSource (for voice)
│   └── spatialBlend: 1.0 (3D audio)
└── Child: VoiceAudioSource (or use parent)
```

**Networked Object Prefab Requirements:**
```
Tool (example: Wrench)
├── NetworkIdentity
├── NetworkInteractionState (if pickupable)
├── Rigidbody (if physics)
│   └── Constraints: None initially
├── Collider
└── Optional: NetworkPhysicsObject (if authority-based)
```

---

### 2. **Scene Setup Instructions**

Create `SCENE_SETUP.md` with step-by-step guide:

**Lobby Scene Checklist:**
- [ ] Canvas (UI)
- [ ] NetworkConnectionManager prefab (already DontDestroyOnLoad)
- [ ] Lobby player model spawned dynamically
- [ ] Voice chat enabled via VoiceSessionManager

**Ground Control Scene Checklist:**
- [ ] GroundControlSceneManager on empty GameObject
- [ ] All interactive objects have NetworkIdentity
- [ ] Transmission console calls `VoiceSessionManager.SetLocalPlayerAtConsole()`
- [ ] Satellite visual model (non-networked reference)

**Space Station Scene Checklist:**
- [ ] SpaceStationSceneManager on empty GameObject
- [ ] Satellite parts have NetworkPhysicsObject (for authority-based physics)
- [ ] Tools/objects have NetworkInteractionState
- [ ] Sensor to detect local player proximity to other players

---

### 3. **Inspector Configuration Template**

Create a scriptable object `NetworkingConfiguration` with public fields:

```csharp
public class NetworkingConfiguration : ScriptableObject
{
    [Header("Lobby")]
    public string lobbySceneName = "Lobby";
    public GameObject playerPrefab;
    public bool autoSpawnPlayers = true;
    
    [Header("Game Scenes")]
    public string groundControlSceneName = "GroundControl";
    public string spaceStationSceneName = "SpaceStation";
    
    [Header("Voice Chat")]
    public float spaceProximityRadius = 20f;
    public Key pushToTalkKey = Key.V;
    public int voiceChannel = 2;
    
    [Header("Sync Rates")]
    public float playerTransformSyncRate = 20f;
    public float physicsObjectSyncRate = 10f;
    public float satelliteStateSendRate = 1f;
    
    [Header("Debugging")]
    public bool enableNetworkLogs = false;
    public bool showDebugVisuals = true;
}
```

Load in managers:
```csharp
public class SceneSyncManager : MonoBehaviour
{
    [SerializeField] private NetworkingConfiguration config;
    
    private string lobbySceneName => config.lobbySceneName;
    // etc...
}
```

---

### 4. **Visual Debugging Tools**

Add a `NetworkDebugger` MonoBehaviour that shows:

**Inspector Overlay (when enabled):**
```
┌────────────────────────────────────────┐
│ NETWORKING DEBUG                       │
├────────────────────────────────────────┤
│ Connection Status:     Connected        │
│ Local SteamId:        76561234567890   │
│ Local Role:           SpaceStation      │
│ Local Scene:          SpaceStation      │
│ Players in lobby:     2                 │
│                                        │
│ REMOTE PLAYERS:                        │
│ ├─ 76561234567891 (Space)             │
│ │  ├─ Scene: SpaceStation              │
│ │  ├─ Latency: 45ms                    │
│ │  ├─ Voice: [ENABLED] 🎤              │
│ │  └─ Models Spawned: 1                │
│ └─ 76561234567892 (Ground)            │
│    ├─ Scene: GroundControl             │
│    ├─ Latency: 52ms                    │
│    ├─ Voice: [DISABLED]                │
│    └─ Models Spawned: 1                │
│                                        │
│ PACKETS (last 10s):                   │
│ ├─ Sent: 2,345                        │
│ ├─ Received: 2,289                    │
│ └─ Lost: 0.2%                         │
│                                        │
│ SCENES:                               │
│ ├─ Active: SpaceStation               │
│ ├─ Objects in Registry: 8             │
│ └─ DontDestroyOnLoad: 12              │
└────────────────────────────────────────┘
```

**Gizmo Visualization:**
- Draw radius around local player (voice proximity for Space)
- Show position of remote players with SteamId labels
- Red X for players that should exist but aren't spawned

---

### 5. **Scene-Specific Networking Managers**

Instead of global managers, create scene-specific wrappers:

```csharp
/// <summary>
/// Ground Control specific network behavior.
/// Handles voice gating and player spawning for Ground Control scene only.
/// </summary>
public class GroundControlNetworking : MonoBehaviour
{
    private void Start()
    {
        // Only spawn Ground Control players
        foreach (var member in SteamManager.Instance.currentLobby.Members)
        {
            if (GetPlayerScene(member.Id) == NetworkSceneId.GroundControl)
            {
                NetworkConnectionManager.Instance.SpawnRemotePlayerFor(member.Id);
            }
        }
        
        // Subscribe to voice console interaction
        ConsoleUI.Instance.OnConsoleInteract += OnConsoleInteract;
    }
    
    private void OnConsoleInteract(bool isInteracting)
    {
        VoiceSessionManager.Instance?.SetLocalPlayerAtConsole(isInteracting);
    }
}
```

This makes it clearer which systems apply to which scene.

---

### 6. **Loose Coupling Between Game Logic and Networking**

**Current Issue:** Game code calls networking directly
```csharp
// In GroundControlSceneManager.cs
PlayerStateManager.Instance.SetLocalPlayerRole(PlayerRole.GroundControl);
```

**Better:** Event-based interface

```csharp
/// <summary>
/// Provides a high-level game flow API that internally uses networking.
/// Decouples game logic from network implementation.
/// </summary>
public class GameFlowManager : MonoBehaviour
{
    public event Action<NetworkSceneId> OnSceneLoaded;
    public event Action<SteamId> OnRemotePlayerSpawned;
    
    public void StartGameSession(NetworkSceneId sceneId)
    {
        // Internal implementation uses SceneSyncManager, etc.
        // Game code just calls this method
    }
    
    public void OnLocalPlayerInteractingWithConsole(bool isInteracting)
    {
        // Internal implementation uses VoiceSessionManager
    }
}
```

Game code now calls:
```csharp
GameFlowManager.Instance.OnLocalPlayerInteractingWithConsole(true);
```

Instead of:
```csharp
VoiceSessionManager.Instance?.SetLocalPlayerAtConsole(true);
PlayerStateManager.Instance.SetLocalPlayerRole(PlayerRole.GroundControl);
SceneSyncManager.Instance.RequestStartGame();
```

**Benefits:**
- Game code doesn't know about networking internals
- Easier to test (mock GameFlowManager)
- Easier to refactor networking without breaking game code
- Single point of contact for game→network communication

---

### 7. **Network Event Bus**

Create a centralized event system:

```csharp
public class NetworkEventBus : MonoBehaviour
{
    // Scene events
    public event Action<NetworkSceneId> OnLocalSceneChanged;
    public event Action<SteamId, NetworkSceneId> OnRemoteSceneChanged;
    
    // Player events
    public event Action<SteamId> OnRemotePlayerSpawned;
    public event Action<SteamId> OnRemotePlayerDespawned;
    public event Action<SteamId> OnPlayerDisconnected;
    
    // Voice events
    public event Action<SteamId> OnVoiceStarted;
    public event Action<SteamId> OnVoiceEnded;
    
    // State events
    public event Action<float> OnSatelliteHealthChanged;
    public event Action<uint> OnComponentDamaged;
}
```

All managers emit events to this bus, and game code subscribes:

```csharp
public class GameUI : MonoBehaviour
{
    private void Start()
    {
        NetworkEventBus.Instance.OnSatelliteHealthChanged += UpdateHealthBar;
        NetworkEventBus.Instance.OnRemotePlayerSpawned += ShowPlayerJoinedNotification;
    }
}
```

**Benefit:** Clear, decoupled event flow visible in one place.

---

## Summary of Verification

### ✅ **Correctly Implemented**

- Lobby scene transitions properly with all players
- Voice chat rules correctly implemented per role/scene
- Remote player models spawned only in shared scenes
- NetworkIdentity registry used for object lookups
- Scene unload properly triggers NetworkIdentity cleanup
- InteractionState uses reliable channel for important events
- PlayerStateManager tracks per-player scene/role state

### ⚠️ **Implemented but Needs Improvement**

- ID generation not synchronized (race condition risk)
- Player models not cleaned on scene transition
- Object registry not scene-scoped (potential cross-scene contamination)
- Satellite state sent to all scenes (bandwidth waste)
- No explicit player departure cleanup

### ❌ **Not Yet Implemented**

- Late-join synchronization
- Connection state machine
- Heartbeat/timeout detection
- Rate limiting and anti-cheat
- Packet batching and compression
- Comprehensive error handling
- Network debugging tools
- Centralized game flow API
- Network event bus

---

## Recommended Next Steps

1. **Immediate (This Week):**
   - Add player model cleanup on scene transition
   - Add scene-specific filters to message handlers
   - Create NetworkingConfiguration scriptable object

2. **Short Term (Next 2 Weeks):**
   - Implement GameFlowManager abstraction layer
   - Create NetworkDebugger visual tool
   - Fix ID generation race condition

3. **Medium Term (Next Month):**
   - Implement late-join synchronization
   - Add connection state machine
   - Create comprehensive error handling

4. **Documentation:**
   - Write PREFAB_SETUP.md
   - Write SCENE_SETUP.md
   - Create developer quickstart guide

