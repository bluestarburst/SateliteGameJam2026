# Steam Invites

The host must be in Play Mode with Steam initialized and a joinable lobby active.

## Invite A Friend

- Select the runtime `SteamPackConfig` component in the Inspector and use **Invite Friends**.
  When no lobby exists, use **Create Lobby And Invite Friends**.
- Or add `SteamInviteFriendsButton` to any Unity UI Button and use its `InviteFriends` method.
  Enable **Create Lobby When Missing** only for a host/debug button.

The button opens Steam's native game-invite overlay. Steam's global overlay shortcut still depends
on the Steam client and its overlay setting; the explicit button is the reliable test entry point
inside the Editor.

## Join An Existing Host

The joining client must already be running in Play Mode (or be a built game). An Editor that is not
in Play Mode has no game process and cannot receive a Steam lobby callback. A shipping build can be
launched by Steam normally; Unity cannot be put into Play Mode remotely.

When the running client accepts an invite, `SteamManager.JoinLobbyAsync` now:

1. Leaves its old lobby and clears remote avatars, voice, player state, transition work, and stale
   satellite state.
2. Joins the host lobby and waits for the host's scene assignment without loading an intermediate
   local scene.
3. Lets the lobby host send the existing role/scene assignment. A mid-round joiner then loads the
   host-selected gameplay scene and receives the normal late-join snapshot.

The same transition is used when joining from the public lobby list.
