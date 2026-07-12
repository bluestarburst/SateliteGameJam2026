# Scene Setup

Use one prefab for all persistent networking: `Assets/Prefabs/Networking/SteamPack.prefab`.
Place one unmodified instance at the root of every flow scene. Do not create scene-specific
variants or add networking singletons directly to a scene.

`SteamPack` keeps the first runtime instance with `DontDestroyOnLoad`. When a player follows
the normal flow, the Matchmaking instance survives and later scene copies destroy themselves.
When Play starts directly in Lobby, BaseStation, or Satellite, that scene's copy becomes the
persistent instance instead. This is intentional and makes direct-scene debugging work.

## Shared Configuration

Configure the prefab once through its `SteamPackConfig` component:

- `Networking Configuration`: `Assets/Resources/NetworkingConfig.asset`
- `Role Visual Profile`: `Assets/Resources/RoleVisualProfile.asset`

`NetworkingConfig.asset` owns the Steam App ID, remote-player prefab, voice options, debug
options, and the `GameFlowDefinition`. `GameFlowDefinition.asset` is the only place that maps
scene names, flow order, roles, and direct-scene development assignments.

After changing either reference, select the prefab and use **SteamPack Config > Apply To
Attached Managers**. Do not set those internal references on individual manager components.

## SteamPack Components

Keep these components on the one prefab:

- `SteamPackConfig` - single Inspector surface for shared assets.
- `SteamManager` - Steam initialization, lobby ownership, and P2P transport.
- `NetworkConnectionManager` - packet routing and remote player lifecycle.
- `NetworkSyncManager` - transform and interaction synchronization.
- `PlayerStateManager` - replicated player role, readiness, and current scene.
- `SceneSyncManager` - host-authoritative scene transitions and late-join placement.
- `SceneFlowController` - reads `GameFlowDefinition` and tracks local flow state.
- `GameFlowManager` - gameplay-facing API for lobby/start/end actions.
- `VoiceSessionManager`, `VoiceChatP2P`, `SceneAudioAnchorManager` - voice lifecycle and
  scene-specific voice placement.
- `NetworkDebugOverlay` - optional Tab overlay; enable behavior in `NetworkingConfig.asset`.

## Per-Scene Contents

| Scene | Keep in the scene | Do not add |
| --- | --- | --- |
| `Matchmaking` | `SteamPack`, matchmaking UI, create/join lobby controls | Lobby/gameplay managers or player controllers |
| `Lobby` | `SteamPack`, lobby UI, role/ready/start controls, `LobbyNetworkingManager` when its lobby voice proxies are used | Extra Steam/network/state singletons |
| `BaseStation` | `SteamPack`, ground-control world/UI, `GroundControlSceneManager`, ground player spawn/setup including `LocalPlayerNetworkSetup`, ground puzzles | Space player controller or duplicated network managers |
| `Satellite` | `SteamPack`, station world/UI, `SpaceStationSceneManager`, space player spawn/setup including `LocalPlayerNetworkSetup`, station puzzles | Ground player controller or duplicated network managers |

Put a role-specific interaction or puzzle on the relevant gameplay scene object. Let it call
`GameFlowManager` or its focused game-state service; it must not create its own Steam/lobby/
scene manager.

Player prefab migration and the local-rig/remote-avatar split are described in
[PLAYER_PREFABS.md](PLAYER_PREFABS.md).

## Normal And Debug Flow

Production entry point is `Matchmaking`:

`Matchmaking -> Lobby -> BaseStation or Satellite -> Lobby`

Enable `Development Session` in `GameFlowDefinition` only for direct-scene tests. It can create
a joinable lobby while preserving the open debug scene. Leave role and scene overrides as
`None` unless testing a specific placement; default assignments come from the scene mapping.

## Validation

After adding a flow scene or changing its mapping:

1. Add the scene to Build Settings.
2. Add the same `SteamPack` prefab at the scene root.
3. Configure the scene in `GameFlowDefinition`.
4. Run **Tools > Networking > Validate Game Flow**.
