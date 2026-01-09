# Unity Scene Setup Guide

## SteamPack Prefab Setup

### 1. Create the SteamPack Prefab

1. Create an empty GameObject in your scene: `GameObject > Create Empty`
2. Rename it to **"SteamPack"**
3. Add the following components (in this order):

#### Core Steam Components
- **SteamManager** (Assets/Scripts/Networking/Core/SteamManager.cs)
  - Set your `Game App ID` (use 480 for testing with Spacewar)
  - Configure `Game Scene Name` if you want automatic scene loading
  - Leave `Auto Create Lobby For Testing` unchecked for production

#### Network Management
- **NetworkConnectionManager** (Assets/Scripts/Networking/Core/NetworkConnectionManager.cs)
  - `Channels To Poll`: [0, 1, 3, 4] (should be default)
  - **Optional**: Assign `Player Prefab` if you want automatic remote player spawning
  - Set `Auto Spawn Player` to true if using player prefab

- **ObjectRegistryBridge** (Assets/Scripts/Networking/Core/ObjectRegistryBridge.cs)
  - No configuration needed

#### State Management
- **PlayerStateManager** (Assets/Scripts/Networking/State/PlayerStateManager.cs)
  - No configuration needed
  
- **SceneSyncManager** (Assets/Scripts/Networking/State/SceneSyncManager.cs)
  - Set `Scene Change Timeout` (default: 10 seconds)
  
- **SatelliteStateManager** (Assets/Scripts/Networking/State/SatelliteStateManager.cs)
  - Set `Health` (default: 100)
  - Set `Send Rate` (default: 1 Hz for low-frequency updates)

#### Voice Management
- **VoiceChatP2P** (Assets/Scripts/Networking/Voice/VoiceChatP2P.cs)
  - Assign `Push To Talk Action` (Input System action)
  - Set `Always Record` to false for production
  - `Voice Channel` should be 2 (default)
  
- **VoiceSessionManager** (Assets/Scripts/Networking/Voice/VoiceSessionManager.cs)
  - Set `Space Proximity Radius` (default: 20 units for space-to-space voice)
  - Enable `Debug Logs` for testing

### 2. Make SteamPack a Prefab

1. Drag the configured SteamPack GameObject into your **Prefabs** folder
2. Delete the instance from the scene (it will persist via DontDestroyOnLoad)

### 3. Add SteamPack to Your Startup Scene

- Place the **SteamPack** prefab in your **first loaded scene** (typically Lobby or Main Menu)
- It will persist across all scene transitions via `DontDestroyOnLoad`

---

## Scene-Specific Setup

### Lobby Scene

#### Setup
1. Add UI for lobby creation/joining (use existing CreateLobbyButton, UnrankedLobbiesView)
2. Add LobbyPlayersView to display current lobby members

#### Voice Setup
```csharp
// In your lobby initialization script
void Start()
{
    if (PlayerStateManager.Instance != null)
    {
        PlayerStateManager.Instance.SetLocalPlayerRole(PlayerRole.Lobby);
        PlayerStateManager.Instance.SetLocalPlayerScene(NetworkSceneId.Lobby);
    }
}
```

**Voice Behavior**: Everyone hears everyone (VoiceSessionManager automatically allows all voice in Lobby role)

---

### Ground Control Scene

#### Console Setup

1. Create a console GameObject with a **Collider** (trigger)
2. Add a script to detect player interaction:

```csharp
using UnityEngine;
using SatelliteGameJam.Networking.Voice;
using SatelliteGameJam.Networking.State;
using SatelliteGameJam.Networking.Messages;

public class TransmissionConsole : MonoBehaviour
{
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private float interactionRadius = 2f;
    
    private bool isPlayerNearby = false;
    private bool isPlayerInteracting = false;
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNearby = true;
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNearby = false;
            StopInteracting();
        }
    }
    
    private void Update()
    {
        if (isPlayerNearby && Input.GetKeyDown(interactKey) && !isPlayerInteracting)
        {
            StartInteracting();
        }
        else if (isPlayerInteracting && Input.GetKeyUp(interactKey))
        {
            StopInteracting();
        }
    }
    
    private void StartInteracting()
    {
        isPlayerInteracting = true;
        
        // Enable voice for space station communication
        if (VoiceSessionManager.Instance != null)
        {
            VoiceSessionManager.Instance.SetLocalPlayerAtConsole(true);
        }
        
        // Optional: Update UI, play sound, show indicator
        Debug.Log("Console activated - can now communicate with space station");
    }
    
    private void StopInteracting()
    {
        isPlayerInteracting = false;
        
        // Disable voice for space station communication
        if (VoiceSessionManager.Instance != null)
        {
            VoiceSessionManager.Instance.SetLocalPlayerAtConsole(false);
        }
        
        Debug.Log("Console deactivated");
    }
}
```

#### Scene Initialization

```csharp
using SatelliteGameJam.Networking.State;
using SatelliteGameJam.Networking.Messages;

public class GroundControlSceneManager : MonoBehaviour
{
    void Start()
    {
        // Set player role and scene
        if (PlayerStateManager.Instance != null)
        {
            PlayerStateManager.Instance.SetLocalPlayerRole(PlayerRole.GroundControl);
            PlayerStateManager.Instance.SetLocalPlayerScene(NetworkSceneId.GroundControl);
        }
        
        // Subscribe to satellite state changes for UI updates
        if (SatelliteStateManager.Instance != null)
        {
            SatelliteStateManager.Instance.OnHealthChanged += UpdateHealthUI;
            SatelliteStateManager.Instance.OnComponentDamaged += OnComponentDamaged;
            SatelliteStateManager.Instance.OnConsoleStateChanged += OnConsoleStateChanged;
        }
    }
    
    private void UpdateHealthUI(float health)
    {
        // Update your UI health bar/display
        Debug.Log($"Satellite health: {health}%");
    }
    
    private void OnComponentDamaged(uint componentIndex)
    {
        Debug.Log($"Component {componentIndex} damaged!");
        // Show damage indicator on UI
    }
    
    private void OnConsoleStateChanged(uint consoleId, ConsoleStateData state)
    {
        Debug.Log($"Console {consoleId} state changed: {state.StateByte}");
        // Update console screen/display
    }
}
```

**Voice Behavior**: 
- Can **only** hear space players when at console (interacting)
- Can **always** hear other ground control players

---

### Space Station Scene

#### Scene Initialization

```csharp
using SatelliteGameJam.Networking.State;
using SatelliteGameJam.Networking.Messages;
using UnityEngine;

public class SpaceStationSceneManager : MonoBehaviour
{
    void Start()
    {
        // Set player role and scene
        if (PlayerStateManager.Instance != null)
        {
            PlayerStateManager.Instance.SetLocalPlayerRole(PlayerRole.SpaceStation);
            PlayerStateManager.Instance.SetLocalPlayerScene(NetworkSceneId.SpaceStation);
        }
        
        // Space players are authority for satellite state changes
        // Example: Damage a component when player interacts
    }
    
    // Example: Player repairs a component
    public void RepairComponent(int componentIndex)
    {
        if (SatelliteStateManager.Instance != null)
        {
            SatelliteStateManager.Instance.SetComponentRepaired(componentIndex);
        }
    }
    
    // Example: Satellite takes damage
    public void DamageSatellite(float damage)
    {
        if (SatelliteStateManager.Instance != null)
        {
            float currentHealth = SatelliteStateManager.Instance.GetHealth();
            SatelliteStateManager.Instance.SetHealth(currentHealth - damage);
        }
    }
}
```

**Voice Behavior**:
- Can **always** hear ground control players
- Can hear other space players **within proximity radius** (default 20 units)

---

## Networked Object Setup

### 1. Synced Transform Objects (Player-Held Items)

For objects that players own and move (tools, flashlights, etc.):

```csharp
// Add to your object:
1. NetworkIdentity component
2. NetworkTransformSync component
3. Rigidbody (optional, for velocity sync)

// In your pickup script:
using SatelliteGameJam.Networking.Sync;

void PickupItem(GameObject item)
{
    var interaction = item.GetComponent<NetworkInteractionState>();
    if (interaction != null)
    {
        bool success = interaction.TryPickup(SteamManager.Instance.PlayerSteamId);
        if (success)
        {
            // Attach to player hand/inventory
            item.transform.SetParent(playerHand);
        }
    }
}

void DropItem(GameObject item, Vector3 dropPosition, Vector3 dropVelocity)
{
    var interaction = item.GetComponent<NetworkInteractionState>();
    if (interaction != null)
    {
        interaction.Drop(dropPosition, dropVelocity);
        item.transform.SetParent(null);
    }
}
```

### 2. Free Physics Objects (Soccer Ball, etc.)

For objects with physics that multiple players can interact with:

```csharp
// Add to your object:
1. NetworkIdentity component (assign unique ID in inspector)
2. NetworkPhysicsObject component
3. Rigidbody component

// Settings on NetworkPhysicsObject:
- Send Rate: 10 Hz (default)
- Authority Handoff Cooldown: 0.2s (prevents ping-pong)

// No code needed - authority automatically transfers on collision
```

### 3. Interactable Objects (Switches, Buttons, Levers)

```csharp
using SatelliteGameJam.Networking.Sync;
using Steamworks;

public class NetworkedSwitch : MonoBehaviour
{
    private NetworkInteractionState interaction;
    [SerializeField] private bool switchState = false;
    
    void Awake()
    {
        interaction = GetComponent<NetworkInteractionState>();
        interaction.OnUsed += OnSwitchUsed;
    }
    
    public void ToggleSwitch()
    {
        interaction.Use(SteamManager.Instance.PlayerSteamId);
    }
    
    private void OnSwitchUsed(SteamId userId)
    {
        // Toggle switch for all players
        switchState = !switchState;
        UpdateSwitchVisual(switchState);
        
        Debug.Log($"Switch toggled by {userId} - State: {switchState}");
    }
    
    private void UpdateSwitchVisual(bool state)
    {
        // Update switch appearance/animation
    }
}

// Add to GameObject:
1. NetworkIdentity (assign unique ID)
2. NetworkInteractionState
3. This script
```

### 4. Satellite Parts (Rotating Solar Panels, etc.)

```csharp
using SatelliteGameJam.Networking.State;
using UnityEngine;

public class SolarPanel : MonoBehaviour
{
    [SerializeField] private uint partId = 1; // Unique ID for this part
    [SerializeField] private float rotationSpeed = 10f;
    
    private void Start()
    {
        // Only authority updates part transforms
        if (SatelliteStateManager.Instance != null)
        {
            SatelliteStateManager.Instance.OnPartTransformChanged += OnPartTransformReceived;
        }
    }
    
    private void Update()
    {
        if (IsAuthority())
        {
            // Rotate solar panel
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
            
            // Broadcast transform at low rate (managed by SatelliteStateManager)
            if (Time.frameCount % 60 == 0) // Every 60 frames (~1 second at 60fps)
            {
                SatelliteStateManager.Instance.SetPartTransform(
                    partId, 
                    transform.position, 
                    transform.rotation
                );
            }
        }
    }
    
    private void OnPartTransformReceived(uint receivedPartId, PartTransformData data)
    {
        if (receivedPartId == partId && !IsAuthority())
        {
            // Apply received transform
            transform.position = data.Position;
            transform.rotation = data.Rotation;
        }
    }
    
    private bool IsAuthority()
    {
        // Use same authority logic as SatelliteStateManager (lowest SteamId)
        if (SteamManager.Instance?.currentLobby == null) return false;
        var members = SteamManager.Instance.currentLobby.Members;
        if (!members.Any()) return false;
        return members.Min(m => m.Id) == SteamManager.Instance.PlayerSteamId;
    }
}
```

---

## Remote Player Avatar Setup

For proper positional voice audio, you need to register remote player avatars:

```csharp
using SatelliteGameJam.Networking.Voice;
using SatelliteGameJam.Networking.State;
using Steamworks;
using UnityEngine;

public class RemotePlayerSpawner : MonoBehaviour
{
    [SerializeField] private GameObject remotePlayerPrefab;
    
    void Start()
    {
        // Subscribe to player join events
        if (PlayerStateManager.Instance != null)
        {
            PlayerStateManager.Instance.OnPlayerJoined += OnPlayerJoined;
            PlayerStateManager.Instance.OnPlayerLeft += OnPlayerLeft;
        }
    }
    
    private void OnPlayerJoined(SteamId steamId)
    {
        // Skip local player
        if (steamId == SteamManager.Instance.PlayerSteamId) return;
        
        // Spawn remote player avatar
        GameObject avatar = Instantiate(remotePlayerPrefab);
        avatar.name = $"RemotePlayer_{steamId}";
        
        // Register with VoiceSessionManager for positional audio
        if (VoiceSessionManager.Instance != null)
        {
            VoiceSessionManager.Instance.RegisterRemotePlayerAvatar(steamId, avatar);
        }
    }
    
    private void OnPlayerLeft(SteamId steamId)
    {
        // Clean up remote player
        if (VoiceSessionManager.Instance != null)
        {
            VoiceSessionManager.Instance.UnregisterRemotePlayer(steamId);
        }
        
        // Destroy avatar GameObject
        GameObject avatar = GameObject.Find($"RemotePlayer_{steamId}");
        if (avatar != null) Destroy(avatar);
    }
}
```

---

## Testing Checklist

### Single Machine Testing (Two Builds)
1. Build your game twice (or use Editor + Build)
2. Both instances must use same Steam App ID
3. Use Steam's "Run Game as Different User" feature
4. Or use separate Steam accounts on different machines

### Test Sequence
- [ ] Lobby voice works (all hear all)
- [ ] Scene transitions sync for all players
- [ ] Ground control voice only works at console
- [ ] Space-to-space proximity voice works
- [ ] Ground-to-space voice bidirectional when at console
- [ ] Physics objects sync properly
- [ ] Pickup/drop/use interactions replicate
- [ ] Satellite health/components update on all clients
- [ ] Console states sync

---

## Performance Tips

1. **Voice Proximity**: Adjust `Space Proximity Radius` in VoiceSessionManager based on your scene size
2. **Physics Sync Rate**: Keep NetworkPhysicsObject at 10Hz for smooth sync without overwhelming bandwidth
3. **Satellite Updates**: SatelliteStateManager uses 1Hz by default for low-frequency state - increase only if needed
4. **Channel Separation**: High-frequency data on Channel 1, reliable events on Channel 3, state on Channel 4

---

## Common Issues

### Voice Not Working
- Check `Push To Talk Action` is assigned in VoiceChatP2P
- Verify players are in correct role (Lobby/GroundControl/SpaceStation)
- Ensure VoiceSessionManager has registered remote player avatars
- Check AudioSource is enabled on VoiceRemotePlayer components

### Objects Not Syncing
- Verify NetworkIdentity has unique Network ID
- Check NetworkConnectionManager is polling correct channels
- Ensure object has appropriate sync component (Transform/Physics/Interaction)

### Scene Transitions Fail
- Confirm SceneSyncManager is present in SteamPack
- Check Unity scene names match NetworkSceneId enum values
- Verify all players are in same lobby before transitioning

### Authority Conflicts
- SatelliteStateManager uses lowest SteamId as authority by default
- NetworkPhysicsObject uses last-touch authority
- NetworkInteractionState uses first-come-first-served for pickup
