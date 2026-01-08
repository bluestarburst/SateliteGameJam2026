/// <summary>
/// Defines the types of network messages that can be sent between peers.
/// Used as the first byte of every network packet to identify message type.
/// </summary>
public enum NetworkMessageType : byte
{
    // Voice (Channel 2)
    VoiceData = 0x00,           // [SenderSteamId(8)][CompressedAudio(N)]
    
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
}
