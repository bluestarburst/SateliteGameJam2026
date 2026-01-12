using System;
using UnityEngine;
using SatelliteGameJam.Networking.Core.Abstractions;
using SatelliteGameJam.Networking.Messages;

namespace SatelliteGameJam.Networking.Messages
{
    /// <summary>
    /// Example: Custom message type for Satellite Game player ready state.
    /// This shows how to implement INetworkMessage for game-specific messages.
    /// 
    /// Other projects would create their own message types following this pattern.
    /// 
    /// Reference: DeveloperExperienceImprovements.md Part 6
    /// </summary>
    public class PlayerReadyMessage : INetworkMessage
    {
        public byte MessageTypeId => (byte)NetworkMessageType.PlayerReady;
        public int Channel => 0; // Reliable ordered channel for state updates
        public bool RequireReliable => true;

        public ulong PlayerId { get; set; }
        public bool IsReady { get; set; }

        public PlayerReadyMessage()
        {
            // Parameterless constructor required for registry
        }

        public PlayerReadyMessage(ulong playerId, bool isReady)
        {
            PlayerId = playerId;
            IsReady = isReady;
        }

        public byte[] Serialize()
        {
            byte[] packet = new byte[10];
            packet[0] = MessageTypeId;
            Buffer.BlockCopy(BitConverter.GetBytes(PlayerId), 0, packet, 1, 8);
            packet[9] = (byte)(IsReady ? 1 : 0);
            return packet;
        }

        public void Deserialize(byte[] data)
        {
            if (data.Length < 10)
                throw new ArgumentException($"PlayerReadyMessage packet too small: {data.Length} bytes");

            PlayerId = BitConverter.ToUInt64(data, 1);
            IsReady = data[9] != 0;
        }
    }

    /// <summary>
    /// Example: Satellite health update message.
    /// Shows serialization of more complex data (float + bitfield).
    /// </summary>
    public class SatelliteHealthMessage : INetworkMessage
    {
        public byte MessageTypeId => (byte)NetworkMessageType.SatelliteHealth;
        public int Channel => 4; // Satellite state channel
        public bool RequireReliable => true;

        public float Health { get; set; }
        public uint DamageBits { get; set; }

        public SatelliteHealthMessage()
        {
        }

        public SatelliteHealthMessage(float health, uint damageBits)
        {
            Health = health;
            DamageBits = damageBits;
        }

        public byte[] Serialize()
        {
            byte[] packet = new byte[9];
            packet[0] = MessageTypeId;
            Buffer.BlockCopy(BitConverter.GetBytes(Health), 0, packet, 1, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(DamageBits), 0, packet, 5, 4);
            return packet;
        }

        public void Deserialize(byte[] data)
        {
            if (data.Length < 9)
                throw new ArgumentException($"SatelliteHealthMessage packet too small: {data.Length} bytes");

            Health = BitConverter.ToSingle(data, 1);
            DamageBits = BitConverter.ToUInt32(data, 5);
        }
    }

    /// <summary>
    /// Example: Player role selection message.
    /// Shows serialization of enums.
    /// </summary>
    public class PlayerRoleMessage : INetworkMessage
    {
        public byte MessageTypeId => 0x03; // Custom message type ID
        public int Channel => 0;
        public bool RequireReliable => true;

        public ulong PlayerId { get; set; }
        public PlayerRole Role { get; set; }

        public PlayerRoleMessage()
        {
        }

        public PlayerRoleMessage(ulong playerId, PlayerRole role)
        {
            PlayerId = playerId;
            Role = role;
        }

        public byte[] Serialize()
        {
            byte[] packet = new byte[10];
            packet[0] = MessageTypeId;
            Buffer.BlockCopy(BitConverter.GetBytes(PlayerId), 0, packet, 1, 8);
            packet[9] = (byte)Role;
            return packet;
        }

        public void Deserialize(byte[] data)
        {
            if (data.Length < 10)
                throw new ArgumentException($"PlayerRoleMessage packet too small: {data.Length} bytes");

            PlayerId = BitConverter.ToUInt64(data, 1);
            Role = (PlayerRole)data[9];
        }
    }

    /// <summary>
    /// Example: Scene transition message.
    /// Shows serialization of enums for scene IDs.
    /// </summary>
    public class SceneTransitionMessage : INetworkMessage
    {
        public byte MessageTypeId => 0x04; // Custom message type ID
        public int Channel => 0;
        public bool RequireReliable => true;

        public NetworkSceneId TargetScene { get; set; }

        public SceneTransitionMessage()
        {
        }

        public SceneTransitionMessage(NetworkSceneId targetScene)
        {
            TargetScene = targetScene;
        }

        public byte[] Serialize()
        {
            byte[] packet = new byte[2];
            packet[0] = MessageTypeId;
            packet[1] = (byte)TargetScene;
            return packet;
        }

        public void Deserialize(byte[] data)
        {
            if (data.Length < 2)
                throw new ArgumentException($"SceneTransitionMessage packet too small: {data.Length} bytes");

            TargetScene = (NetworkSceneId)data[1];
        }
    }

    /// <summary>
    /// Example: Transform sync message for high-frequency updates.
    /// Shows use of unreliable channel for performance.
    /// </summary>
    public class TransformSyncMessage : INetworkMessage
    {
        public byte MessageTypeId => (byte)NetworkMessageType.TransformSync;
        public int Channel => 1; // High-frequency unreliable channel
        public bool RequireReliable => false;

        public uint NetworkId { get; set; }
        public Vector3 Position { get; set; }
        public Quaternion Rotation { get; set; }

        public TransformSyncMessage()
        {
        }

        public TransformSyncMessage(uint networkId, Vector3 position, Quaternion rotation)
        {
            NetworkId = networkId;
            Position = position;
            Rotation = rotation;
        }

        public byte[] Serialize()
        {
            byte[] packet = new byte[33];
            packet[0] = MessageTypeId;
            
            int offset = 1;
            Buffer.BlockCopy(BitConverter.GetBytes(NetworkId), 0, packet, offset, 4);
            offset += 4;

            Buffer.BlockCopy(BitConverter.GetBytes(Position.x), 0, packet, offset, 4);
            offset += 4;
            Buffer.BlockCopy(BitConverter.GetBytes(Position.y), 0, packet, offset, 4);
            offset += 4;
            Buffer.BlockCopy(BitConverter.GetBytes(Position.z), 0, packet, offset, 4);
            offset += 4;

            Buffer.BlockCopy(BitConverter.GetBytes(Rotation.x), 0, packet, offset, 4);
            offset += 4;
            Buffer.BlockCopy(BitConverter.GetBytes(Rotation.y), 0, packet, offset, 4);
            offset += 4;
            Buffer.BlockCopy(BitConverter.GetBytes(Rotation.z), 0, packet, offset, 4);
            offset += 4;
            Buffer.BlockCopy(BitConverter.GetBytes(Rotation.w), 0, packet, offset, 4);

            return packet;
        }

        public void Deserialize(byte[] data)
        {
            if (data.Length < 33)
                throw new ArgumentException($"TransformSyncMessage packet too small: {data.Length} bytes");

            int offset = 1;
            NetworkId = BitConverter.ToUInt32(data, offset);
            offset += 4;

            float px = BitConverter.ToSingle(data, offset);
            offset += 4;
            float py = BitConverter.ToSingle(data, offset);
            offset += 4;
            float pz = BitConverter.ToSingle(data, offset);
            offset += 4;

            Position = new Vector3(px, py, pz);

            float rx = BitConverter.ToSingle(data, offset);
            offset += 4;
            float ry = BitConverter.ToSingle(data, offset);
            offset += 4;
            float rz = BitConverter.ToSingle(data, offset);
            offset += 4;
            float rw = BitConverter.ToSingle(data, offset);

            Rotation = new Quaternion(rx, ry, rz, rw);
        }
    }

    /// <summary>
    /// Example: Object interaction message (pickup/drop).
    /// Shows serialization of Vector3 and velocity data.
    /// </summary>
    public class ObjectInteractionMessage : INetworkMessage
    {
        public byte MessageTypeId => 0x05; // Custom message type ID
        public int Channel => 0;
        public bool RequireReliable => true;

        public uint ObjectNetworkId { get; set; }
        public ulong InteractorId { get; set; }
        public bool IsPickup { get; set; } // true = pickup, false = drop
        public Vector3 DropPosition { get; set; }
        public Vector3 DropVelocity { get; set; }

        public ObjectInteractionMessage()
        {
        }

        public byte[] Serialize()
        {
            byte[] packet = new byte[38];
            packet[0] = MessageTypeId;
            
            int offset = 1;
            Buffer.BlockCopy(BitConverter.GetBytes(ObjectNetworkId), 0, packet, offset, 4);
            offset += 4;
            Buffer.BlockCopy(BitConverter.GetBytes(InteractorId), 0, packet, offset, 8);
            offset += 8;
            packet[offset] = (byte)(IsPickup ? 1 : 0);
            offset += 1;

            Buffer.BlockCopy(BitConverter.GetBytes(DropPosition.x), 0, packet, offset, 4);
            offset += 4;
            Buffer.BlockCopy(BitConverter.GetBytes(DropPosition.y), 0, packet, offset, 4);
            offset += 4;
            Buffer.BlockCopy(BitConverter.GetBytes(DropPosition.z), 0, packet, offset, 4);
            offset += 4;

            Buffer.BlockCopy(BitConverter.GetBytes(DropVelocity.x), 0, packet, offset, 4);
            offset += 4;
            Buffer.BlockCopy(BitConverter.GetBytes(DropVelocity.y), 0, packet, offset, 4);
            offset += 4;
            Buffer.BlockCopy(BitConverter.GetBytes(DropVelocity.z), 0, packet, offset, 4);

            return packet;
        }

        public void Deserialize(byte[] data)
        {
            if (data.Length < 38)
                throw new ArgumentException($"ObjectInteractionMessage packet too small: {data.Length} bytes");

            int offset = 1;
            ObjectNetworkId = BitConverter.ToUInt32(data, offset);
            offset += 4;
            InteractorId = BitConverter.ToUInt64(data, offset);
            offset += 8;
            IsPickup = data[offset] != 0;
            offset += 1;

            float dpx = BitConverter.ToSingle(data, offset);
            offset += 4;
            float dpy = BitConverter.ToSingle(data, offset);
            offset += 4;
            float dpz = BitConverter.ToSingle(data, offset);
            offset += 4;
            DropPosition = new Vector3(dpx, dpy, dpz);

            float dvx = BitConverter.ToSingle(data, offset);
            offset += 4;
            float dvy = BitConverter.ToSingle(data, offset);
            offset += 4;
            float dvz = BitConverter.ToSingle(data, offset);
            DropVelocity = new Vector3(dvx, dvy, dvz);
        }
    }
}
