# AGENTS.md

This file provides guidance to Codex (Codex.ai/code) when working with code in this repository.

## Project Overview

This is **Satellite Game Jam 2026** - a Unity multiplayer game with role-based gameplay (Ground Control vs. Space Station). Players cooperate across two different scenes with asymmetric roles and voice chat gating.

**Unity Version:** 2023.2+ (URP 17.3)
**Networking:** Steam P2P via Facepunch Steamworks wrapper
**Platform:** PC (Steam)

## Build & Run

This is a Unity project - open in Unity Editor (2023.2+) and use standard Unity workflows:
- **Play in Editor:** Press Play button or Ctrl+P
- **Build:** File > Build Settings > Build
- **Run Tests:** Window > General > Test Runner

No external build commands needed - Unity handles compilation automatically.

## Architecture

### Networking Layer Stack

```
Game Logic (PlayerControllers, UI)
         ↓
GameFlowManager (High-level API abstraction)
         ↓
Scene Managers (LobbyNetworkingManager, etc.)
         ↓
State Managers (PlayerStateManager, SceneSyncManager, SatelliteStateManager)
         ↓
NetworkConnectionManager (Packet routing, handler dispatch)
         ↓
INetworkTransport → SteamP2PTransport (via SteamManager)
```

### Key Directories

- `Assets/Scripts/Networking/Core/` - Transport, connection management, abstractions
- `Assets/Scripts/Networking/Messages/` - Message types and serialization
- `Assets/Scripts/Networking/State/` - Player, scene, and game state tracking
- `Assets/Scripts/Networking/Sync/` - Transform, physics, and interaction sync components
- `Assets/Scripts/Networking/Voice/` - Voice chat and spatial audio
- `Assets/Scripts/SceneManagers/` - Per-scene networking setup

### Game Flow

```
Matchmaking → Lobby (all players together) → Role Assignment → Scene Split
                                                    ↓
                              Ground Control Scene (Ground players)
                              Space Station Scene (Space players)
```

### Network Channels

| Channel | Purpose | Reliability |
|---------|---------|-------------|
| 0 | Control messages (ready, roles) | Reliable |
| 1 | Transform sync | Unreliable (high frequency) |
| 2 | Voice data | Unreliable |
| 3 | Interactions (pickup/drop) | Reliable |
| 4 | Game state (satellite health) | Reliable |

### Core Singletons

- `SteamManager` - Steam P2P connections, lobby management
- `NetworkConnectionManager` - Packet routing, handler registration
- `GameFlowManager` - High-level game API (abstracts networking from game code)
- `PlayerStateManager` - Per-player state tracking (scene, role, ready)
- `SceneSyncManager` - Coordinated scene transitions

### Extensibility Pattern

Messages implement `INetworkMessage` interface and register with `NetworkMessageRegistry`. This allows adding new message types without modifying core networking code.

```csharp
public class CustomMessage : INetworkMessage
{
    public byte MessageTypeId => 0x70;
    public int Channel => 1;
    public bool RequireReliable => false;
    public byte[] Serialize() { /* ... */ }
    public void Deserialize(byte[] data) { /* ... */ }
}
```

## Scenes

- `Matchmaking.unity` - Lobby creation/joining
- `Lobby.unity` - Pre-game staging, role selection
- `BaseStation.unity` - Ground Control gameplay
- `Satellite.unity` - Space Station gameplay

## Configuration

Networking settings are in `Assets/Scripts/Networking/NetworkingConfiguration.asset` - a ScriptableObject with Inspector-editable values for sync rates, voice settings, timeouts, etc.

## Documentation

Detailed docs are in `/Documents/`:
- `networking-states.md` - **Requirements specification** (source of truth)
- `ARCHITECTURE.md` - Current implementation analysis
- `RECOMMENDATIONS.md` - Gap analysis and what needs to be fixed
- `QUICKSTART.md` - Quick reference for common tasks

## Debug Tools

- Press **Tab** in-game to toggle `NetworkDebugOverlay` showing real-time network stats
- Enable verbose logging in `NetworkingConfiguration` for detailed packet tracing
