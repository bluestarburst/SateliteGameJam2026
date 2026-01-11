# Remote Player Prefab Setup Guide

## Creating the Remote Player Prefab

### 1. Create the Player GameObject

1. In Unity Hierarchy, create a new GameObject: `GameObject > 3D Object > Capsule` (or your character model)
2. Rename it to **"RemotePlayer"**
3. Add a **Character Controller** or **Rigidbody** component for movement
4. Tag it as **"Player"** (Edit > Project Settings > Tags and Layers)

### 2. Add Required Network Components

Add these components to the RemotePlayer GameObject:

#### NetworkIdentity Component
- Click **Add Component** > Search for "NetworkIdentity"
- The Network ID will be set automatically at runtime
- Owner SteamId will be assigned when spawned

#### Audio Components (for voice chat)
- **Audio Source**:
  - Click **Add Component** > Audio Source
  - Set **Spatial Blend** to `1` (3D sound)
  - Enable **Play On Awake**: `false`
  - Set **Loop**: `true`
  - Adjust **Min Distance** and **Max Distance** for voice range

### 3. Optional: Add Visual Components

- Add a **MeshRenderer** or character model
- Add player name tag (TextMeshPro above head)
- Add team color indicator

### 4. Create the Prefab

1. Drag the **RemotePlayer** GameObject from Hierarchy to your **Prefabs** folder
2. Delete the RemotePlayer instance from the scene
3. Your prefab is ready!

---

## Assigning the Prefab to NetworkConnectionManager

### Option A: Via Inspector (Recommended)

1. In your Hierarchy, find the **SteamPack** GameObject (or wherever NetworkConnectionManager is attached)
2. Expand it to find **NetworkConnectionManager** component
3. In the Inspector, locate the **Player Prefab** section:
   - Check **Auto Spawn Player**: `true`
   - Drag your **RemotePlayer** prefab from the Prefabs folder into the **Player Prefab** field

### Option B: Via Code

If you need to assign it dynamically:

```csharp
using SatelliteGameJam.Networking.Core;

// In your initialization script
NetworkConnectionManager.Instance.SetPlayerPrefab(remotePlayerPrefab, autoSpawn: true);
```

---

## Verifying the Setup

### Test Checklist

- [ ] RemotePlayer prefab has **NetworkIdentity** component
- [ ] RemotePlayer prefab has **Audio Source** with Spatial Blend = 1
- [ ] RemotePlayer prefab is assigned to NetworkConnectionManager
- [ ] Auto Spawn Player is checked
- [ ] RemotePlayer prefab is tagged as "Player"

### Runtime Test

1. Start a lobby with 2+ players
2. Start the game (scene transition)
3. Check Unity Console for: `Spawning remote player for [SteamId]`
4. Verify remote player avatars appear in the scene
5. Test voice chat (should be positional audio)

---

## Common Issues

**Issue**: Remote players not spawning
- **Fix**: Ensure `Auto Spawn Player` is checked on NetworkConnectionManager
- **Fix**: Verify the prefab reference isn't missing (shows as "None")

**Issue**: Voice audio not working
- **Fix**: Check Audio Source has Spatial Blend = 1 (3D)
- **Fix**: Verify VoiceRemotePlayer component is attached at runtime (auto-added)

**Issue**: Players spawn in wrong location
- **Fix**: Set spawn points in your scene manager scripts
- **Fix**: Add a spawn point manager to position players correctly

**Issue**: Multiple copies of same player
- **Fix**: NetworkConnectionManager already prevents duplicates - check for manual spawning code

---

## Advanced: Custom Spawn Logic

If you need custom spawn positions or player types:

```csharp
// In your scene manager (GroundControlSceneManager or SpaceStationSceneManager)

private Vector3 GetNextSpawnPoint()
{
    // Your spawn point logic
    return new Vector3(0, 1, 0);
}

// After NetworkConnectionManager spawns the player:
private void OnRemotePlayerJoined(SteamId steamId)
{
    var playerObj = FindRemotePlayerObject(steamId);
    if (playerObj != null)
    {
        playerObj.transform.position = GetNextSpawnPoint();
    }
}
```

---

## Summary

Your RemotePlayer prefab needs:
1. **NetworkIdentity** - for network ownership
2. **Audio Source** - for voice chat (Spatial Blend = 1)
3. Assigned to **NetworkConnectionManager** prefab field
4. **Auto Spawn Player** enabled

The scene managers (GroundControlSceneManager, SpaceStationSceneManager) automatically:
- Spawn remote players when they join your scene
- Register them with VoiceSessionManager for voice chat
- Clean up when players leave
