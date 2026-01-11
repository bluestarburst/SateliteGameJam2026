# Mock Steam Testing Guide

## Overview

The mock Steam system simulates a complete multiplayer environment **without Steam running**. This includes:

✅ Remote player spawning  
✅ Position/rotation/velocity updates  
✅ Voice audio data streaming  
✅ All networking systems (state, voice, sync)  

---

## Quick Start

### 1. Enable Mock Mode

```
SteamPack GameObject:
└── NetworkSetup Component
    └── Use Mock Steam: ☑ (check this box)
```

This automatically adds **MockSteamNetworking** component.

### 2. Configure Mock Remote Player

```
MockSteamNetworking Component:
├── Mock Remote Player Simulation
│   ├── Simulate Remote Player: ☑
│   ├── Mock Player Move Radius: 5 (meters)
│   ├── Mock Player Move Speed: 1 (radians/sec)
│   ├── Mock Transform Send Rate: 10 (Hz)
│   ├── Mock Audio Send Rate: 50 (Hz)
│   └── Mock Audio Enabled: ☑
```

### 3. Press Play

The system automatically:
1. Creates mock lobby with 2 players (you + 1 mock peer)
2. Spawns RemotePlayer prefab for mock peer
3. Starts sending transform updates (Channel 1)
4. Starts sending audio data (Channel 2)

---

## What You'll See

### In Scene View

- **RemotePlayer prefab** spawns at `(2, 0, 0)`
- **Circular motion** around origin at configured radius
- **Smooth interpolation** via NetworkTransformSync
- **Real-time position updates** every 0.1 seconds (10 Hz)

### In Console

```
[MockSteam] Initializing mock networking (SteamId: 76561199999999999)
[MockSteam] Created lobby with 2 members
[MockSteam] Initialized remote player simulation
[NetworkConnectionManager] Spawning remote player for 76561199999999999
[VoiceSessionManager] Registered remote player 76561199999999999
```

### In Inspector (Runtime)

Watch the RemotePlayer GameObject:
- **Transform** updates every frame (interpolated)
- **AudioSource** receives voice data packets
- **NetworkIdentity** shows owner SteamId

---

## Mock Data Flow

### Transform Updates (Channel 1)

```
MockSteamNetworking (Update loop)
    ↓ Every 0.1s (10 Hz)
NetworkTransformSync packet
    [Type: 0x10]
    [NetId: Mock SteamId]
    [Position: Circular motion]
    [Rotation: Facing direction]
    [Velocity: Tangential velocity]
    ↓
NetworkConnectionManager (PollChannel 1)
    ↓
NetworkTransformSync.OnReceiveTransformSync()
    ↓
RemotePlayer GameObject (interpolation)
    ↓ Visual result
Smooth circular motion in scene!
```

### Audio Data (Channel 2)

```
MockSteamNetworking (Update loop)
    ↓ Every 0.02s (50 Hz)
VoiceData packet
    [Type: 0x00]
    [SenderSteamId: Mock player]
    [AudioSize: 160 bytes]
    [CompressedAudio: Mock data]
    ↓
VoiceChatP2P (PollChannel 2)
    ↓
VoiceRemotePlayer.OnAudioReceived()
    ↓
AudioSource.Play() on RemotePlayer
    ↓ Audio result
Mock audio plays from RemotePlayer position!
```

---

## Customization

### Change Movement Pattern

Edit in Inspector:
- **Move Radius**: Distance from origin (default: 5m)
- **Move Speed**: Angular velocity (default: 1 rad/s)

Math:
```
Position.x = cos(angle) * radius
Position.z = sin(angle) * radius
angle += moveSpeed * deltaTime
```

Result: Circular motion, period = `2π / moveSpeed` seconds

### Change Network Rates

Edit in Inspector:
- **Transform Send Rate**: Position updates per second (default: 10 Hz)
- **Audio Send Rate**: Voice packets per second (default: 50 Hz)

Impact:
- **Higher rates** = smoother motion, more CPU/bandwidth
- **Lower rates** = choppier motion, less CPU/bandwidth

Recommended:
- Transform: 10-20 Hz (matches real networking)
- Audio: 50-100 Hz (matches Opus codec frame rate)

### Disable Audio Simulation

Uncheck **Mock Audio Enabled** if you only want to test movement.

---

## Testing Scenarios

### Scenario 1: Movement Only

```
Simulate Remote Player: ☑
Mock Audio Enabled: ☐

Result: See remote player move, no audio
Use case: Test position sync, interpolation
```

### Scenario 2: Full Simulation

```
Simulate Remote Player: ☑
Mock Audio Enabled: ☑

Result: See and hear remote player
Use case: Test complete multiplayer flow
```

### Scenario 3: Multiple Mock Players

```csharp
// At runtime, add more mock players
void Start()
{
    if (MockSteamNetworking.Instance != null)
    {
        MockSteamNetworking.Instance.AddMockPlayer("MockPlayer2");
        MockSteamNetworking.Instance.AddMockPlayer("MockPlayer3");
    }
}

// Note: Only first mock player moves automatically
// Others are static (can be extended in future)
```

---

## Verification Checklist

Use this to verify mock mode is working:

### Setup Phase
- [ ] NetworkSetup has "Use Mock Steam" checked
- [ ] MockSteamNetworking component exists on SteamPack
- [ ] "Enable Mock Mode" is checked on MockSteamNetworking
- [ ] "Simulate Remote Player" is checked

### Runtime Phase
- [ ] Console shows "[MockSteam] Initializing mock networking"
- [ ] Console shows "[MockSteam] Created lobby with 2 members"
- [ ] Console shows "[MockSteam] Initialized remote player simulation"

### Remote Player Phase
- [ ] Console shows "[NetworkConnectionManager] Spawning remote player for..."
- [ ] RemotePlayer GameObject appears in Hierarchy
- [ ] RemotePlayer has NetworkIdentity component
- [ ] RemotePlayer has AudioSource component
- [ ] NetworkIdentity shows owner SteamId (not your local ID)

### Movement Phase
- [ ] RemotePlayer position changes every frame
- [ ] Movement is smooth (interpolated)
- [ ] Player follows circular path
- [ ] Player faces direction of movement

### Audio Phase (if enabled)
- [ ] Console shows voice-related logs
- [ ] AudioSource on RemotePlayer is active
- [ ] VoiceRemotePlayer component attached
- [ ] Audio packets logged (if debug enabled)

---

## Troubleshooting

### Remote Player Not Spawning

**Issue**: No RemotePlayer appears in scene

**Fixes**:
1. Check RemotePlayer prefab assigned to NetworkSetup
2. Check "Auto Spawn Players" is enabled
3. Verify console shows "Spawning remote player" message
4. Ensure mock lobby has 2+ members (check MockMemberCount)

### Remote Player Not Moving

**Issue**: RemotePlayer spawns but doesn't move

**Fixes**:
1. Check "Simulate Remote Player" is enabled
2. Verify Move Speed > 0
3. Check console for errors during packet send
4. Ensure NetworkTransformSync component on RemotePlayer

### No Audio Playing

**Issue**: Remote player moves but no audio

**Fixes**:
1. Check "Mock Audio Enabled" is checked
2. Verify AudioSource exists on RemotePlayer prefab
3. Check AudioSource Spatial Blend = 1 (3D)
4. Enable voice debug logs in VoiceSessionManager
5. Verify VoiceRemotePlayer component attached at runtime

### Stuttering Movement

**Issue**: Remote player teleports or stutters

**Fixes**:
1. Increase Transform Send Rate (try 20 Hz)
2. Check NetworkTransformSync interpolation settings
3. Verify no errors in console during packet processing
4. Check frame rate (F key in Game view)

---

## Comparison: Mock vs Real Steam

| Feature | Mock Steam | Real Steam |
|---------|------------|------------|
| **Setup** | Check box in Inspector | Install Steam + App ID |
| **Players** | Simulated | Real users |
| **Movement** | Circular pattern | User input |
| **Audio** | Generated noise | Real microphone |
| **Latency** | Zero (local) | Network dependent |
| **Packets** | Perfect delivery | Can be lost |
| **Testing** | Instant, offline | Requires lobby |

---

## Advanced: Custom Mock Behavior

### Override Movement Pattern

```csharp
// Create custom mock movement script
public class CustomMockMovement : MonoBehaviour
{
    private MockSteamNetworking mockSteam;
    
    void Start()
    {
        mockSteam = MockSteamNetworking.Instance;
    }
    
    void Update()
    {
        if (mockSteam == null || !mockSteam.IsEnabled) return;
        
        // Send custom transform updates
        // (You'll need to access private methods via reflection
        // or make them public for advanced use cases)
    }
}
```

### Inject Custom Packets

```csharp
// Simulate specific network scenarios
void TestCustomPacket()
{
    SteamId mockRemoteId = new SteamId { Value = 76561199999999999 + 1 };
    
    // Create custom packet
    byte[] testPacket = new byte[53];
    testPacket[0] = 0x10; // TransformSync
    // ... fill rest of packet
    
    // Inject into queue
    MockSteamNetworking.Instance.SimulateReceivePacket(
        mockRemoteId, 
        testPacket, 
        1 // Channel
    );
}
```

---

## Summary

The mock Steam system provides:

✅ **Zero setup** - Check one box  
✅ **Offline testing** - No Steam required  
✅ **Visual feedback** - See remote player move  
✅ **Audio simulation** - Hear mock voice data  
✅ **Full integration** - All systems work  
✅ **Configurable** - Adjust rates, radius, speed  
✅ **Realistic** - Matches real packet structure  

Perfect for rapid iteration without lobby management!