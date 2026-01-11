# Simplified Network Setup Guide

## What Changed?

### ✅ Improvements

1. **Single Setup Component** - Configure everything in one place
2. **Auto-Component Setup** - NetworkIdentity automatically added
3. **No More Manual Retries** - Dependency resolution happens automatically
4. **Offline Testing** - Mock Steam system for testing without Steam
5. **Cleaner Code** - Base classes eliminate duplicate singleton code
6. **Fewer Scripts Needed** - Auto-setup reduces manual component assignment

---

## Quick Start (NEW METHOD)

### 1. Setup SteamPack Prefab (One-Time Setup)

1. Find or create your **SteamPack** GameObject (DontDestroyOnLoad)
2. Add the **NetworkSetup** component
3. Configure in Inspector:

```
NetworkSetup Component:
├── General Settings
│   └── Use Mock Steam: ☑ (for offline testing)
├── Player Prefab
│   ├── Remote Player Prefab: [Drag your RemotePlayer prefab here]
│   └── Auto Spawn Players: ☑
├── Scene Names
│   ├── Lobby Scene Name: "Lobby"
│   ├── Ground Control Scene Name: "GroundControl"
│   └── Space Station Scene Name: "SpaceStation"
├── Voice Settings
│   └── Space Proximity Radius: 20
└── Debug
    ├── Enable Network Debug Logs: ☐
    ├── Enable Scene Sync Debug Logs: ☐
    └── Enable Voice Debug Logs: ☐
```

**That's it!** NetworkSetup will automatically create and configure all managers:
- NetworkConnectionManager
- PlayerStateManager
- SceneSyncManager
- VoiceSessionManager
- SatelliteStateManager

### 2. Create Local Player (Simplified)

Your local player character now needs **fewer components**:

#### Minimum Requirements:
1. **NetworkTransformSync** OR **NetworkPhysicsObject** component
   - NetworkIdentity is **auto-added** (no manual setup needed!)
   
2. **Optional**: Audio Source (for voice chat)

#### Example Inspector Setup:
```
LocalPlayer GameObject:
├── Transform
├── Character Controller (your movement)
├── NetworkTransformSync (auto-adds NetworkIdentity)
│   ├── Send Rate: 10
│   ├── Sync Position: ☑
│   ├── Sync Rotation: ☑
│   └── Sync Velocity: ☑
└── Audio Source (optional)
    ├── Spatial Blend: 1 (3D)
    ├── Loop: ☑
    └── Play On Awake: ☐
```

That's it! No need to manually add NetworkIdentity anymore.

### 3. Create Remote Player Prefab (Simplified)

```
RemotePlayer Prefab:
├── Capsule (visual)
├── NetworkIdentity (can be auto-added)
└── Audio Source (for voice)
    ├── Spatial Blend: 1
    ├── Loop: ☑
    └── Play On Awake: ☐
```

Drag this prefab into NetworkSetup's "Remote Player Prefab" field.

---

## Offline Testing (NEW!)

### Enable Mock Steam Mode

1. On your SteamPack GameObject, find **NetworkSetup**
2. Check **"Use Mock Steam"**
3. This adds **MockSteamNetworking** component

### Mock Settings

```
MockSteamNetworking Component:
├── Enable Mock Mode: ☑
├── Mock Player Name: "TestPlayer"
├── Mock Steam Id: 76561199999999999
├── Mock Lobby Member Count: 2
├── Auto Create Mock Peers: ☑
└── Mock Remote Player Simulation: ☑
    ├── Simulate Remote Player: ☑
    ├── Mock Player Move Radius: 5
    ├── Mock Player Move Speed: 1
    ├── Mock Transform Send Rate: 10 Hz
    ├── Mock Audio Send Rate: 50 Hz
    └── Mock Audio Enabled: ☑
```

Now you can test multiplayer without Steam running!

### What Gets Simulated

The mock system automatically simulates:

1. **Remote Player Movement**
   - Creates a virtual remote player that moves in a circle
   - Sends position/rotation/velocity updates at 10 Hz
   - Uses Channel 1 (TransformSync packets)

2. **Voice Audio Data**
   - Sends mock audio packets at 50 Hz
   - Uses Channel 2 (VoiceData packets)
   - Simulates compressed audio frames

3. **Network Lobby**
   - Creates mock lobby with multiple players
   - Simulates P2P packet send/receive
   - Works with all existing networking code

### Testing Without Steam

```csharp
// Your code works the same way - no changes needed!
SteamManager.Instance.PlayerName // Returns "TestPlayer"
SteamManager.Instance.PlayerSteamId // Returns 76561199999999999

// Mock lobby members are auto-created
foreach (var member in SteamManager.Instance.currentLobby.Members)
{
    // Works with mock or real Steam!
}

// Remote player automatically sends:
// - Transform updates (Channel 1) at 10 Hz
// - Audio data (Channel 2) at 50 Hz
// - NetworkConnectionManager automatically spawns RemotePlayer prefab
// - VoiceSessionManager plays audio through AudioSource

// You'll see:
// 1. Remote player avatar appear in scene
// 2. Avatar moving in circular pattern
// 3. Audio data being received (if voice system enabled)
```

### Visual Testing

When mock mode is active:
1. **Start Play Mode** - Mock lobby created automatically
2. **Remote Player Spawns** - NetworkConnectionManager creates prefab
3. **Player Moves** - Circular motion at configured radius/speed
4. **Voice Data Flows** - Audio packets sent to voice system
5. **All Systems Work** - Scene sync, state management, voice gating

You can **see and hear** the mock remote player without Steam!

---

## What You DON'T Need to Do Anymore

### ❌ OLD METHOD (Don't do this):
```csharp
// OLD: Manual NetworkIdentity setup
var identity = gameObject.AddComponent<NetworkIdentity>();
identity.SetOwner(steamId);
identity.SetNetworkId((uint)steamId.Value);

// OLD: Manual manager registration
private void RepeatUntilRegistered()
{
    if (NetworkConnectionManager.Instance == null)
    {
        Invoke(nameof(RepeatUntilRegistered), 0.5f);
        return;
    }
    NetworkConnectionManager.Instance.RegisterHandler(...);
}

// OLD: Duplicate singleton code in every manager
private void Awake()
{
    if (Instance != null && Instance != this)
    {
        Destroy(gameObject);
        return;
    }
    Instance = this;
    DontDestroyOnLoad(gameObject);
}
```

### ✅ NEW METHOD (Automatic):
```csharp
// NetworkIdentity is auto-added when you add NetworkTransformSync
gameObject.AddComponent<NetworkTransformSync>();
// That's it! NetworkIdentity appears automatically.

// Managers inherit from NetworkManagerBase<T>
public class MyManager : NetworkManagerBase<MyManager>
{
    protected override void OnAwakeAfterSingleton()
    {
        // Your init code here - singleton is already setup!
    }
}

// Dependency registration uses DependencyHelper
DependencyHelper.RetryUntilSuccess(
    this,
    TryRegisterHandlers,
    "NetworkConnectionManager"
);
```

---

## Architecture Changes

### New Base Classes

#### 1. NetworkManagerBase<T>
All managers now inherit from this:
- Automatic singleton pattern
- Automatic DontDestroyOnLoad
- Override `OnAwakeAfterSingleton()` instead of `Awake()`
- No more duplicate code!

**Example:**
```csharp
public class PlayerStateManager : NetworkManagerBase<PlayerStateManager>
{
    protected override void OnAwakeAfterSingleton()
    {
        // Initialization code
    }
}
```

#### 2. NetworkSyncBase
NetworkTransformSync and NetworkPhysicsObject inherit from this:
- Auto-adds NetworkIdentity if missing
- Provides `IsOwner()` helper method
- Handles initialization order automatically

**Example:**
```csharp
public class NetworkTransformSync : NetworkSyncBase
{
    protected override void OnNetworkSetupComplete()
    {
        // NetworkIdentity is guaranteed to exist here
    }
}
```

#### 3. DependencyHelper
Replaces manual `Invoke(nameof(...), 0.5f)` patterns:

```csharp
// OLD
private void RegisterHandlers()
{
    if (NetworkConnectionManager.Instance == null)
    {
        Invoke(nameof(RegisterHandlers), 0.5f);
        return;
    }
    // Register...
}

// NEW
DependencyHelper.RetryUntilSuccess(
    this,
    TryRegisterHandlers,
    "NetworkConnectionManager",
    retryInterval: 0.1f,
    maxAttempts: 10
);

private bool TryRegisterHandlers()
{
    if (NetworkConnectionManager.Instance == null) return false;
    // Register...
    return true;
}
```

---

## Migration Guide

### If you have existing projects:

1. **Update SteamPack**:
   - Add `NetworkSetup` component
   - Configure all settings in one place
   - Remove individual manager GameObjects (they're auto-created now)

2. **Update Local Players**:
   - Keep NetworkTransformSync or NetworkPhysicsObject
   - Remove manual NetworkIdentity (it's auto-added)

3. **Update Remote Player Prefab**:
   - Assign to NetworkSetup's "Remote Player Prefab" field

4. **Enable Offline Testing**:
   - Check "Use Mock Steam" in NetworkSetup
   - Test without Steam!

---

## Troubleshooting

### NetworkIdentity Not Auto-Adding?

Check that NetworkTransformSync has "Auto Add Network Identity" checked (default: true).

### Managers Not Initializing?

Click "Validate Setup" context menu on NetworkSetup component to see current state.

### Mock Steam Not Working?

Ensure MockSteamNetworking component exists and "Enable Mock Mode" is checked.

### Want to See Debug Logs?

Enable debug logs per-manager in NetworkSetup component:
- Network Debug Logs
- Scene Sync Debug Logs
- Voice Debug Logs

---

## Summary of Improvements

| Feature | OLD | NEW |
|---------|-----|-----|
| Setup Scripts | 5+ separate managers | 1 NetworkSetup component |
| NetworkIdentity | Manual AddComponent | Auto-added |
| Singleton Code | Duplicated 5x times | NetworkManagerBase<T> (1x) |
| Dependency Retries | Manual Invoke(...) | DependencyHelper |
| Offline Testing | Requires Steam | MockSteamNetworking |
| Inspector Fields | Scattered across managers | Centralized in NetworkSetup |
| Code Duplication | High | Minimal |

**Lines of Code Reduced**: ~200+ lines of duplicate code eliminated!

---

## Next Steps

1. Add **NetworkSetup** to your SteamPack
2. Configure **all settings in one place**
3. Enable **"Use Mock Steam"** for offline testing
4. Add **NetworkTransformSync** to your player (NetworkIdentity auto-added)
5. Test with and without Steam!

For detailed networking flow, see [PlayerNetworkingSetup.md](PlayerNetworkingSetup.md).