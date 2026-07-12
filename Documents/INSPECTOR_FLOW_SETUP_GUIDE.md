# Inspector-Configurable Flow Setup Guide

This guide explains how to configure the new inspector-driven multiplayer flow for:

- Full game loop (`Matchmaking -> Lobby -> Gameplay split -> Lobby`)
- Direct-scene development sessions using `DontDestroyOnLoad`

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
- `GroundControl` -> `sceneId: GroundControl`, `sceneName: BaseStation`, `modeType: Gameplay`, `allowedRoles: GroundControl`
- `SpaceStation` -> `sceneId: SpaceStation`, `sceneName: Satellite`, `modeType: Gameplay`, `allowedRoles: SpaceStation`
- `Matchmaking` -> `sceneId: Matchmaking`, `sceneName: Matchmaking`, `modeType: Matchmaking`

Set well-known fields:

- `lobbyScene = Lobby`
- `matchmakingScene = Matchmaking`

The allowed-role list is also the role-to-scene routing rule. Do not create a second mapping.

## Step C: Configure `SteamPack` Prefab

On the `SteamPack` prefab:

1. Ensure these components exist:
   - `SteamManager`
   - `SceneFlowController`
   - `SceneSyncManager`
   - `NetworkConnectionManager`
   - `VoiceSessionManager`
   - `VoiceChatP2P`
   - `SceneAudioAnchorManager`
2. Select `SteamPackConfig`, assign `NetworkingConfig.asset` and `RoleVisualProfile.asset`, then use `Apply To Attached Managers`.
   `NetworkingConfig.asset` owns the GameFlowDefinition and remote-player prefab references.

## Step D: Configure Role Visuals

In `RoleVisualProfile.entries`, add:

- Scene-specific and/or default visual prefabs per role.
- Use `scene = None` as role default fallback.

Remote players now use `PlayerAvatarComposition` and spawn visuals under `VisualAnchor`.

## Step E: Scene Wiring

1. Put one `SteamPack` in every configured flow scene: `Matchmaking`, `Lobby`, `BaseStation`, and `Satellite`.
2. Keep scene-specific managers only for scene-specific logic:
   - `GroundControlSceneManager`
   - `SpaceStationSceneManager`
3. `LobbyNetworkingManager` is still valid for lobby-only voice proxy behavior if desired.

Important:

- Core managers use `DontDestroyOnLoad`; scene copies self-destroy after the first instance persists.
- This lets a developer press Play directly from any configured flow scene without special bootstrap setup.

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

## 3) Direct Scene Development Sessions

Use this for fast iteration without manually navigating through matchmaking.

## Test Scene Pattern

In `GameFlowDefinition > Development Session`:

- Enable the session.
- Keep `createJoinableLobby` enabled to host from the scene currently open in the editor.
- Leave local and joining role/scene overrides as `None` to derive them from the scene mapping.
- Override only when a focused test needs a non-default join placement.

The development session is gated to editor and development builds. It remains local when Steam is unavailable.

## Recommended Focused Test Cases

### A) Scene routing test
- Press Play from each configured flow scene.
- Confirm the local role is derived from the flow entry.
- Confirm the joinable lobby remains at that point in the flow.

### B) Role split test
- Join with second client
- Verify the joiner receives the configured role and scene from the lobby host.
- Start a normal lobby round and verify role-based scene split from the same flow asset.

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
- `GameFlowDefinition`
  - every scene is enabled in Build Settings
  - gameplay scenes have one allowed role
  - development join overrides are set only when needed
- `SteamPackConfig`
  - `NetworkingConfig.asset` and `RoleVisualProfile.asset` assigned
- `SceneAudioAnchorManager`
  - rules created for lobby/game scenes

Run `Tools > Networking > Validate Game Flow` after changing mappings or SteamPack.

---

## 5) Suggested Team Workflow

- Treat `GameFlowDefinition` as source of truth for scene graph and role routing.
- Treat `RoleVisualProfile` as source of truth for role visuals.
- Keep SteamPack as the only persistent networking bootstrap.
- Use the development-session overrides only for focused join-placement tests.
