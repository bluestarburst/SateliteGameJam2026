using System;
using System.Collections.Generic;
using Steamworks;
using UnityEngine;

namespace SatelliteGameJam.Networking.Core.Abstractions
{
    /// <summary>
    /// Generic handler registration system.
    /// Allows multiple handlers for the same message type.
    /// Allows projects to inject custom handlers without modifying core code.
    /// 
    /// Usage:
    /// <code>
    /// // Register handler:
    /// NetworkHandlerRegistry registry = new NetworkHandlerRegistry();
    /// registry.RegisterHandler&lt;PlayerReadyMessage&gt;(OnPlayerReady);
    /// 
    /// // When packet arrives:
    /// registry.InvokeHandlers(senderId, messageTypeId, packetData);
    /// 
    /// // Unregister when done:
    /// registry.UnregisterHandler&lt;PlayerReadyMessage&gt;(OnPlayerReady);
    /// </code>
    /// 
    /// Reference: DeveloperExperienceImprovements.md Part 6
    /// </summary>
    public class NetworkHandlerRegistry
    {
        private Dictionary<byte, List<GenericNetworkMessageHandler>> handlers = new Dictionary<byte, List<GenericNetworkMessageHandler>>();
        private Dictionary<Type, List<object>> typedHandlerReferences = new Dictionary<Type, List<object>>();

        /// <summary>
        /// Register a handler for a specific message type.
        /// Multiple handlers can be registered for the same type - they will all be invoked.
        /// </summary>
        /// <typeparam name="T">Message type implementing INetworkMessage</typeparam>
        /// <param name="handler">Handler callback to invoke when message is received</param>
        public void RegisterHandler<T>(NetworkMessageHandler<T> handler) where T : INetworkMessage, new()
        {
            if (handler == null)
            {
                Debug.LogWarning("[NetworkHandlerRegistry] Attempted to register null handler");
                return;
            }

            var messageId = NetworkMessageRegistry.Instance.GetMessageId<T>();

            if (!handlers.ContainsKey(messageId))
                handlers[messageId] = new List<GenericNetworkMessageHandler>();

            // Wrap typed handler in generic handler
            GenericNetworkMessageHandler genericHandler = (sender, msg) =>
            {
                try
                {
                    handler(sender, (T)msg);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[NetworkHandlerRegistry] Error in handler for message type {typeof(T).Name}: {ex}");
                }
            };

            handlers[messageId].Add(genericHandler);

            // Keep reference to original typed handler for unregistration
            if (!typedHandlerReferences.ContainsKey(typeof(T)))
                typedHandlerReferences[typeof(T)] = new List<object>();
            
            typedHandlerReferences[typeof(T)].Add(handler);

            Debug.Log($"[NetworkHandlerRegistry] Registered handler for message type {typeof(T).Name} (ID: {messageId})");
        }

        /// <summary>
        /// Unregister a specific handler.
        /// </summary>
        /// <typeparam name="T">Message type implementing INetworkMessage</typeparam>
        /// <param name="handler">Handler to remove</param>
        public void UnregisterHandler<T>(NetworkMessageHandler<T> handler) where T : INetworkMessage, new()
        {
            if (handler == null)
                return;

            var messageId = NetworkMessageRegistry.Instance.GetMessageId<T>();

            if (handlers.TryGetValue(messageId, out var handlerList))
            {
                // Find and remove matching handler by comparing targets
                handlerList.RemoveAll(h => h.Target == handler.Target && h.Method.Name == handler.Method.Name);
                
                if (handlerList.Count == 0)
                {
                    handlers.Remove(messageId);
                }
            }

            if (typedHandlerReferences.TryGetValue(typeof(T), out var refList))
            {
                refList.Remove(handler);
                
                if (refList.Count == 0)
                {
                    typedHandlerReferences.Remove(typeof(T));
                }
            }

            Debug.Log($"[NetworkHandlerRegistry] Unregistered handler for message type {typeof(T).Name}");
        }

        /// <summary>
        /// Unregister all handlers for a specific message type.
        /// </summary>
        /// <typeparam name="T">Message type implementing INetworkMessage</typeparam>
        public void UnregisterAllHandlers<T>() where T : INetworkMessage, new()
        {
            var messageId = NetworkMessageRegistry.Instance.GetMessageId<T>();

            if (handlers.ContainsKey(messageId))
            {
                handlers.Remove(messageId);
                Debug.Log($"[NetworkHandlerRegistry] Unregistered all handlers for message type {typeof(T).Name}");
            }

            if (typedHandlerReferences.ContainsKey(typeof(T)))
            {
                typedHandlerReferences.Remove(typeof(T));
            }
        }

        /// <summary>
        /// Invoke all handlers for a message type.
        /// Called by NetworkConnectionManager when a packet arrives.
        /// </summary>
        /// <param name="sender">Steam ID of sender</param>
        /// <param name="messageId">Message type ID from packet</param>
        /// <param name="data">Complete packet data</param>
        public void InvokeHandlers(SteamId sender, byte messageId, byte[] data)
        {
            if (!handlers.TryGetValue(messageId, out var handlerList))
            {
                Debug.LogWarning($"[NetworkHandlerRegistry] No handlers registered for message ID {messageId}");
                return;
            }

            // Create message instance and deserialize
            INetworkMessage message;
            try
            {
                message = NetworkMessageRegistry.Instance.CreateMessage(messageId);
                message.Deserialize(data);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NetworkHandlerRegistry] Failed to deserialize message ID {messageId}: {ex}");
                return;
            }

            // Invoke all registered handlers
            foreach (var handler in handlerList)
            {
                try
                {
                    handler(sender, message);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[NetworkHandlerRegistry] Error invoking handler for message ID {messageId}: {ex}");
                    // Continue invoking other handlers even if one fails
                }
            }
        }

        /// <summary>
        /// Check if any handlers are registered for a message type.
        /// </summary>
        /// <param name="messageId">Message type ID</param>
        /// <returns>True if at least one handler is registered</returns>
        public bool HasHandlers(byte messageId)
        {
            return handlers.ContainsKey(messageId) && handlers[messageId].Count > 0;
        }

        /// <summary>
        /// Get count of handlers for a specific message type.
        /// </summary>
        /// <param name="messageId">Message type ID</param>
        /// <returns>Number of registered handlers</returns>
        public int GetHandlerCount(byte messageId)
        {
            if (handlers.TryGetValue(messageId, out var handlerList))
                return handlerList.Count;
            
            return 0;
        }

        /// <summary>
        /// Clear all registered handlers.
        /// Useful for cleanup or testing.
        /// </summary>
        public void ClearAllHandlers()
        {
            handlers.Clear();
            typedHandlerReferences.Clear();
            Debug.Log("[NetworkHandlerRegistry] Cleared all handlers");
        }
    }
}
