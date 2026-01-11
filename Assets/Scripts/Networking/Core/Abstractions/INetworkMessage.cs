using System;
using Steamworks;

namespace SatelliteGameJam.Networking.Core.Abstractions
{
    /// <summary>
    /// Base interface for all network messages.
    /// Projects implement this for custom message types.
    /// This abstraction allows the networking framework to be reused across different games.
    /// 
    /// Example usage:
    /// <code>
    /// public class MyGameMessage : INetworkMessage
    /// {
    ///     public byte MessageTypeId => 0x50;
    ///     public int Channel => 1;
    ///     public bool RequireReliable => true;
    ///     
    ///     public byte[] Serialize() { /* serialize your data */ }
    ///     public void Deserialize(byte[] data) { /* deserialize your data */ }
    /// }
    /// </code>
    /// 
    /// Reference: DeveloperExperienceImprovements.md Part 6
    /// </summary>
    public interface INetworkMessage
    {
        /// <summary>
        /// Unique identifier for this message type (0-255).
        /// Should be unique across all message types in your project.
        /// </summary>
        byte MessageTypeId { get; }

        /// <summary>
        /// Which Steam P2P channel to send on (0-4).
        /// Channel 0: Reliable ordered (state updates)
        /// Channel 1: Unreliable unordered (high-frequency transforms)
        /// Channel 2: Reliable unordered (voice chat)
        /// Channel 3: Unreliable unordered (physics)
        /// Channel 4: Reliable ordered (satellite state)
        /// </summary>
        int Channel { get; }

        /// <summary>
        /// Whether this message requires reliable delivery.
        /// True: Steam guarantees delivery and order (if using reliable channel)
        /// False: Best-effort delivery, may be lost or arrive out of order
        /// </summary>
        bool RequireReliable { get; }

        /// <summary>
        /// Serialize this message to bytes for network transmission.
        /// First byte should typically be MessageTypeId.
        /// </summary>
        /// <returns>Byte array containing serialized message data</returns>
        byte[] Serialize();

        /// <summary>
        /// Deserialize from bytes received from network.
        /// Assume first byte is MessageTypeId and has already been read.
        /// </summary>
        /// <param name="data">Complete packet data including MessageTypeId</param>
        void Deserialize(byte[] data);
    }

    /// <summary>
    /// Handler for receiving network messages of a specific type.
    /// Type parameter T allows type-safe message handling.
    /// </summary>
    /// <typeparam name="T">Message type implementing INetworkMessage</typeparam>
    /// <param name="sender">Steam ID of the player who sent this message</param>
    /// <param name="message">Deserialized message instance</param>
    public delegate void NetworkMessageHandler<T>(SteamId sender, T message) where T : INetworkMessage;

    /// <summary>
    /// Generic handler that doesn't know the message type at compile time.
    /// Used internally by NetworkHandlerRegistry for type erasure.
    /// </summary>
    /// <param name="sender">Steam ID of the player who sent this message</param>
    /// <param name="message">Deserialized message instance (type unknown at compile time)</param>
    public delegate void GenericNetworkMessageHandler(SteamId sender, INetworkMessage message);
}
