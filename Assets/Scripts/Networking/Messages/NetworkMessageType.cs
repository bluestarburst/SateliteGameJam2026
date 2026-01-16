namespace SatelliteGameJam.Networking.Messages
{
    /// <summary>
    /// Defines the types of network messages that can be sent between peers.
    /// Used as the first byte of every network packet to identify message type.
    /// </summary>
    public enum NetworkMessageType : byte
{
    // Voice (Channel 2)
    VoiceData = 0x00,           // [SenderSteamId(8)][CompressedAudio(N)]
    
    // Control (Channel 0 - Reliable)
    PlayerReady = 0x01,                  // [SteamId(8)]
    SceneChangeRequest = 0x02,           // [SteamId(8)][SceneId(2)][Timestamp(4)]
    SceneChangeAcknowledge = 0x03,       // [SteamId(8)][SceneId(2)]
    RoleAssign = 0x04,                   // [SteamId(8)][Role(1)]
    
    // Transform/Physics (Channel 1)
    TransformSync = 0x10,        // [NetId(4)][OwnerSteamId(8)][Pos(12)][Rot(12)][Vel(12)]
    PhysicsSync = 0x11,          // [NetId(4)][AuthSteamId(8)][Pos(12)][Rot(12)][Vel(12)][AngVel(12)]
    
    // Interactions (Channel 3 - Reliable)
    InteractionPickup = 0x20,    // [NetId(4)][OwnerSteamId(8)]
    InteractionDrop = 0x21,      // [NetId(4)][OwnerSteamId(8)][Pos(12)][Vel(12)]
    InteractionUse = 0x22,       // [NetId(4)][UserSteamId(8)]
    
    // Authority changes
    AuthorityRequest = 0x30,     // [NetId(4)][RequesterSteamId(8)]
    AuthorityGrant = 0x31,       // [NetId(4)][NewAuthSteamId(8)]
    
    // Presence/State (Channel 4 - Low Frequency)
    PlayerSceneState = 0x40,             // [SteamId(8)][SceneId(2)][Role(1)][Timestamp(4)]
    PlayerProximityHint = 0x41,          // [SteamId(8)][SceneId(2)][Pos(12)]
    SatelliteHealth = 0x42,              // [Health(4)][DamageBits(4)]
    SatellitePartTransform = 0x43,       // [PartId(4)][Pos(12)][Rot(16)]
    ConsoleState = 0x44,                 // [ConsoleId(4)][StateByte(1)][Payload(N)]
    ConsoleInteraction = 0x45,           // [SteamId(8)][AtConsole(1)]
    
    // Late-join synchronization (Channel 0 - Reliable)
    StateSnapshotRequest = 0x50,         // [RequesterSteamId(8)]
    StateSnapshotResponse = 0x51,        // [Health(4)][DamageBits(4)][ConsoleCount(2)][ConsoleData(N)][PlayerCount(1)][PlayerData(N)]
}

/// <summary>
/// Player role types for scene/voice gating
/// </summary>
public enum PlayerRole : byte
{
    None = 0,
    Lobby = 1,
    GroundControl = 2,
    SpaceStation = 3
}

/// <summary>
/// Scene identifiers for network state tracking
/// </summary>
    public enum NetworkSceneId : ushort
    {
        None = 0,
        Lobby = 1,
        GroundControl = 2,
        SpaceStation = 3
    }
}
