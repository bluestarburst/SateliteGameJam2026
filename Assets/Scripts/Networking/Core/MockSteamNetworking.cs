using System;
using System.Collections.Generic;
using Steamworks;
using Steamworks.Data;
using UnityEngine;

namespace SatelliteGameJam.Networking.Core
{
    /// <summary>
    /// Mock implementation of Steam P2P networking for offline testing.
    /// Simulates lobby creation, P2P packet sending/receiving without Steam.
    /// Enable via NetworkSettings inspector.
    /// </summary>
    public class MockSteamNetworking : MonoBehaviour
    {
        public static MockSteamNetworking Instance { get; private set; }

        [Header("Mock Player Configuration")]
        [SerializeField] private bool enableMockMode = false;
        [SerializeField] private string mockPlayerName = "TestPlayer";
        [SerializeField] private ulong mockSteamId = 76561199999999999;

        [Header("Mock Lobby")]
        [SerializeField] private int mockLobbyMemberCount = 2;
        [SerializeField] private bool autoCreateMockPeers = true;

        [Header("Mock Remote Player Simulation")]
        [SerializeField] private bool simulateRemotePlayer = true;
        [SerializeField] private float mockPlayerMoveRadius = 5f;
        [SerializeField] private float mockPlayerMoveSpeed = 1f;
        [SerializeField] private float mockTransformSendRate = 10f; // Hz
        [SerializeField] private float mockAudioSendRate = 50f; // Hz
        [SerializeField] private bool mockAudioEnabled = true;

        // Mock state
        private Dictionary<int, Queue<MockPacket>> channelQueues = new();
        private List<MockMember> mockMembers = new();
        private bool isInitialized = false;

        // Mock remote player simulation
        private float nextTransformSendTime;
        private float nextAudioSendTime;
        private Vector3 mockPlayerPosition;
        private Quaternion mockPlayerRotation;
        private float mockPlayerMoveAngle;

        public bool IsEnabled => enableMockMode;
        public SteamId MockLocalSteamId => mockSteamId;
        public string MockPlayerName => mockPlayerName;
        public int MockMemberCount => mockMembers.Count;

        private struct MockPacket
        {
            public SteamId SenderId;
            public byte[] Data;
            public int Channel;
        }

        private class MockMember
        {
            public SteamId Id;
            public string Name;
        }

        public class MockLobbyMember
        {
            public SteamId Id { get; set; }
            public string Name { get; set; }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (enableMockMode)
            {
                InitializeMockSystem();
            }
        }

        private void InitializeMockSystem()
        {
            Debug.Log($"[MockSteam] Initializing mock networking (SteamId: {mockSteamId})");

            // Initialize channel queues
            for (int i = 0; i < 5; i++)
            {
                channelQueues[i] = new Queue<MockPacket>();
            }

            // Add local player
            mockMembers.Add(new MockMember
            {
                Id = mockSteamId,
                Name = mockPlayerName
            });

            // Add mock peers if enabled
            if (autoCreateMockPeers)
            {
                for (int i = 1; i < mockLobbyMemberCount; i++)
                {
                    mockMembers.Add(new MockMember
                    {
                        Id = mockSteamId + (ulong)i,
                        Name = $"MockPlayer{i}"
                    });
                }
            }

            isInitialized = true;
            
            // Initialize mock remote player simulation
            if (simulateRemotePlayer && mockMembers.Count > 1)
            {
                mockPlayerPosition = new Vector3(2f, 0f, 0f);
                mockPlayerRotation = Quaternion.identity;
                mockPlayerMoveAngle = 0f;
                Debug.Log($"[MockSteam] Initialized remote player simulation");
            }

            Debug.Log($"[MockSteam] Created lobby with {mockMembers.Count} members");
        }

        private void Update()
        {
            if (!enableMockMode || !isInitialized || !simulateRemotePlayer) return;
            if (mockMembers.Count < 2) return;

            // Simulate a remote player sending data
            SimulateMockRemotePlayer();
        }

        private void SimulateMockRemotePlayer()
        {
            SteamId mockRemoteId = mockMembers[1].Id; // First mock peer

            // Update mock player position (circular motion)
            mockPlayerMoveAngle += mockPlayerMoveSpeed * Time.deltaTime;
            mockPlayerPosition = new Vector3(
                Mathf.Cos(mockPlayerMoveAngle) * mockPlayerMoveRadius,
                0f,
                Mathf.Sin(mockPlayerMoveAngle) * mockPlayerMoveRadius
            );
            mockPlayerRotation = Quaternion.Euler(0f, mockPlayerMoveAngle * Mathf.Rad2Deg, 0f);

            // Send transform updates
            if (Time.time >= nextTransformSendTime)
            {
                SendMockTransformUpdate(mockRemoteId);
                nextTransformSendTime = Time.time + (1f / mockTransformSendRate);
            }

            // Send audio updates
            if (mockAudioEnabled && Time.time >= nextAudioSendTime)
            {
                SendMockAudioData(mockRemoteId);
                nextAudioSendTime = Time.time + (1f / mockAudioSendRate);
            }
        }

        private void SendMockTransformUpdate(SteamId senderId)
        {
            // Create mock NetworkTransformSync packet
            // Packet format: [Type(1)][NetId(4)][OwnerSteamId(8)][Pos(12)][Rot(16)][Vel(12)]
            const int packetSize = 53;
            byte[] packet = new byte[packetSize];
            
            packet[0] = 0x10; // NetworkMessageType.TransformSync
            int offset = 1;

            // NetId (use SteamId as network ID for simplicity)
            WriteUInt(packet, ref offset, (uint)senderId.Value);
            
            // Owner SteamId
            WriteULong(packet, ref offset, senderId);
            
            // Position
            WriteVector3(packet, ref offset, mockPlayerPosition);
            
            // Rotation
            WriteQuaternion(packet, ref offset, mockPlayerRotation);
            
            // Velocity (circular motion velocity)
            Vector3 velocity = new Vector3(
                -Mathf.Sin(mockPlayerMoveAngle) * mockPlayerMoveSpeed * mockPlayerMoveRadius,
                0f,
                Mathf.Cos(mockPlayerMoveAngle) * mockPlayerMoveSpeed * mockPlayerMoveRadius
            );
            WriteVector3(packet, ref offset, velocity);

            // Queue packet as if received from remote player
            SimulateReceivePacket(senderId, packet, 1); // Channel 1 = Transform
        }

        private void SendMockAudioData(SteamId senderId)
        {
            // Create mock voice packet
            // Packet format: [Type(1)][SenderSteamId(8)][AudioSize(2)][CompressedAudio(N)]
            
            // Generate mock audio data (silence or simple tone)
            int audioDataSize = 160; // Typical opus frame size
            byte[] packet = new byte[1 + 8 + 2 + audioDataSize];
            
            packet[0] = 0x00; // NetworkMessageType.VoiceData
            int offset = 1;
            
            // Sender SteamId
            WriteULong(packet, ref offset, senderId);
            
            // Audio data size
            WriteUShort(packet, ref offset, (ushort)audioDataSize);
            
            // Mock audio data (random noise to simulate voice)
            for (int i = 0; i < audioDataSize; i++)
            {
                packet[offset + i] = (byte)(UnityEngine.Random.value * 20); // Low amplitude "noise"
            }

            // Queue packet as if received from remote player
            SimulateReceivePacket(senderId, packet, 2); // Channel 2 = Voice
        }

        // Serialization helpers (matching NetworkSerialization.cs format)
        private void WriteUInt(byte[] buffer, ref int offset, uint value)
        {
            buffer[offset++] = (byte)(value >> 24);
            buffer[offset++] = (byte)(value >> 16);
            buffer[offset++] = (byte)(value >> 8);
            buffer[offset++] = (byte)value;
        }

        private void WriteULong(byte[] buffer, ref int offset, SteamId value)
        {
            ulong val = value.Value;
            buffer[offset++] = (byte)(val >> 56);
            buffer[offset++] = (byte)(val >> 48);
            buffer[offset++] = (byte)(val >> 40);
            buffer[offset++] = (byte)(val >> 32);
            buffer[offset++] = (byte)(val >> 24);
            buffer[offset++] = (byte)(val >> 16);
            buffer[offset++] = (byte)(val >> 8);
            buffer[offset++] = (byte)val;
        }

        private void WriteUShort(byte[] buffer, ref int offset, ushort value)
        {
            buffer[offset++] = (byte)(value >> 8);
            buffer[offset++] = (byte)value;
        }

        private void WriteVector3(byte[] buffer, ref int offset, Vector3 value)
        {
            WriteFloat(buffer, ref offset, value.x);
            WriteFloat(buffer, ref offset, value.y);
            WriteFloat(buffer, ref offset, value.z);
        }

        private void WriteQuaternion(byte[] buffer, ref int offset, Quaternion value)
        {
            // Normalize before writing
            value = Quaternion.Normalize(value);
            WriteFloat(buffer, ref offset, value.x);
            WriteFloat(buffer, ref offset, value.y);
            WriteFloat(buffer, ref offset, value.z);
            WriteFloat(buffer, ref offset, value.w);
        }

        private void WriteFloat(byte[] buffer, ref int offset, float value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                buffer[offset++] = bytes[3];
                buffer[offset++] = bytes[2];
                buffer[offset++] = bytes[1];
                buffer[offset++] = bytes[0];
            }
            else
            {
                buffer[offset++] = bytes[0];
                buffer[offset++] = bytes[1];
                buffer[offset++] = bytes[2];
                buffer[offset++] = bytes[3];
            }
        }

        /// <summary>
        /// Mock SendP2PPacket - stores packet in local queue for testing
        /// </summary>
        public bool SendP2PPacket(SteamId targetId, byte[] data, int channel)
        {
            if (!enableMockMode || !isInitialized) return false;

            // Simulate network delay (optional)
            MockPacket packet = new MockPacket
            {
                SenderId = mockSteamId,
                Data = (byte[])data.Clone(),
                Channel = channel
            };

            if (channelQueues.ContainsKey(channel))
            {
                channelQueues[channel].Enqueue(packet);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Mock IsP2PPacketAvailable - checks if packets exist in queue
        /// </summary>
        public bool IsP2PPacketAvailable(int channel)
        {
            if (!enableMockMode || !isInitialized) return false;
            return channelQueues.ContainsKey(channel) && channelQueues[channel].Count > 0;
        }

        /// <summary>
        /// Mock ReadP2PPacket - reads from queue
        /// </summary>
        public MockP2PPacket? ReadP2PPacket(int channel)
        {
            if (!enableMockMode || !isInitialized) return null;

            if (channelQueues.ContainsKey(channel) && channelQueues[channel].Count > 0)
            {
                var packet = channelQueues[channel].Dequeue();
                return new MockP2PPacket
                {
                    SteamId = packet.SenderId,
                    Data = packet.Data
                };
            }

            return null;
        }

        /// <summary>
        /// Get mock lobby members
        /// </summary>
        public List<MockLobbyMember> GetMockMembers()
        {
            return mockMembers.ConvertAll(m => new MockLobbyMember { Id = m.Id, Name = m.Name });
        }

        /// <summary>
        /// Simulate receiving a packet from a remote peer (for testing)
        /// </summary>
        public void SimulateReceivePacket(SteamId senderId, byte[] data, int channel)
        {
            if (!enableMockMode || !isInitialized) return;

            MockPacket packet = new MockPacket
            {
                SenderId = senderId,
                Data = (byte[])data.Clone(),
                Channel = channel
            };

            if (channelQueues.ContainsKey(channel))
            {
                channelQueues[channel].Enqueue(packet);
            }
        }

        /// <summary>
        /// Add a mock player to the lobby at runtime
        /// </summary>
        public void AddMockPlayer(string playerName = null)
        {
            ulong newId = mockSteamId + (ulong)mockMembers.Count;
            mockMembers.Add(new MockMember
            {
                Id = newId,
                Name = playerName ?? $"MockPlayer{mockMembers.Count}"
            });
            Debug.Log($"[MockSteam] Added mock player: {newId}");
        }

        /// <summary>
        /// Remove a mock player from the lobby
        /// </summary>
        public void RemoveMockPlayer(SteamId steamId)
        {
            mockMembers.RemoveAll(m => m.Id == steamId);
            Debug.Log($"[MockSteam] Removed mock player: {steamId}");
        }

        public struct MockP2PPacket
        {
            public SteamId SteamId;
            public byte[] Data;
        }
    }
}