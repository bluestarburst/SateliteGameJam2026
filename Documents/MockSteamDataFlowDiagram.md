# Mock Steam Data Flow Diagram

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         MOCK STEAM SYSTEM                                │
│                         (Offline Testing)                                │
└─────────────────────────────────────────────────────────────────────────┘

                              ┌──────────────┐
                              │ Press Play   │
                              └──────┬───────┘
                                     │
                                     ▼
                        ┌────────────────────────┐
                        │  MockSteamNetworking   │
                        │     (Awake)            │
                        │                        │
                        │ • Create mock lobby    │
                        │ • Add 2 players        │
                        │ • Init simulation      │
                        └────────┬───────────────┘
                                 │
                 ┌───────────────┴───────────────┐
                 │                               │
                 ▼                               ▼
      ┌──────────────────┐          ┌───────────────────┐
      │ NetworkConnection│          │  SteamManager     │
      │    Manager       │          │  (mock lobby)     │
      │                  │          │                   │
      │ Auto-spawns      │          │ Members:          │
      │ RemotePlayer     │          │ • You             │
      │ prefab           │          │ • MockPlayer1     │
      └────────┬─────────┘          └───────────────────┘
               │
               ▼
    ┌────────────────────┐
    │  RemotePlayer      │
    │  GameObject        │
    │                    │
    │ Components:        │
    │ • NetworkIdentity  │
    │ • AudioSource      │
    │ • (Visual mesh)    │
    └────────┬───────────┘
             │
             │ (Ready to receive data)
             │
┌────────────▼─────────────────────────────────────────────────────────────┐
│                        UPDATE LOOP (Every Frame)                          │
└───────────────────────────────────────────────────────────────────────────┘

  ┌─────────────────────────────────────────────────────────────────────┐
  │ MockSteamNetworking.Update()                                        │
  │                                                                     │
  │ 1. Calculate position (circular motion)                            │
  │    mockPlayerPosition = (cos(θ) * R, 0, sin(θ) * R)               │
  │    θ += speed * deltaTime                                          │
  │                                                                     │
  │ 2. Check if time to send transform                                 │
  │    if (Time.time >= nextTransformSendTime)                         │
  │       SendMockTransformUpdate()                                     │
  │       nextTransformSendTime = Time.time + (1/10)                   │
  │                                                                     │
  │ 3. Check if time to send audio                                     │
  │    if (Time.time >= nextAudioSendTime)                             │
  │       SendMockAudioData()                                           │
  │       nextAudioSendTime = Time.time + (1/50)                       │
  └─────────────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────────────────┐
│                    TRANSFORM DATA FLOW (10 Hz)                           │
└──────────────────────────────────────────────────────────────────────────┘

  MockSteamNetworking.SendMockTransformUpdate()
        │
        │ Create packet: [0x10][NetId][OwnerSteamId][Pos][Rot][Vel]
        │ Size: 53 bytes
        │ Channel: 1 (Unreliable)
        │
        ▼
  SimulateReceivePacket(mockRemoteId, packet, 1)
        │
        │ Enqueue to channelQueues[1]
        │
        ▼
  NetworkConnectionManager.Update()
        │
        │ PollChannel(1)
        │
        ▼
  SteamNetworking.ReadP2PPacket(1)
        │
        │ Returns MockP2PPacket
        │
        ▼
  NetworkConnectionManager.RoutePacket()
        │
        │ Parse msgType = 0x10 (TransformSync)
        │
        ▼
  NetworkTransformSync.OnReceiveTransformSync(sender, data)
        │
        │ Deserialize:
        │ • targetPosition
        │ • targetRotation
        │ • targetVelocity
        │
        ▼
  NetworkTransformSync.Update()
        │
        │ if (!IsOwner())
        │     InterpolateToTarget()
        │
        ▼
  RemotePlayer.transform (VISUAL RESULT)
        │
        │ Smooth circular motion
        │ Position updates interpolated
        │ Rotation faces movement direction
        │
        ▼
  👁️ You see the player move!

┌──────────────────────────────────────────────────────────────────────────┐
│                      AUDIO DATA FLOW (50 Hz)                             │
└──────────────────────────────────────────────────────────────────────────┘

  MockSteamNetworking.SendMockAudioData()
        │
        │ Create packet: [0x00][SenderSteamId][AudioSize][CompressedAudio]
        │ Size: 1 + 8 + 2 + 160 = 171 bytes
        │ Channel: 2 (Voice)
        │
        ▼
  SimulateReceivePacket(mockRemoteId, packet, 2)
        │
        │ Enqueue to channelQueues[2]
        │
        ▼
  VoiceChatP2P.Update()
        │
        │ PollChannel(2)
        │
        ▼
  SteamNetworking.ReadP2PPacket(2)
        │
        │ Returns MockP2PPacket
        │
        ▼
  VoiceChatP2P.OnReceiveVoiceData(sender, data)
        │
        │ Decompress audio (or use raw)
        │
        ▼
  VoiceRemotePlayer.OnAudioReceived(audioData)
        │
        │ Convert to float[]
        │ Send to AudioSource
        │
        ▼
  AudioSource.Play() on RemotePlayer
        │
        │ Spatial audio (3D)
        │ Position = RemotePlayer.transform.position
        │
        ▼
  🔊 You hear the mock player!

┌──────────────────────────────────────────────────────────────────────────┐
│                    PARALLEL SYSTEMS (Working Together)                    │
└──────────────────────────────────────────────────────────────────────────┘

  PlayerStateManager
        │
        │ Tracks mock player's scene/role
        │ Fires OnPlayerJoined event
        │ Fires OnPlayerSceneChanged event
        │
        ▼
  VoiceSessionManager
        │
        │ Registers RemotePlayer avatar
        │ Applies voice gating rules
        │ Enables/disables AudioSource
        │
        ▼
  SceneSyncManager
        │
        │ Can transition mock player to scenes
        │ Handles collective scene changes
        │
        ▼
  SatelliteStateManager
        │
        │ Mock player can interact with satellite
        │ Authority transfers work normally

┌──────────────────────────────────────────────────────────────────────────┐
│                         TIMING DIAGRAM                                    │
└──────────────────────────────────────────────────────────────────────────┘

Time (seconds)  │  Events
─────────────────┼────────────────────────────────────────────────────────
0.00            │  [Init] Mock lobby created
                │  [Spawn] RemotePlayer prefab instantiated
                │  [Audio] VoiceRemotePlayer component added
                │
0.10            │  [Xform] Transform update sent (10 Hz)
                │         Position: (2.0, 0, 0)
                │
0.12            │  [Audio] Audio packet sent (50 Hz)
                │  [Audio] Audio packet sent
                │
0.14            │  [Audio] Audio packet sent
                │
0.16            │  [Audio] Audio packet sent
                │
0.18            │  [Audio] Audio packet sent
                │
0.20            │  [Xform] Transform update sent
                │         Position: (1.98, 0, 0.2)
                │  [Audio] Audio packet sent
                │
0.22            │  [Audio] Audio packet sent
                │
...             │  (continues every frame)

┌──────────────────────────────────────────────────────────────────────────┐
│                    CONFIGURATION REFERENCE                                │
└──────────────────────────────────────────────────────────────────────────┘

MockSteamNetworking Inspector Settings:

Enable Mock Mode: ☑
    └─> Activates entire system

Mock Player Name: "TestPlayer"
    └─> Your local player name

Mock Steam Id: 76561199999999999
    └─> Your local Steam ID

Mock Lobby Member Count: 2
    └─> Total players (you + 1 mock)

Auto Create Mock Peers: ☑
    └─> Creates (count - 1) mock players

Simulate Remote Player: ☑
    └─> Enables movement/audio simulation

Mock Player Move Radius: 5.0
    └─> Circle radius in meters

Mock Player Move Speed: 1.0
    └─> Angular velocity (rad/s)
    └─> Period = 2π seconds (~6.28s per lap)

Mock Transform Send Rate: 10.0
    └─> Position updates per second (Hz)
    └─> Interval = 0.1s

Mock Audio Send Rate: 50.0
    └─> Voice packets per second (Hz)
    └─> Interval = 0.02s

Mock Audio Enabled: ☑
    └─> Send voice data packets

┌──────────────────────────────────────────────────────────────────────────┐
│                         PACKET STRUCTURES                                 │
└──────────────────────────────────────────────────────────────────────────┘

TransformSync Packet (Channel 1, 53 bytes):
┌────┬──────┬────────────┬──────────┬─────────┬──────────┐
│Type│NetId │OwnerSteamId│ Position │ Rotation│ Velocity │
├────┼──────┼────────────┼──────────┼─────────┼──────────┤
│ 1  │  4   │     8      │    12    │   16    │    12    │
│0x10│uint  │   ulong    │Vector3   │Quaternion│Vector3  │
└────┴──────┴────────────┴──────────┴─────────┴──────────┘

VoiceData Packet (Channel 2, 171 bytes):
┌────┬────────────┬──────────┬─────────────────────┐
│Type│SenderSteamId│AudioSize │  CompressedAudio    │
├────┼────────────┼──────────┼─────────────────────┤
│ 1  │     8      │    2     │        160          │
│0x00│   ulong    │  ushort  │   byte[160]         │
└────┴────────────┴──────────┴─────────────────────┘

┌──────────────────────────────────────────────────────────────────────────┐
│                          TESTING CHECKLIST                                │
└──────────────────────────────────────────────────────────────────────────┘

□ NetworkSetup has "Use Mock Steam" checked
□ MockSteamNetworking component exists
□ "Enable Mock Mode" is checked
□ "Simulate Remote Player" is checked
□ RemotePlayer prefab assigned to NetworkSetup
□ Press Play

Expected Results:
□ Console: "[MockSteam] Initializing mock networking"
□ Console: "[MockSteam] Created lobby with 2 members"
□ Console: "[MockSteam] Initialized remote player simulation"
□ Console: "[NetworkConnectionManager] Spawning remote player for..."
□ Hierarchy: RemotePlayer GameObject appears
□ Scene View: RemotePlayer moves in circle
□ Inspector: RemotePlayer transform changes
□ Inspector: AudioSource shows audio data (if enabled)

Success! 🎉 Mock multiplayer working offline!
```