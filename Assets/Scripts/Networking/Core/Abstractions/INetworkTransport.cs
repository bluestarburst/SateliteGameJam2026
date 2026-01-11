using System;
using Steamworks;
using UnityEngine;

namespace SatelliteGameJam.Networking.Core.Abstractions
{
    /// <summary>
    /// Abstraction for network transport layer.
    /// Allows swapping between Steam P2P, Netcode, Mirror, custom UDP, etc.
    /// 
    /// This abstraction makes the networking system transport-agnostic,
    /// enabling reuse across different networking backends without changing game code.
    /// 
    /// Reference: DeveloperExperienceImprovements.md Part 6
    /// </summary>
    public interface INetworkTransport
    {
        /// <summary>
        /// Initialize transport layer.
        /// </summary>
        /// <param name="appId">Steam App ID or similar identifier</param>
        void Initialize(uint appId);

        /// <summary>
        /// Send packet to a specific peer.
        /// </summary>
        /// <param name="target">Target peer ID</param>
        /// <param name="data">Packet data to send</param>
        /// <param name="channel">Channel number (0-4)</param>
        /// <param name="reliable">Whether to guarantee delivery</param>
        void SendPacket(SteamId target, byte[] data, int channel, bool reliable);

        /// <summary>
        /// Broadcast packet to all connected peers.
        /// </summary>
        /// <param name="data">Packet data to send</param>
        /// <param name="channel">Channel number (0-4)</param>
        /// <param name="reliable">Whether to guarantee delivery</param>
        void BroadcastPacket(byte[] data, int channel, bool reliable);

        /// <summary>
        /// Poll for incoming packets.
        /// </summary>
        /// <param name="sender">Steam ID of sender (output)</param>
        /// <param name="data">Packet data (output)</param>
        /// <param name="channel">Channel the packet arrived on (output)</param>
        /// <returns>True if a packet was received, false otherwise</returns>
        bool TryReadPacket(out SteamId sender, out byte[] data, out int channel);

        /// <summary>
        /// Check if transport is connected.
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Get count of connected peers.
        /// </summary>
        int ConnectedPeerCount { get; }

        /// <summary>
        /// Shutdown transport layer.
        /// </summary>
        void Shutdown();
    }

    /// <summary>
    /// Steam P2P implementation of INetworkTransport.
    /// This is the current implementation used by the Satellite Game.
    /// </summary>
    public class SteamP2PTransport : INetworkTransport
    {
        private const int MaxChannels = 5;

        public bool IsConnected => SteamManager.Instance?.currentLobby.Id.Value != 0;

        public int ConnectedPeerCount
        {
            get
            {
                if (SteamManager.Instance?.currentLobby.Id.Value == 0)
                    return 0;
                
                return SteamManager.Instance.currentLobby.MemberCount - 1; // Exclude self
            }
        }

        public void Initialize(uint appId)
        {
            // Steam is already initialized by SteamManager
            Debug.Log($"[SteamP2PTransport] Initialized (Steam App ID: {appId})");
        }

        public void SendPacket(SteamId target, byte[] data, int channel, bool reliable)
        {
            if (data == null || data.Length == 0)
            {
                Debug.LogWarning("[SteamP2PTransport] Attempted to send empty packet");
                return;
            }

            if (channel < 0 || channel >= MaxChannels)
            {
                Debug.LogError($"[SteamP2PTransport] Invalid channel {channel}, must be 0-{MaxChannels - 1}");
                return;
            }

            P2PSend sendType = reliable ? P2PSend.Reliable : P2PSend.UnreliableNoDelay;
            
            bool success = SteamNetworking.SendP2PPacket(target, data, data.Length, channel, sendType);
            
            if (!success)
            {
                Debug.LogWarning($"[SteamP2PTransport] Failed to send packet to {target.Value} on channel {channel}");
            }
        }

        public void BroadcastPacket(byte[] data, int channel, bool reliable)
        {
            if (SteamManager.Instance?.currentLobby.MemberCount == 0)
            {
                Debug.LogWarning("[SteamP2PTransport] Cannot broadcast: not in lobby");
                return;
            }

            foreach (var member in SteamManager.Instance.currentLobby.Members)
            {
                if (member.Id != SteamManager.Instance.PlayerSteamId)
                {
                    SendPacket(member.Id, data, channel, reliable);
                }
            }
        }

        public bool TryReadPacket(out SteamId sender, out byte[] data, out int channel)
        {
            sender = 0;
            data = null;
            channel = 0;

            // Poll all channels
            for (int ch = 0; ch < MaxChannels; ch++)
            {
                if (SteamNetworking.IsP2PPacketAvailable(ch))
                {
                    P2Packet? packet = SteamNetworking.ReadP2PPacket(ch);
                    if (packet.HasValue)
                    {
                        sender = packet.Value.SteamId;
                        data = packet.Value.Data;
                        channel = ch;
                        return true;
                    }
                }
            }

            return false;
        }

        public void Shutdown()
        {
            // Steam shutdown handled by SteamManager
            Debug.Log("[SteamP2PTransport] Shutdown");
        }
    }

    /// <summary>
    /// Example: Mock transport for testing without Steam.
    /// Useful for unit tests or local development.
    /// </summary>
    public class MockTransport : INetworkTransport
    {
        private Queue<(SteamId sender, byte[] data, int channel)> incomingPackets = new Queue<(SteamId, byte[], int)>();
        private bool isConnected = false;

        public bool IsConnected => isConnected;
        public int ConnectedPeerCount => isConnected ? 1 : 0;

        public void Initialize(uint appId)
        {
            isConnected = true;
            Debug.Log($"[MockTransport] Initialized (mock mode)");
        }

        public void SendPacket(SteamId target, byte[] data, int channel, bool reliable)
        {
            Debug.Log($"[MockTransport] Sent packet to {target.Value} on channel {channel} (reliable: {reliable})");
            // In mock mode, don't actually send anywhere
        }

        public void BroadcastPacket(byte[] data, int channel, bool reliable)
        {
            Debug.Log($"[MockTransport] Broadcast packet on channel {channel} (reliable: {reliable})");
        }

        public bool TryReadPacket(out SteamId sender, out byte[] data, out int channel)
        {
            if (incomingPackets.Count > 0)
            {
                var packet = incomingPackets.Dequeue();
                sender = packet.sender;
                data = packet.data;
                channel = packet.channel;
                return true;
            }

            sender = 0;
            data = null;
            channel = 0;
            return false;
        }

        public void Shutdown()
        {
            isConnected = false;
            incomingPackets.Clear();
            Debug.Log("[MockTransport] Shutdown");
        }

        /// <summary>
        /// Test helper: inject a packet for testing receive logic.
        /// </summary>
        public void InjectPacket(SteamId sender, byte[] data, int channel)
        {
            incomingPackets.Enqueue((sender, data, channel));
        }
    }
}
