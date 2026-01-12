# Networking Scene Setup Guide

This guide explains how to configure Unity scenes for networking in the Satellite Game.

---

## Prerequisites

Before setting up scenes, ensure you have:

1. **NetworkingConfiguration asset created**
   - Right-click in Project → Create → Networking → Configuration
   - Save in `Assets/Resources/NetworkingConfig.asset`
   - Configure all settings in Inspector

2. **Prefabs configured**
   - See [PREFAB_SETUP.md](PREFAB_SETUP.md) for prefab setup
   - Remote player prefab assigned in NetworkingConfiguration

3. **Core managers in first scene**
   - NetworkConnectionManager
   - PlayerStateManager
   - SceneSyncManager
   - VoiceSessionManager
   - GameFlowManager
   - NetworkDebugOverlay

---

## Lobby Scene Setup

The lobby is where players connect, select roles, and start the game.

### 1. Create UI Canvas

Create a Canvas for lobby UI:

```
Lobby (Scene)
├── UI Canvas
│   ├── Ready Button
│   ├── Role Dropdown
│   ├── Start Game Button (host only)
│   ├── Player List
│   └── Connection Status
```

### 2. Wire Up UI Buttons

Connect UI buttons to GameFlowManager APIs:

**Ready Button (onClick):**
```csharp
GameFlowManager.Instance.MarkPlayerReady();
```

**Role Dropdown (onValueChanged):**
```csharp
public void OnRoleChanged(int roleIndex)
{
    PlayerRole role = (PlayerRole)roleIndex; // 0=None, 1=GroundControl, 2=SpaceStation
    GameFlowManager.Instance.SelectRole(role);
}
```

**Start Game Button (onClick - host only):**
```csharp
public void OnStartGamePressed()
{
    if (GameFlowManager.Instance.IsLocalPlayerHost())
    {
        GameFlowManager.Instance.StartGame();
    }
}
```

### 3. Add LobbyNetworkingManager

1. Create empty GameObject named "LobbyNetworking"
2. Add component: `LobbyNetworkingManager`
3. **Player Spawn Parent**: Create empty GameObject as spawn point
4. Assign spawn parent in inspector

### 4. Configure Voice Chat

Voice is enabled by default in lobby (all players hear each other).

No additional setup needed - LobbyNetworkingManager handles this automatically.

### 5. Create Player List UI

Display connected players dynamically:

```csharp
public class LobbyPlayerList : MonoBehaviour
{
    [SerializeField] private Transform playerListParent;
    [SerializeField] private GameObject playerEntryPrefab;

    private void Start()
    {
        GameFlowManager.Instance.OnRemotePlayerJoined += OnPlayerJoined;
        GameFlowManager.Instance.OnRemotePlayerLeft += OnPlayerLeft;
        
        RefreshPlayerList();
    }

    private void OnPlayerJoined(SteamId steamId)
    {
        RefreshPlayerList();
    }

    private void OnPlayerLeft(SteamId steamId)
    {
        RefreshPlayerList();
    }

    private void RefreshPlayerList()
    {
        // Clear existing entries
        foreach (Transform child in playerListParent)
        {
            Destroy(child.gameObject);
        }

        // Create entry for each player
        var playerIds = GameFlowManager.Instance.GetAllPlayerIds();
        foreach (var playerId in playerIds)
        {
            var entry = Instantiate(playerEntryPrefab, playerListParent);
            var text = entry.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            text.text = GameFlowManager.Instance.GetPlayerName(playerId);
        }
    }
}
```

---

## Ground Control Scene Setup

Ground Control is where players monitor satellite health and coordinate with Space Station.

### 1. Add GroundControlSceneManager

1. Create empty GameObject named "GroundControlNetworking"
2. Add component: `GroundControlSceneManager`
3. **Log Debug**: Enable for testing, disable for production

The manager automatically:
- Sets player role and scene
- Spawns Ground Control players only
- Configures voice gating based on console interaction

### 2. Setup Transmission Console

The console is the communication hub for Ground Control.

**Console Interaction Script:**
```csharp
public class TransmissionConsole : MonoBehaviour
{
    private GroundControlSceneManager sceneManager;

    private void Start()
    {
        sceneManager = FindObjectOfType<GroundControlSceneManager>();
    }

    public void OnPlayerEnterConsole()
    {
        // Called when player walks up to console and presses interact
        sceneManager?.OnConsoleInteractionStarted();
        
        // Or use GameFlowManager:
        GameFlowManager.Instance.SetConsoleInteraction(true);
    }

    public void OnPlayerExitConsole()
    {
        // Called when player walks away or presses exit
        sceneManager?.OnConsoleInteractionEnded();
        
        // Or use GameFlowManager:
        GameFlowManager.Instance.SetConsoleInteraction(false);
    }
}
```

### 3. Add Networked Objects

**Tools and Equipment:**
1. Place tool prefabs in scene
2. Each must have NetworkIdentity and NetworkInteractionState
3. Assign unique Network IDs in inspector

**Example:**
```
Ground Control Scene
├── Tools
│   ├── Wrench (NetworkIdentity ID: 100)
│   ├── Scanner (NetworkIdentity ID: 101)
│   └── Repair Kit (NetworkIdentity ID: 102)
```

### 4. Create Satellite Visual Reference

Ground Control sees a non-networked representation of the satellite:

```csharp
public class SatelliteMonitor : MonoBehaviour
{
    [SerializeField] private TMPro.TextMeshProUGUI healthText;
    [SerializeField] private Image healthBar;

    private void Start()
    {
        // Subscribe to satellite health changes
        GameFlowManager.Instance.OnSatelliteHealthChanged += OnHealthChanged;
        
        // Subscribe to component damage
        GameFlowManager.Instance.OnSatelliteComponentDamaged += OnComponentDamaged;
        GameFlowManager.Instance.OnSatelliteComponentRepaired += OnComponentRepaired;
    }

    private void OnHealthChanged(float newHealth)
    {
        healthText.text = $"Satellite Health: {newHealth:F0}%";
        healthBar.fillAmount = newHealth / 100f;
    }

    private void OnComponentDamaged(int componentIndex)
    {
        Debug.Log($"Component {componentIndex} damaged!");
        // Update UI, show warning
    }

    private void OnComponentRepaired(int componentIndex)
    {
        Debug.Log($"Component {componentIndex} repaired!");
        // Update UI, clear warning
    }
}
```

### 5. Configure Scene-Specific Settings

**Build Settings:**
- Add Ground Control scene to build
- Scene index should match scene order in NetworkingConfiguration

**Lighting:**
- Bake lighting for performance
- Use real-time shadows sparingly

---

## Space Station Scene Setup

Space Station is where players physically interact with the satellite.

### 1. Add SpaceStationSceneManager

1. Create empty GameObject named "SpaceStationNetworking"
2. Add component: `SpaceStationSceneManager`
3. **Log Debug**: Enable for testing

The manager automatically:
- Sets player role and scene
- Spawns Space Station players only
- Voice enabled (always hear Ground Control when they're at console)

### 2. Setup Satellite Model

The satellite is the main interactive object:

**Satellite Structure:**
```
Satellite
├── Core (NetworkIdentity ID: 200)
│   ├── NetworkPhysicsObject
│   └── Rigidbody
├── Solar Panel 1 (NetworkIdentity ID: 201)
│   ├── NetworkTransformSync (rotation)
│   └── Hinge Joint
├── Solar Panel 2 (NetworkIdentity ID: 202)
│   ├── NetworkTransformSync (rotation)
│   └── Hinge Joint
├── Antenna (NetworkIdentity ID: 203)
│   ├── NetworkTransformSync (rotation)
│   └── Motor
└── Sensor Array (NetworkIdentity ID: 204)
    └── NetworkIdentity
```

### 3. Implement Repair System

**Repair Interaction:**
```csharp
public class RepairableComponent : MonoBehaviour
{
    [SerializeField] private int componentIndex; // Matches satellite component ID
    [SerializeField] private float repairTime = 5f;
    
    private NetworkIdentity networkIdentity;
    private bool isRepairing = false;

    private void Start()
    {
        networkIdentity = GetComponent<NetworkIdentity>();
    }

    public void StartRepair()
    {
        if (GameFlowManager.Instance.IsSatelliteComponentDamaged(componentIndex))
        {
            isRepairing = true;
            StartCoroutine(RepairCoroutine());
        }
    }

    private IEnumerator RepairCoroutine()
    {
        yield return new WaitForSeconds(repairTime);
        
        if (isRepairing)
        {
            // Report repair to network
            GameFlowManager.Instance.ReportSatelliteRepair(componentIndex);
            isRepairing = false;
        }
    }

    public void CancelRepair()
    {
        isRepairing = false;
    }
}
```

### 4. Add Damage System

**Damage Trigger:**
```csharp
public class SpaceDebris : MonoBehaviour
{
    [SerializeField] private int damageComponentIndex;
    [SerializeField] private float damage = 25f;

    private void OnCollisionEnter(Collision collision)
    {
        // Check if hit satellite component
        var repairableComponent = collision.gameObject.GetComponent<RepairableComponent>();
        if (repairableComponent != null)
        {
            // Report damage to network (authority only)
            if (SatelliteStateManager.Instance.HasAuthority)
            {
                GameFlowManager.Instance.ReportSatelliteDamage(damageComponentIndex);
            }
        }
    }
}
```

### 5. Setup Player Spawn Points

Create spawn positions for Space Station players:

```
Space Station Scene
├── Player Spawns
│   ├── Spawn Point 1 (Transform)
│   ├── Spawn Point 2 (Transform)
│   └── Spawn Point 3 (Transform)
```

No additional scripting needed - SpaceStationSceneManager handles spawning.

### 6. Optional: Proximity Voice Detection

For proximity-based voice chat in Space Station:

```csharp
public class ProximityVoiceDetector : MonoBehaviour
{
    [SerializeField] private float detectionRadius = 20f;
    private Transform localPlayer;

    private void Update()
    {
        if (localPlayer != null)
        {
            GameFlowManager.Instance.UpdateLocalPlayerPosition(localPlayer.position);
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}
```

---

## Scene Transition Testing

### Test Scene Flow

1. **Start in Lobby**
   - All players connect
   - Select roles (Ground Control or Space Station)
   - Mark ready
   - Host starts game

2. **Transition to Game Scenes**
   - Ground Control players → Ground Control scene
   - Space Station players → Space Station scene
   - Verify players spawn correctly
   - Test voice communication

3. **End Game**
   - Host calls EndGame
   - All players return to Lobby

### Debug Scene Transitions

Enable NetworkDebugOverlay (Tab key) to monitor:
- Current scene for each player
- Player roles
- Scene change packets
- Connection status

### Common Scene Issues

**Players not transitioning:**
- Check scene names in NetworkingConfiguration match exactly
- Verify scenes are added to Build Settings
- Check SceneSyncManager is in first scene

**Voice not working across scenes:**
- Ground Control: Verify console interaction is being detected
- Space Station: Voice should always work
- Check VoiceSessionManager configuration

**Players spawning in wrong scene:**
- Verify GroundControlSceneManager/SpaceStationSceneManager exist in scenes
- Check PlayerStateManager has correct role/scene set
- Monitor NetworkDebugOverlay for state mismatches

---

## Performance Optimization

### Scene-Specific Optimizations

**Lobby:**
- Minimal lighting (UI-focused)
- No complex physics
- Simple player models

**Ground Control:**
- Baked lighting
- Occlusion culling for large rooms
- LOD for satellite monitor displays

**Space Station:**
- Realtime lighting for space ambiance
- LOD on satellite components
- Physics optimization for floating objects

### Network Optimization

Monitor packet statistics in NetworkDebugOverlay:
- Target: < 100 packets/sec per player
- Reduce sync rates if needed
- Use interpolation for smooth movement

---

## Build Settings Configuration

**Required Build Settings:**
1. Add all scenes in correct order:
   - Lobby
   - Ground Control
   - Space Station

2. **Player Settings:**
   - Scripting Backend: IL2CPP (for better performance)
   - API Compatibility: .NET Standard 2.1
   - Allow unsafe code: TRUE (for Steamworks)

3. **Quality Settings:**
   - Disable VSync for testing
   - Set appropriate quality levels

---

## Final Checklist

Before shipping, verify:

- [ ] NetworkingConfiguration asset exists in Resources
- [ ] All scenes have appropriate scene managers
- [ ] UI buttons wired to GameFlowManager
- [ ] Prefabs assigned in NetworkingConfiguration
- [ ] Satellite components have unique NetworkIdentity IDs
- [ ] Console interaction triggers voice gating
- [ ] Scenes added to Build Settings in correct order
- [ ] Tested full game flow: Lobby → Game → End Game
- [ ] NetworkDebugOverlay works (Tab key)
- [ ] Voice chat tested in all scenes
- [ ] Player spawning works correctly
- [ ] Scene transitions are smooth

---

For prefab configuration, see [PREFAB_SETUP.md](PREFAB_SETUP.md)
