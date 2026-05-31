# Inspector-Configurable Flow Setup Guide

This guide explains how to configure the new inspector-driven multiplayer flow for:

- Full game loop (`Matchmaking -> Lobby -> Gameplay split -> Lobby`)
- Focused SteamPack feature tests using `DontDestroyOnLoad`

---

## 1) What Was Added

New core systems:

- `Assets/Scripts/Networking/Core/GameFlowDefinition.cs`
- `Assets/Scripts/Networking/Core/SceneFlowController.cs`
- `Assets/Scripts/Networking/Core/RoleVisualProfile.cs`
- `Assets/Scripts/Networking/Sync/PlayerAvatarComposition.cs`
- `Assets/Scripts/Networking/Voice/SceneAudioAnchorManager.cs`

Updated systems now consume these:

- `NetworkingConfiguration`, `SceneSyncManager`, `SteamManager`, `NetworkConnectionManager`
- `VoiceSessionManager`, `VoiceChatP2P`, `TransmissionInteract`
- `CreateLobbyButton`, `UnrankedLobbiesView`, `LobbyPlayersView`

---

## 2) Full Game Loop Setup (Production + Daily Dev)

## Step A: Create Required Assets

1. Create a `GameFlowDefinition` asset:
   - `Create -> Networking -> Game Flow Definition`
   - Suggested path: `Assets/Resources/GameFlowDefinition.asset`
2. Create a `RoleVisualProfile` asset:
   - `Create -> Networking -> Role Visual Profile`
   - Suggested path: `Assets/Resources/RoleVisualProfile.asset`
3. Open your existing `NetworkingConfig.asset` and assign:
   - `gameFlowDefinition` -> your new `GameFlowDefinition` asset

## Step B: Configure `GameFlowDefinition`

In the `scenes` list, add at least:

- `Lobby` -> `sceneId: Lobby`, `sceneName: Lobby`, `modeType: Lobby`
- `GroundControl` -> `sceneId: GroundControl`, `sceneName: BaseStation`, `modeType: Gameplay`
- `SpaceStation` -> `sceneId: SpaceStation`, `sceneName: Satellite`, `modeType: Gameplay`
- Optional: `Matchmaking` using `sceneId: None`, `sceneName: Matchmaking`, `modeType: Matchmaking`

In `roleSceneRules`, add:

- `GroundControl -> GroundControl`
- `SpaceStation -> SpaceStation`

Set well-known fields:

- `lobbyScene = Lobby`
- `matchmakingScene = None` (or your preferred scene mapping strategy)

Optional hooks:

- Add UnityEvents in `onWillEnter` and `onDidEnter` per scene for custom setup/cleanup.

## Step C: Configure `SteamPack` Prefab

On the `SteamPack` prefab:

1. Ensure these components exist once:
   - `SteamManager`
   - `SceneFlowController`
   - `SceneSyncManager`
   - `NetworkConnectionManager`
   - `VoiceSessionManager`
   - `VoiceChatP2P`
   - `SceneAudioAnchorManager`
2. Wire references:
   - `SceneFlowController.networkingConfiguration` -> `NetworkingConfig.asset`
   - `SceneFlowController.gameFlowDefinition` -> `GameFlowDefinition.asset`
   - `SceneSyncManager.config` -> `NetworkingConfig.asset`
   - `NetworkConnectionManager.config` -> `NetworkingConfig.asset`
   - `NetworkConnectionManager.roleVisualProfile` -> `RoleVisualProfile.asset`
   - `VoiceSessionManager.config` -> `NetworkingConfig.asset`

## Step D: Configure Role Visuals

In `RoleVisualProfile.entries`, add:

- Scene-specific and/or default visual prefabs per role.
- Use `scene = None` as role default fallback.

Remote players now use `PlayerAvatarComposition` and spawn visuals under `VisualAnchor`.

## Step E: Scene Wiring

1. Put one `SteamPack` in your bootstrap entry scene (recommended: `Matchmaking`).
2. Keep scene-specific managers only for scene-specific logic:
   - `GroundControlSceneManager`
   - `SpaceStationSceneManager`
3. `LobbyNetworkingManager` is still valid for lobby-only voice proxy behavior if desired.

Important:

- Core managers use `DontDestroyOnLoad`; duplicate manager instances in loaded scenes self-destroy.
- Preferred workflow is one bootstrap source of truth to avoid confusion.

## Step F: Audio Anchor Rules

`SceneAudioAnchorManager.rules` supports:

- `FollowPlayerAvatar`
- `FixedAnchor`
- `NonSpatial`

For `FixedAnchor`, set `anchorObjectName` to an actual scene GameObject name (for example a console speaker object).  
If no rule matches, system falls back to avatar-follow behavior.

## Step G: Verify Full Loop

1. Host creates lobby.
2. Players pick roles.
3. Host starts game.
4. Role-based split sends players to configured target scenes.
5. End game returns players to lobby.
6. Confirm voice behavior:
   - Lobby: broad voice
   - Gameplay: role/proximity gated + anchor mode behavior

---

## 3) SteamPack Feature Unit Tests with `DontDestroyOnLoad`

Use this for fast iteration without running full matchmaking each time.

## Test Scene Pattern

Create a small `NetTest_*` scene with:

- One `SteamPack` prefab instance
- Minimal local player/controller test object(s)
- Optional test anchors for audio (`CommAnchor`, `ConsoleAnchor`, etc.)

Do not add additional manager duplicates outside SteamPack unless intentional.

## Dev Startup Configuration

On `SteamManager`:

- `enableDevStartupProfile = true`
- `devStartupMode` one of:
  - `Normal`
  - `AutoCreateLobby`
  - `SkipToLobby`
  - `SkipToGameplay`
  - `AutoJoinByCode` (currently logs warning; not implemented yet)
- Optional:
  - `forcedLocalRole`
  - `autoStartWhenMinimumPeers`
  - `minimumPeersToAutoStart`

Notes:

- Dev startup logic is gated to editor/dev builds in code.
- `SkipToGameplay` uses `SceneFlowController.ResolveGameplaySceneForRole`.

## Recommended Focused Test Cases

### A) Scene routing test
- Set mode `SkipToLobby` or `SkipToGameplay`
- Validate scene load comes through `SceneFlowController`

### B) Role split test
- Use `AutoCreateLobby`
- Join with second client
- Assign different roles
- Start game and verify split scenes from `GameFlowDefinition.roleSceneRules`

### C) Voice anchor test
- Add `SceneAudioAnchorManager` rules for fixed/non-spatial/follow
- Toggle `TransmissionInteract`
- Verify remote voice object behavior and audibility

### D) DDOL persistence test
- Transition across scenes repeatedly
- Confirm singleton managers persist once and duplicates are destroyed

---

## 4) Minimal Inspector Checklist

- `NetworkingConfig.asset`
  - `gameFlowDefinition` assigned
  - `remotePlayerPrefab` assigned
  - voice settings (`voiceChatEnabled`, `proximityVoiceDistance`, `useRoleBasedVoiceGating`) set
- `SceneFlowController`
  - `gameFlowDefinition` assigned
  - `networkingConfiguration` assigned
- `NetworkConnectionManager`
  - `roleVisualProfile` assigned
- `SteamManager`
  - dev startup options configured for your current workflow
- `SceneAudioAnchorManager`
  - rules created for lobby/game scenes

---

## 5) Known Limitation Right Now

- `SteamManager` `AutoJoinByCode` mode currently logs a warning and is not implemented yet.

---

## 6) Suggested Team Workflow

- Treat `GameFlowDefinition` as source of truth for scene graph and role routing.
- Treat `RoleVisualProfile` as source of truth for role visuals.
- Keep SteamPack as the only persistent networking bootstrap.
- Use dev startup mode presets per tester profile to speed up iteration.

