# Player Prefabs

Use three prefab categories. Do not put any of them in Matchmaking or Lobby.

| Category | Count | Responsibility |
| --- | --- | --- |
| Local rig | One per gameplay role/scene | Input, camera, movement, interactor, local collision, and local network setup |
| Remote avatar | Spawned at runtime per same-scene peer | Transform replication, name/role tag, role model, and remote voice only |
| Model visual | One or more per role | Mesh, animator, cosmetics, and no networking/input/gameplay scripts |

## Included Foundation

- [RemotePlayerAvatar.prefab](/Users/bryanthargreaves/Documents/personal/SateliteGameJam2026/Assets/Prefabs/Networking/RemotePlayerAvatar.prefab) replaces the old physical remote cube in `NetworkingConfig.asset`.
- [PlayerRigCatalog.asset](/Users/bryanthargreaves/Documents/personal/SateliteGameJam2026/Assets/Resources/PlayerRigCatalog.asset) maps a role and gameplay scene to a local rig prefab.
- `PlayerRig`, `ScenePlayerBootstrap`, and `PlayerSpawnPoint` are the reusable local-player path.
- `RoleVisualProfile.asset` maps remote avatars to model-only visual prefabs.

Both current role entries use `PlaceholderAstronautVisual` so remote peers remain visible during
migration. Replace those two references with real model-only prefabs when the astronaut art is
ready.

A remote peer is created only for another player in the same gameplay scene. It has no camera,
input actions, `CharacterController`, `Rigidbody`, movement script, or interactor. This avoids
the two common multiplayer bugs: remote players reading local input and scene-specific physics
running twice.

## Migrate The Existing Players

Do this once for `BaseStation`, then once for `Satellite`:

1. Open the gameplay scene and select the root of its current local player object.
2. Run **Tools > Players > Migrate Selected Local Player To Prefab**.
3. Save to `Assets/Prefabs/Players/`.
4. The tool adds `PlayerRig`, preserves the existing hierarchy/references, adds the required
   network components through `RequireComponent`, and registers the prefab in
   `PlayerRigCatalog` for the scene's default role.
5. Leave the resulting prefab instance in the scene initially. It is still a safe local-player
   setup and gives you a clean migration checkpoint.

At that point the intended local rig contents are:

- `BaseStation`: `GroundPlayerControl`, `GroundPlayerInteractor`, its camera and
  `CharacterController`, plus `PlayerRig`, `NetworkIdentity`, and `LocalPlayerNetworkSetup`.
- `Satellite`: `SpacePlayerControl`, its camera and `CharacterController`, plus `PlayerRig`,
  `NetworkIdentity`, and `LocalPlayerNetworkSetup`.
- A future exterior rig: `OutsideSpacePlayerControl`, `OutsideSpacePlayerInteractor`, its
  camera and `Rigidbody`, plus the same common player components.

Do not combine ground and zero-gravity controllers on one prefab. Shared code belongs in a
small common component or a model visual; role movement and interaction remain on distinct
local-rig prefabs.

## Switch To Runtime Spawning

After both rig prefabs work as scene instances:

1. Add `PlayerSpawnPoint` objects to each gameplay scene. Set their role/scene and unique
   priority; use several points when a role can have several players.
2. Add one `ScenePlayerBootstrap` to each gameplay scene and leave its catalog blank so it uses
   `NetworkingConfig.asset`.
3. Remove the old local-player instance from that scene. The bootstrap creates exactly one local
   rig; `SceneSyncManager` creates remote avatars for the other players in the same scene.
4. Add model-only prefabs to `RoleVisualProfile.asset` for GroundControl/BaseStation and
   SpaceStation/Satellite.

For now, player spawn points are ordered by priority. The next multiplayer pass should add a
host-replicated spawn-slot assignment before enabling crowded joins, so a late join cannot take
an occupied point. That assignment belongs in `PlayerStateManager`, not in a movement prefab.
