# Network Architecture Visual Guide

## Game Flow State Diagram

```
                    ┌─────────────────────┐
                    │  MATCHMAKING SCENE  │
                    │  (Canvas UI Only)   │
                    │  - No networking    │
                    │  - Player finds/    │
                    │    creates lobby    │
                    └──────────┬──────────┘
                               │
                        [Lobby Created]
                               │
                               ↓
                    ┌─────────────────────┐
                    │   LOBBY SCENE       │
                    │  (Shared by All)    │
                    ├─────────────────────┤
                    │ ✅ Remote players   │
                    │    spawned          │
                    │ ✅ Voice enabled    │
                    │    (Push-to-talk)   │
                    │ ✅ Transforms sync  │
                    │ ✅ All hear all     │
                    │ ⏳ Waiting for      │
                    │    Start Game       │
                    └──────────┬──────────┘
                               │
                    [Host clicks Start]
                               │
                ┌──────────────┴──────────────┐
                │                             │
                ↓                             ↓
    ┌──────────────────────┐    ┌──────────────────────┐
    │ GROUND CONTROL       │    │ SPACE STATION        │
    │ (Ground players)     │    │ (Space players)      │
    ├──────────────────────┤    ├──────────────────────┤
    │ ✅ Lobby models      │    │ ✅ Lobby models      │
    │    CLEANED UP        │    │    CLEANED UP        │
    │ ✅ Ground models     │    │ ✅ Space models      │
    │    spawned           │    │    spawned           │
    │ ❌ Space models      │    │ ❌ Ground models     │
    │    NOT spawned       │    │    NOT spawned       │
    │ ❌ Space transforms  │    │ ❌ Ground transforms │
    │    NOT synced        │    │    NOT synced        │
    │ ✅ Voice: Always     │    │ ✅ Voice: Always     │
    │    hear Ground       │    │    hear Ground       │
    │ ✅ Voice: Hear       │    │ ✅ Voice: Hear       │
    │    Space only at     │    │    Space within      │
    │    console           │    │    proximity         │
    └──────────┬───────────┘    └──────────┬───────────┘
               │                           │
               └───────────────┬───────────┘
                               │
                    [End Game clicked]
                               │
                               ↓
                    ┌─────────────────────┐
                    │   Return to Lobby   │
                    │  (Restart cycle)    │
                    └─────────────────────┘
```

---

## Network Message Flow (Sequence Diagram)

### Lobby Scene - All Players Connected

```
Player A                Network                Player B
(Local)              Channel 0,1,2,3          (Remote)
   │                     │                        │
   ├─ PlayerReady ────→─ NCM ─────────────────→ Player B
   │  (Ch 0, Rel)       └──                      (ProcessMsg)
   │                                             │
   ├─ TransformSync ────→─ NCM ────────────────→ Player B
   │  (Ch 1, Unrel)      └──                      (Interpolate)
   │                                             │
   ├─ VoiceData ────────→─ NCM ────────────────→ Player B
   │  (Ch 2, Unrel)      └──                      (PlayAudio)
   │                                             │
   │← TransformSync ────── NCM ────────────────← Player B
   │  (interpolate)       └──                      (Sync)
   │                                             │
   │← VoiceData ────────── NCM ────────────────← Player B
   │  (play)              └──                      (Record)
   │                                             │
```

### Scene Transition - All Players

```
All Players           SceneSyncManager          All Players
    │                       │                       │
    ├─ All ready ───────→ Manager ──────────→ PlayerStateManager
    │  (PlayerReady)       │                       │
    │                      │                    (Update state)
    │                      ├─ SceneChange ───→     │
    │                      │ Assignment             │
    │                      │ (Ch 4, Rel)           │
    │                      │                    (Load scene)
    │                      │←─ Ack ───────────     │
    │                      │  (Ch 0, Rel)          │
    │                      │                       │
    │                      ├─ All Acked? ────→ Process complete
    │                      │                       │
    └──────────────────────────────────────────────┘
```

### Ground Control Scene - Separate Message Streams

```
Ground Control Players          Network              Ground Control Players
      │                           │                          │
      ├─ TransformSync ──────→ Channel 1 ─────────→ Receive & Interpolate
      │  (Position)             (10Hz, Unrel)             │
      │                                                    │
      ├─ PhysicsSync ────────→ Channel 1 ─────────→ Receive & Interpolate
      │  (Object physics)        (10Hz, Unrel)             │
      │                                                    │
      ├─ VoiceData ──────────→ Channel 2 ─────────→ Gate by console state
      │  (Audio packets)         (Variable)              │
      │  └→ If NOT at console: AudioSource disabled
      │                                                    │
      ├─ Interaction events ──→ Channel 3 ─────────→ Pickup/Drop/Use
      │  (Pickup/Drop)           (Reliable)                │
      │                                                    │
      └─ SatelliteHealth ─────→ Channel 4 ─────────→ Update UI / State
         (Broadcast)             (1Hz, Reliable)         │

🚨 Space Station objects NOT SENT to Ground players
🚨 Space Station messages NOT received
```

---

## Voice Chat Gating Decision Tree

```
Local Player in Lobby?
├─ YES
│  └─→ Hear ALL remote players
│      [All AudioSources enabled]
│
└─ NO, Player in Ground Control
   │
   └─ Hearing a Ground Control player?
      ├─ YES
      │  └─→ HEAR
      │      [AudioSource enabled]
      │
      └─ NO, Hearing a Space Station player?
         │
         └─ Local player at console?
            ├─ YES
            │  └─→ HEAR
            │      [AudioSource enabled]
            │
            └─ NO
               └─→ MUTE
                   [AudioSource disabled]

└─ NO, Player in Space Station
   │
   └─ Hearing a Ground Control player?
      ├─ YES
      │  └─→ HEAR (always)
      │      [AudioSource enabled]
      │
      └─ NO, Hearing a Space Station player?
         │
         └─ Within proximity radius?
            ├─ YES
            │  └─→ HEAR
            │      [AudioSource enabled]
            │
            └─ NO
               └─→ MUTE
                   [AudioSource disabled]
```

---

## Object Synchronization Boundaries

```
                         ┌─────────────────┐
                         │  Global Objects │
                         │  (DontDestroy)  │
                         │                 │
                         │ ✓ NetworkIdentity│
                         │   Registry      │
                         │ ✓ PlayerState   │
                         │   Manager       │
                         │ ✓ VoiceSession  │
                         │   Manager       │
                         │ ✓ SatelliteState│
                         │   (synced to    │
                         │    all scenes)  │
                         └────────┬────────┘
                                  │
                 ┌────────────────┴────────────────┐
                 │                                 │
                 ↓                                 ↓
    ┌──────────────────────┐      ┌──────────────────────┐
    │ GROUND CONTROL       │      │ SPACE STATION        │
    │ SCENE OBJECTS        │      │ SCENE OBJECTS        │
    │                      │      │                      │
    │ ✓ Tools (sync via    │      │ ✓ Tools (sync via    │
    │   NetworkTransform   │      │   NetworkTransform   │
    │   Sync)              │      │   Sync)              │
    │ ✓ Crates (physics)   │      │ ✓ Satellite parts    │
    │ ✓ Consoles (state)   │      │ ✓ Equipment          │
    │                      │      │                      │
    │ ❌ NO Space objects  │      │ ❌ NO Ground objects │
    │ ❌ NO Space player   │      │ ❌ NO Ground player  │
    │    models            │      │    models            │
    │ ❌ NO Space position │      │ ❌ NO Ground position│
    │    data              │      │    data              │
    │                      │      │                      │
    │ 🔊 Voice:           │      │ 🔊 Voice:           │
    │ Ground ↔ Ground ✓   │      │ Space ↔ Space ✓     │
    │ Ground ↔ Space      │      │ (if in proximity)    │
    │ (console only) ✓    │      │                      │
    │                      │      │ Ground ↔ Space ✓    │
    │                      │      │ (always)             │
    └──────────────────────┘      └──────────────────────┘
         (Player A)                    (Player B)
         (Player C)                    (Player D)
```

---

## Network Manager Responsibility Diagram

```
┌──────────────────────────────────────────────────────────┐
│                    SteamManager                           │
│              (Lobby creation/joining)                     │
│  Emits: RemotePlayerJoined, RemotePlayerLeft events     │
└────────────────────────┬─────────────────────────────────┘
                         │
        ┌────────────────┼────────────────┐
        │                │                │
        ↓                ↓                ↓
┌─────────────────┐ ┌──────────────────┐ ┌──────────────────┐
│ NetworkConnection│ │PlayerStateManager│ │VoiceSessionMgr   │
│Manager          │ │                  │ │                  │
├─────────────────┤ ├──────────────────┤ ├──────────────────┤
│ Routes packets  │ │Tracks per-player │ │Applies voice     │
│ Spawns/despawns │ │scene, role,      │ │gating rules      │
│ players         │ │ready state       │ │Manages AudioSrc  │
│ Polls channels  │ │Broadcasts state  │ │Handles proximity │
│ Registers       │ │changes           │ │Attaches Voice    │
│ handlers        │ │                  │ │RemotePlayer      │
└────────┬────────┘ └────────┬─────────┘ └────────┬─────────┘
         │                   │                     │
         │ Sends packets     │ Sends state         │ Registers
         │ to Steam P2P      │ changes on          │ avatar for
         │                   │ Channel 4           │ voice
         └───────────────┬───────────────────────┘
                         │
                         ↓
                ┌────────────────┐
                │ Remote Peers   │
                │  (Steam P2P)   │
                └────────────────┘
                         │
    ┌────────────────────┼────────────────────┐
    │                    │                    │
    ↓                    ↓                    ↓
Ground Control       Space Station         Lobby
Player receives      Player receives       Player receives
Ground transforms    Space transforms      All transforms
& voice              & voice               & voice
```

---

## Implementation Dependency Graph

```
Production Readiness
├─ Error Handling
├─ Connection State Machine
├─ Late-Join Sync
└─ (See NetworkingProductionReadinessReport.md)

Developer Experience
├─ NetworkingConfiguration (foundational)
│  ├─ LobbyNetworkingManager
│  ├─ GroundControlNetworkingManager
│  └─ SpaceStationNetworkingManager
├─ GameFlowManager (abstraction layer)
├─ NetworkDebugOverlay (debugging)
└─ Setup Documentation

Game-Specific Implementation
├─ Prefab Setup (depends on config)
├─ Scene Setup (depends on scene managers)
├─ Console Interaction (depends on GameFlowManager API)
└─ Satellite State (depends on managers)
```

---

## Scene Load/Unload Sequence

```
┌─────────────────────────────────────────────────────────┐
│ START GAME (Host clicks button)                         │
└────────────────────┬────────────────────────────────────┘
                     │
                     ↓
   ┌────────────────────────────────────┐
   │ SceneSyncManager.RequestStartGame()│
   │ 1. Get role-based assignments      │
   │ 2. Clean up lobby models ⚠️        │
   │ 3. Broadcast assignments           │
   │ 4. Begin ack window (10 sec)       │
   └────────────────────┬───────────────┘
                        │
        ┌───────────────┼────────────────┐
        ↓               ↓                ↓
   Ground Player    Space Player    Ground Player
        │               │                │
   Receives:        Receives:        Receives:
   GroundControl    SpaceStation     GroundControl
   assignment       assignment       assignment
        │               │                │
        ↓               ↓                ↓
   PlayerStateManager.SetLocalPlayerScene()
   │
   ├─ OnPlayerSceneChanged event fired
   │
   └─ SceneSyncManager.OnPlayerSceneChanged()
      │
      └─ SceneManager.LoadScene("GroundControl") etc.
         │
         ├─ [OLD SCENE UNLOADS]
         │  │
         │  └─ NetworkIdentity objects OnDestroy()
         │     └─ Unregister from global registry
         │
         └─ [NEW SCENE LOADS]
            │
            ├─ ObjectRegistryBridge.OnSceneLoaded()
            │  └─ Log: "Scene loaded: GroundControl"
            │
            ├─ NetworkIdentity objects Awake()
            │  └─ Register in global registry
            │
            ├─ NetworkTransformSync components Register handlers
            │
            ├─ GroundControlNetworkingManager.Start()
            │  ├─ Set role: PlayerRole.GroundControl
            │  ├─ Set scene: NetworkSceneId.GroundControl
            │  └─ Spawn remote Ground players only
            │
            ├─ VoiceSessionManager.ApplyVoiceGating()
            │  └─ Ground rule: Hear Ground always, Space at console
            │
            └─ OnSceneLoaded event → Send Ack to host
               │
               └─ SceneSyncManager receives acks from all peers
                  │
                  └─ All acks received → Ack timeout cancelled
                     │
                     └─ Game can begin safely
```

---

## Message Channel Assignment Reference

| Channel | Type | Name | Use | Frequency | Example Messages |
|---------|------|------|-----|-----------|------------------|
| 0 | Reliable | Control | Lobby control, ready states | Event-based | PlayerReady, RoleAssign |
| 1 | Unreliable | High-Freq | Fast-moving state | 10-20 Hz | TransformSync, PhysicsSync |
| 2 | Unreliable | Voice | Voice audio | Variable | VoiceData |
| 3 | Reliable | Interactions | Critical events | Event-based | Pickup, Drop, Use |
| 4 | Reliable | Low-Freq | Periodic updates | 0.5-2 Hz | PlayerSceneState, SatelliteHealth |

---

## Recommended File Organization

```
Assets/
├── Scripts/
│   ├── Networking/
│   │   ├── Core/
│   │   │   ├── NetworkConnectionManager.cs
│   │   │   ├── SteamManager.cs
│   │   │   ├── NetworkingConfiguration.cs ⭐ NEW
│   │   │   └── ObjectRegistryBridge.cs
│   │   │
│   │   ├── Identity/
│   │   │   └── NetworkIdentity.cs
│   │   │
│   │   ├── State/
│   │   │   ├── PlayerStateManager.cs
│   │   │   ├── SatelliteStateManager.cs
│   │   │   └── SceneSyncManager.cs
│   │   │
│   │   ├── Sync/
│   │   │   ├── NetworkTransformSync.cs
│   │   │   ├── NetworkPhysicsObject.cs
│   │   │   └── NetworkInteractionState.cs
│   │   │
│   │   ├── Voice/
│   │   │   ├── VoiceChatP2P.cs
│   │   │   ├── VoiceRemotePlayer.cs
│   │   │   └── VoiceSessionManager.cs
│   │   │
│   │   ├── Messages/
│   │   │   ├── NetworkMessageType.cs
│   │   │   └── NetworkSerialization.cs
│   │   │
│   │   ├── SceneSpecific/ ⭐ NEW
│   │   │   ├── LobbyNetworkingManager.cs
│   │   │   ├── GroundControlNetworkingManager.cs
│   │   │   └── SpaceStationNetworkingManager.cs
│   │   │
│   │   ├── Debugging/ ⭐ NEW
│   │   │   └── NetworkDebugOverlay.cs
│   │   │
│   │   └── GameFlowManager.cs ⭐ NEW
│   │
│   └── [Game-specific scripts]
│
└── Documents/
    ├── GameFlowArchitecture.md ⭐ NEW
    ├── DeveloperExperienceImprovements.md ⭐ NEW
    ├── NetworkingProductionReadinessReport.md ✓ EXISTING
    ├── NetworkingAnalysisSummary.md ⭐ NEW
    ├── PREFAB_SETUP.md ⭐ NEW
    └── SCENE_SETUP.md ⭐ NEW
```

This comprehensive visual guide complements the detailed documentation and provides quick reference for understanding how all systems interact.
