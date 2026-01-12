# Quick Start Guide

## Running the Game

1. Open project in Unity 2023.2+
2. Open `Matchmaking` scene (`Assets/Scenes/Matchmaking.unity`)
3. Press Play
4. Create or join a lobby
5. Players select roles and ready up in Lobby scene
6. Host starts game, players split into Ground Control / Space Station scenes

## Debug Tools

- **Tab** - Toggle network debug overlay (shows connected peers, packet stats, role info)
- Enable `verboseLogging` in NetworkingConfiguration for detailed packet logs

---

## Common Tasks

### Add a New Synced Object

1. Add `NetworkIdentity` component
2. Add one of:
   - `NetworkTransformSync` - for player-owned objects
   - `NetworkPhysicsObject` - for physics objects with last-touch authority
   - `NetworkInteractionState` - for pickup/drop/use objects

```csharp
// Example: Make an object syncable
[RequireComponent(typeof(NetworkIdentity))]
[RequireComponent(typeof(NetworkPhysicsObject))]
public class SyncedTool : MonoBehaviour { }
```

### Handle Console Interaction (Voice Gating)

```csharp
// In your console interaction script
public class TransmissionConsole : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            var sceneManager = FindFirstObjectByType<GroundControlSceneManager>();
            sceneManager?.OnConsoleInteractionStarted();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            var sceneManager = FindFirstObjectByType<GroundControlSceneManager>();
            sceneManager?.OnConsoleInteractionEnded();
        }
    }
}
```

### Update Satellite Health

```csharp
// Damage satellite (authority only)
SatelliteStateManager.Instance?.SetHealth(
    SatelliteStateManager.Instance.GetHealth() - 10f);

// Mark component as damaged
SatelliteStateManager.Instance?.SetComponentDamaged(componentIndex);

// Mark component as repaired
SatelliteStateManager.Instance?.SetComponentRepaired(componentIndex);
```

### Listen to State Changes

```csharp
void Start()
{
    // Player events
    PlayerStateManager.Instance.OnPlayerJoined += OnPlayerJoined;
    PlayerStateManager.Instance.OnPlayerLeft += OnPlayerLeft;
    PlayerStateManager.Instance.OnRoleChanged += OnRoleChanged;

    // Satellite events
    SatelliteStateManager.Instance.OnHealthChanged += OnHealthChanged;
    SatelliteStateManager.Instance.OnComponentDamaged += OnDamaged;
}

void OnPlayerJoined(SteamId steamId) { /* ... */ }
void OnHealthChanged(float health) { /* ... */ }
```

### Create Custom Network Message

```csharp
public class MyCustomMessage : INetworkMessage
{
    public byte MessageTypeId => 0x70; // Unique ID (0x50+ for custom)
    public int Channel => 0;            // 0=reliable, 1=transform, 3=interactions
    public bool RequireReliable => true;

    public int MyData { get; set; }

    public byte[] Serialize()
    {
        byte[] packet = new byte[5];
        packet[0] = MessageTypeId;
        Buffer.BlockCopy(BitConverter.GetBytes(MyData), 0, packet, 1, 4);
        return packet;
    }

    public void Deserialize(byte[] data)
    {
        MyData = BitConverter.ToInt32(data, 1);
    }
}

// Register handler
NetworkConnectionManager.Instance.RegisterHandler<MyCustomMessage>(OnMyMessage);

// Send
var msg = new MyCustomMessage { MyData = 42 };
NetworkConnectionManager.Instance.SendMessageToAll(msg);
```

---

## Channel Reference

| Channel | Use For | Reliability |
|---------|---------|-------------|
| 0 | Control messages, state | Reliable |
| 1 | Transforms, physics | Unreliable (high freq) |
| 2 | Voice (handled by VoiceChatP2P) | Unreliable |
| 3 | Interactions | Reliable |
| 4 | Satellite state | Reliable |

## Message Type Ranges

| Range | Purpose |
|-------|---------|
| 0x00 | Voice |
| 0x01-0x0F | Control messages |
| 0x10-0x1F | Transform/Physics |
| 0x20-0x2F | Interactions |
| 0x30-0x3F | Authority |
| 0x40-0x4F | State sync |
| 0x50+ | Custom game messages |

---

## Key Singletons

| Singleton | Access | Purpose |
|-----------|--------|---------|
| `SteamManager.Instance` | Lobby, Steam ID | `SteamManager.Instance.PlayerSteamId` |
| `NetworkConnectionManager.Instance` | Send/receive packets | `SendToAll()`, `RegisterHandler()` |
| `NetworkSyncManager.Instance` | Sync dispatcher | Handles centralized sync message routing |
| `PlayerStateManager.Instance` | Player state | `GetPlayerState()`, `SetLocalPlayerRole()` |
| `SatelliteStateManager.Instance` | Satellite state | `GetHealth()`, `SetComponentDamaged()` |
| `VoiceSessionManager.Instance` | Voice routing | `SetLocalPlayerAtConsole()` |
| `NetworkingConfiguration.Instance` | Config values | All networking settings |

---

## Scene Setup Checklist

### Any Networked Scene
- [ ] `SteamPack` prefab in scene (contains all managers including NetworkSyncManager)

### Lobby Scene
- [ ] `LobbyNetworkingManager` component on a GameObject
- [ ] UI for ready button, role selection, player list

### Ground Control Scene
- [ ] `GroundControlSceneManager` component
- [ ] Transmission console with trigger to call `OnConsoleInteractionStarted/Ended`
- [ ] UI to display satellite health

### Space Station Scene
- [ ] `SpaceStationSceneManager` component
- [ ] Interactable objects with `NetworkInteractionState`
- [ ] Repair points

---

## Troubleshooting

### Players not seeing each other
1. Check both are in same Steam lobby
2. Verify `NetworkingConfiguration.autoSpawnPlayers` is true
3. Check `remotePlayerPrefab` is assigned
4. Enable verbose logging to see spawn messages

### Voice not working
1. Verify microphone permissions
2. Check push-to-talk key (default: V)
3. Verify role-based gating rules (Ground Control needs to be at console)
4. Check `VoiceSessionManager` is in scene

### Objects not syncing
1. Verify `NetworkIdentity` component exists
2. Check `NetworkId` is set (non-zero)
3. Verify sync component (TransformSync, PhysicsObject) is attached
4. Check owner SteamId is set correctly

### Satellite state not updating
1. Only authority (lowest SteamId) can modify state
2. Check `SatelliteStateManager` is initialized
3. Verify you're calling the correct methods (SetHealth, etc.)
