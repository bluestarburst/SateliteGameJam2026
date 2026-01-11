# Network Architecture Quick Reference

## Files Created/Modified

### New Files (Best Practices)
1. **NetworkManagerBase.cs** - Base class for all singleton managers
2. **DependencyHelper.cs** - Automatic dependency retry system
3. **NetworkSyncBase.cs** - Base class for sync components with auto-setup
4. **MockSteamNetworking.cs** - Offline testing without Steam
5. **NetworkSetup.cs** - All-in-one configuration component
6. **SimplifiedNetworkSetup.md** - User guide for new system

### Modified Files (Refactored)
1. **NetworkConnectionManager.cs** - Now inherits NetworkManagerBase
2. **PlayerStateManager.cs** - Now inherits NetworkManagerBase
3. **SceneSyncManager.cs** - Now inherits NetworkManagerBase
4. **VoiceSessionManager.cs** - Now inherits NetworkManagerBase
5. **NetworkTransformSync.cs** - Now inherits NetworkSyncBase
6. **NetworkPhysicsObject.cs** - Now inherits NetworkSyncBase

---

## Code Improvements Summary

### Before vs After

#### Singleton Pattern
```csharp
// BEFORE (duplicated 5+ times):
public class MyManager : MonoBehaviour
{
    public static MyManager Instance { get; private set; }
    
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
}

// AFTER (once in base class):
public class MyManager : NetworkManagerBase<MyManager>
{
    protected override void OnAwakeAfterSingleton()
    {
        // Your init code
    }
}
```

#### Dependency Resolution
```csharp
// BEFORE:
private void RegisterHandlers()
{
    if (NetworkConnectionManager.Instance == null)
    {
        Debug.LogWarning("Dependency not ready. Retrying...");
        Invoke(nameof(RegisterHandlers), 0.5f);
        return;
    }
    // Register...
}

// AFTER:
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

#### NetworkIdentity Setup
```csharp
// BEFORE (manual):
[RequireComponent(typeof(NetworkIdentity))]
public class NetworkTransformSync : MonoBehaviour
{
    private NetworkIdentity netIdentity;
    
    private void Awake()
    {
        netIdentity = GetComponent<NetworkIdentity>();
        if (netIdentity == null)
        {
            Debug.LogError("NetworkIdentity required!");
            enabled = false;
            return;
        }
    }
}

// AFTER (automatic):
public class NetworkTransformSync : NetworkSyncBase
{
    // netIdentity field inherited and auto-initialized
    // NetworkIdentity auto-added if missing
    
    protected override void OnNetworkSetupComplete()
    {
        // netIdentity guaranteed to exist here
    }
}
```

---

## Inspector Setup

### Old Method (Scattered)
```
Scene Hierarchy:
├── SteamPack
│   ├── SteamManager (configure)
│   ├── NetworkConnectionManager (configure)
│   │   ├── Player Prefab: [assign]
│   │   └── Auto Spawn: [check]
│   ├── PlayerStateManager (no config)
│   ├── SceneSyncManager (configure)
│   │   ├── Lobby Scene: [type]
│   │   ├── Ground Control Scene: [type]
│   │   └── Space Station Scene: [type]
│   ├── VoiceSessionManager (configure)
│   │   ├── Proximity Radius: [set]
│   │   └── Debug Logs: [check]
│   └── SatelliteStateManager (no config)
```
**Result**: 6+ components to configure manually

### New Method (Centralized)
```
Scene Hierarchy:
├── SteamPack
│   ├── SteamManager (existing)
│   └── NetworkSetup ✨ ONE COMPONENT
        ├── Use Mock Steam: ☑
        ├── Remote Player Prefab: [assign]
        ├── Auto Spawn Players: ☑
        ├── Lobby Scene Name: "Lobby"
        ├── Ground Control Scene Name: "GroundControl"
        ├── Space Station Scene Name: "SpaceStation"
        ├── Space Proximity Radius: 20
        └── Debug Logs: [per-system toggles]
```
**Result**: 1 component configures everything! ✨

---

## Offline Testing

### Setup Mock Steam
1. Add **NetworkSetup** to SteamPack
2. Check **"Use Mock Steam"**
3. Configure mock settings:
   - Mock Player Name: "TestPlayer"
   - Mock Steam ID: 76561199999999999
   - Mock Lobby Member Count: 2
   - Auto Create Mock Peers: ☑

### Test Without Steam Running
```csharp
// Works with mock or real Steam - no code changes!
var myId = SteamManager.Instance.PlayerSteamId;
var myName = SteamManager.Instance.PlayerName;

foreach (var member in SteamManager.Instance.currentLobby.Members)
{
    Debug.Log($"Player: {member.Name} ({member.Id})");
}
```

---

## Component Requirements

### Local Player (You Control)
```
Minimum:
├── NetworkTransformSync OR NetworkPhysicsObject
    └── Auto-adds NetworkIdentity ✨

Optional:
└── Audio Source (for voice)
```

### Remote Player (Other Players)
```
Required:
├── NetworkIdentity (can be auto-added)
└── Audio Source (for voice)
    ├── Spatial Blend: 1
    └── Loop: ☑
```

### Physics Objects (Soccer Ball, etc.)
```
Required:
├── Rigidbody
└── NetworkPhysicsObject
    └── Auto-adds NetworkIdentity ✨
```

---

## Key Classes

### NetworkManagerBase<T>
- **Purpose**: Base class for singleton managers
- **Features**: 
  - Automatic singleton pattern
  - DontDestroyOnLoad
  - FindFirstObjectByType fallback
- **Override**: `OnAwakeAfterSingleton()` and `OnDestroyBeforeNull()`
- **Inheritors**: NetworkConnectionManager, PlayerStateManager, SceneSyncManager, VoiceSessionManager

### NetworkSyncBase
- **Purpose**: Base class for networked sync components
- **Features**:
  - Auto-adds NetworkIdentity
  - Provides `IsOwner()` helper
  - Handles initialization order
- **Override**: `OnNetworkSetupComplete()`
- **Inheritors**: NetworkTransformSync, NetworkPhysicsObject

### DependencyHelper
- **Purpose**: Retry operations with dependencies
- **Usage**: `DependencyHelper.RetryUntilSuccess(behaviour, tryFunc, dependencyName, interval, maxAttempts)`
- **Returns**: Automatically stops on success or max attempts

### MockSteamNetworking
- **Purpose**: Offline testing without Steam
- **Features**:
  - Simulates lobby with multiple players
  - P2P packet send/receive queue
  - Add/remove mock players at runtime
- **Enable**: Check "Use Mock Steam" in NetworkSetup

### NetworkSetup
- **Purpose**: All-in-one configuration for SteamPack
- **Features**:
  - Auto-creates all managers
  - Centralized configuration
  - Validation context menu
- **Usage**: Add to SteamPack, configure, done!

---

## Unity Best Practices Applied

✅ **Single Responsibility** - Each base class has one job  
✅ **DRY (Don't Repeat Yourself)** - No duplicate singleton code  
✅ **Composition** - NetworkSyncBase adds functionality without tight coupling  
✅ **Inspector-Driven** - All config in Inspector, no hardcoded values  
✅ **Automatic Setup** - Components auto-add dependencies  
✅ **Testability** - Mock system for offline testing  
✅ **Clear Architecture** - Base classes make structure obvious  
✅ **Error Prevention** - Dependency retry prevents init order bugs  

---

## Migration Checklist

- [ ] Add **NetworkSetup** component to SteamPack
- [ ] Configure all settings in NetworkSetup Inspector
- [ ] Remove manual manager GameObjects (auto-created now)
- [ ] Test with **"Use Mock Steam"** enabled
- [ ] Verify local player has NetworkTransformSync (NetworkIdentity auto-added)
- [ ] Assign RemotePlayer prefab to NetworkSetup
- [ ] Run game - managers should auto-initialize
- [ ] Check console for "[NetworkSetup] Network setup complete!"

---

## Metrics

| Metric | Before | After | Improvement |
|--------|--------|-------|-------------|
| Singleton Code Lines | ~60 (12 lines × 5 managers) | ~20 (base class) | **67% reduction** |
| Retry Pattern Lines | ~45 (15 lines × 3 managers) | ~25 (helper + usage) | **44% reduction** |
| Setup Components | 6+ to configure | 1 to configure | **83% reduction** |
| Manual NetworkIdentity | Required | Auto-added | **100% easier** |
| Offline Testing | Not possible | MockSteamNetworking | **New feature!** |
| Total Code Reduction | - | - | **~200 lines** |

---

## Support

- **Setup Guide**: SimplifiedNetworkSetup.md
- **Detailed Flow**: PlayerNetworkingSetup.md  
- **Architecture**: NetworkArchitecture.md
- **Remote Players**: RemotePlayerSetup.md

For questions, check context menu "Validate Setup" on NetworkSetup component.