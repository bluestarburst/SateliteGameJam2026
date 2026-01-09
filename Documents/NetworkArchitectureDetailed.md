# Networking Detailed Plan

## Goals
- Keep Steam P2P as the transport; SteamManager remains the authoritative peer list.
- Preserve existing channels and message prefixes; add control/state surfaces without breaking high-frequency sync.
- Handle scene transitions, role-based permissions, and voice routing/gating with explicit managers.
- Keep managers stateless where possible; cache state only when necessary for UX (UI, late join, voice routing).

## Manager Layer (DontDestroyOnLoad)
- **NetworkConnectionManager** (existing): Router only. Add polling for Channel 4 when introduced.
- **PlayerStateManager** (NEW): Tracks per-player scene, role, and presence. Emits `OnPlayerSceneChanged`, `OnRoleChanged`, `OnPlayerReady`.
- **SceneSyncManager** (NEW): Coordinates scene change intents/acks, load barriers, and late-join state pulls. Talks to PlayerStateManager.
- **SatelliteStateManager** (NEW): Authoritative cache of satellite health/components/console states. Publishes deltas to UI/consoles.
- **VoiceSessionManager** (NEW): Drives VoiceChatP2P. Resolves/attaches VoiceRemotePlayer to avatars/proxies; applies role, scene, and proximity rules; toggles AudioSource enable/volume.
- **ObjectRegistryBridge** (NEW): Bridges NetworkIdentity registry across scene unload/load (re-registers on Awake/OnDestroy, cleans up on scene unload).

## Channels
- **Channel 0**: Control/lobby (ready, scene change intent/ack, role assignment, voice route overrides if needed).
- **Channel 1**: High-frequency transforms/physics (TransformSync, PhysicsSync).
- **Channel 2**: Voice data (VoiceData). VoiceSessionManager interprets per-sender gating.
- **Channel 3**: Reliable interactions/events (InteractionPickup/Drop/Use, AuthorityRequest/Grant).
- **Channel 4** (NEW): Low-frequency state snapshots/deltas (player presence/scene/role, satellite status, console states, optional voice route hints).

## Message Types (proposed additions)
Group additions to NetworkMessageType with ranges:
- **Control (Channel 0)**
  - `PlayerReady = 0x01`           // [SteamId(8)]
  - `SceneChangeRequest = 0x02`    // [SteamId(8)][SceneId(2)][Timestamp(4)]
  - `SceneChangeAcknowledge = 0x03`// [SteamId(8)][SceneId(2)]
  - `RoleAssign = 0x04`            // [SteamId(8)][Role(byte)]
- **Presence/State (Channel 4)**
  - `PlayerSceneState = 0x40`      // [SteamId(8)][SceneId(2)][Role(byte)][Timestamp(4)]
  - `PlayerProximityHint = 0x41`   // [SteamId(8)][SceneId(2)][Pos(12)] (optional for voice distance)
  - `SatelliteHealth = 0x42`       // [Health(float)][DamageBits(uint)]
  - `SatellitePartTransform = 0x43`// [PartId(uint)][Pos(12)][Rot(12)]
  - `ConsoleState = 0x44`          // [ConsoleId(uint)][StateByte][Payload(N)]
- **Voice Control (optional)**
  - Keep `VoiceData = 0x00` on Channel 2. If needed, add `VoiceRouteHint = 0x45` on Channel 4 for per-sender mute/unmute instructions.

## Scene & Role Flow
- On scene load, SceneSyncManager sends `PlayerSceneState`; peers update lookup. VoiceSessionManager uses this to decide who should be audible.
- Ground role: Only hear space when at console; always hear other ground. VoiceSessionManager toggles AudioSource enabled based on console interaction events.
- Space role: Always hear ground; hear space peers if within radius (use proximity from TransformSync or PlayerProximityHint).
- On leave/unload: send `PlayerSceneState` with SceneId = None; VoiceSessionManager destroys VoiceRemotePlayer for that SteamId.

## Satellite State Flow
- SatelliteStateManager owns health/components/console cache (authoritative or host-authoritative if needed).
- Publishes deltas via Channel 4 messages; UI subscribes to events.
- Part transforms: if not handled by existing TransformSync/PhysicsSync, send `SatellitePartTransform` at low rate or on change.

## Voice Handling Details
- VoiceChatP2P keeps capturing/sending on Channel 2 with prepended sender SteamId.
- VoiceSessionManager resolves avatar transform for positional audio; falls back to proxy GameObject if avatar not present yet.
- Applies gating: console-use flag for ground, proximity for space-space, scene filter to avoid cross-scene bleed.

## Serialization Notes
- Reuse NetworkSerialization helpers.
- SceneId: ushort; Role: byte enum; DamageBits: uint bitfield for component flags.
- Keep packets compact; avoid allocations in hot paths.

## Directory Restructure (Assets/Scripts/Networking)
- Core/
  - NetworkConnectionManager.cs
  - SteamManager.cs (or stay at root if shared)
  - ObjectRegistryBridge.cs (new)
- Identity/
  - NetworkIdentity.cs
- Sync/
  - NetworkTransformSync.cs
  - NetworkPhysicsObject.cs
  - NetworkInteractionState.cs
- Voice/
  - VoiceChatP2P.cs
  - VoiceRemotePlayer.cs
  - VoiceSessionManager.cs (new)
- State/
  - PlayerStateManager.cs (new)
  - SceneSyncManager.cs (new)
  - SatelliteStateManager.cs (new)
- Messages/
  - NetworkMessageType.cs (extended)
  - NetworkSerialization.cs
- UI/
  - LobbyPlayersView.cs
  - UnrankedLobbiesView.cs
  - CreateLobbyButton.cs

(Existing files can be moved into these subfolders; update namespaces accordingly.)

## Implementation Steps
1) Extend NetworkMessageType with control/state entries; add Channel 4 polling in NetworkConnectionManager.
2) Add PlayerStateManager + SceneSyncManager: send/receive PlayerSceneState, SceneChangeRequest/Ack, RoleAssign.
3) Add SatelliteStateManager: cache health/components/console states; send deltas on change.
4) Add VoiceSessionManager: tie into VoiceChatP2P receive path; resolve avatars; enforce role/scene/proximity rules by toggling AudioSource.
5) Add ObjectRegistryBridge: ensure NetworkIdentity registry stays consistent across scene transitions.
6) Restructure folder layout; adjust namespaces/usings; fix assembly definitions if present.
7) Wire gameplay hooks: console interaction events to VoiceSessionManager; scene load/unload events to SceneSyncManager; UI to SatelliteStateManager events.

## Open Questions
- Authority for satellite state: fully peer-to-peer or designate host/owner? (Recommend host/lowest SteamId.)
- SceneId source: Unity build index vs enum? (Prefer enum for stability.)
- Do we need late-join full snapshot messages for satellite/components? (Add if playtests need it.)
