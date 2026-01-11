using System;
using System.Collections.Generic;
using UnityEngine;

namespace SatelliteGameJam.Networking.Core.Abstractions
{
    /// <summary>
    /// Central registry for message types.
    /// Projects register their custom message types here during initialization.
    /// Allows hot-swapping message types without code changes to core networking.
    /// 
    /// Usage:
    /// <code>
    /// // During app initialization:
    /// NetworkMessageRegistry.Instance.RegisterMessageType&lt;PlayerReadyMessage&gt;(0x10);
    /// NetworkMessageRegistry.Instance.RegisterMessageType&lt;SatelliteHealthMessage&gt;(0x20);
    /// 
    /// // Later when receiving:
    /// var message = NetworkMessageRegistry.Instance.CreateMessage(messageId);
    /// message.Deserialize(data);
    /// </code>
    /// 
    /// Reference: DeveloperExperienceImprovements.md Part 6
    /// </summary>
    public class NetworkMessageRegistry
    {
        private static NetworkMessageRegistry instance;
        private Dictionary<byte, Type> messageTypes = new Dictionary<byte, Type>();
        private Dictionary<Type, byte> typeToId = new Dictionary<Type, byte>();

        public static NetworkMessageRegistry Instance => instance ??= new NetworkMessageRegistry();

        /// <summary>
        /// Register a message type with a unique ID.
        /// Call this during app initialization for all custom message types.
        /// </summary>
        /// <typeparam name="T">Message type implementing INetworkMessage</typeparam>
        /// <param name="messageId">Unique identifier (0-255) for this message type</param>
        /// <exception cref="InvalidOperationException">If message ID is already registered</exception>
        public void RegisterMessageType<T>(byte messageId) where T : INetworkMessage, new()
        {
            if (messageTypes.ContainsKey(messageId))
            {
                throw new InvalidOperationException(
                    $"[NetworkMessageRegistry] Message ID {messageId} already registered to type {messageTypes[messageId].Name}. " +
                    $"Cannot register {typeof(T).Name}.");
            }

            if (typeToId.ContainsKey(typeof(T)))
            {
                throw new InvalidOperationException(
                    $"[NetworkMessageRegistry] Message type {typeof(T).Name} already registered with ID {typeToId[typeof(T)]}. " +
                    $"Cannot register again with ID {messageId}.");
            }

            messageTypes[messageId] = typeof(T);
            typeToId[typeof(T)] = messageId;

            Debug.Log($"[NetworkMessageRegistry] Registered message type: {typeof(T).Name} with ID {messageId}");
        }

        /// <summary>
        /// Get message type by ID.
        /// </summary>
        /// <param name="messageId">Message type ID to look up</param>
        /// <returns>Type implementing INetworkMessage</returns>
        /// <exception cref="ArgumentException">If message ID is not registered</exception>
        public Type GetMessageType(byte messageId)
        {
            if (messageTypes.TryGetValue(messageId, out var type))
                return type;

            throw new ArgumentException($"[NetworkMessageRegistry] Unknown message type ID: {messageId}. " +
                $"Make sure to register all message types during initialization.");
        }

        /// <summary>
        /// Get message ID by type.
        /// </summary>
        /// <typeparam name="T">Message type implementing INetworkMessage</typeparam>
        /// <returns>Unique message ID</returns>
        /// <exception cref="ArgumentException">If message type is not registered</exception>
        public byte GetMessageId<T>() where T : INetworkMessage
        {
            if (typeToId.TryGetValue(typeof(T), out var id))
                return id;

            throw new ArgumentException($"[NetworkMessageRegistry] Message type {typeof(T).Name} not registered. " +
                $"Call RegisterMessageType<{typeof(T).Name}>(id) during initialization.");
        }

        /// <summary>
        /// Create an instance of a message by ID.
        /// Used for deserializing incoming packets.
        /// </summary>
        /// <param name="messageId">Message type ID</param>
        /// <returns>New instance of the message type</returns>
        /// <exception cref="ArgumentException">If message ID is not registered</exception>
        /// <exception cref="InvalidOperationException">If message type cannot be instantiated</exception>
        public INetworkMessage CreateMessage(byte messageId)
        {
            var type = GetMessageType(messageId);
            
            try
            {
                return (INetworkMessage)Activator.CreateInstance(type);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"[NetworkMessageRegistry] Failed to create instance of message type {type.Name}. " +
                    $"Ensure the type has a parameterless constructor. Error: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Check if a message ID is registered.
        /// </summary>
        /// <param name="messageId">Message ID to check</param>
        /// <returns>True if registered, false otherwise</returns>
        public bool IsMessageTypeRegistered(byte messageId)
        {
            return messageTypes.ContainsKey(messageId);
        }

        /// <summary>
        /// Check if a message type is registered.
        /// </summary>
        /// <typeparam name="T">Message type to check</typeparam>
        /// <returns>True if registered, false otherwise</returns>
        public bool IsMessageTypeRegistered<T>() where T : INetworkMessage
        {
            return typeToId.ContainsKey(typeof(T));
        }

        /// <summary>
        /// Get count of registered message types.
        /// </summary>
        public int RegisteredTypeCount => messageTypes.Count;

        /// <summary>
        /// Clear all registered message types.
        /// Useful for testing or reinitializing.
        /// </summary>
        public void ClearAllRegistrations()
        {
            messageTypes.Clear();
            typeToId.Clear();
            Debug.Log("[NetworkMessageRegistry] Cleared all message type registrations");
        }
    }
}
