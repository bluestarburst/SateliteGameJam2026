using Steamworks;
using UnityEngine;
using SatelliteGameJam.Networking.Core;
using SatelliteGameJam.Networking.Identity;
using SatelliteGameJam.Networking.Messages;

namespace SatelliteGameJam.Networking.Sync
{
    /// <summary>
    /// Central manager for all networked synchronization.
    /// Registers message handlers ONCE and dispatches to the correct sync components.
    /// Fixes the issue where each sync component registered its own handler.
    ///
    /// Add this component to your SteamPack prefab or a persistent networking GameObject.
    /// </summary>
    public class NetworkSyncManager : MonoBehaviour
    {
        public static NetworkSyncManager Instance { get; private set; }

        [Header("Debug")]
        [SerializeField] private bool debugLogging = false;

        private bool handlersRegistered = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            RegisterHandlers();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            // CRITICAL FIX: Re-register handlers if NetworkConnectionManager was recreated
            // This can happen during scene transitions if there are DontDestroyOnLoad conflicts
            if (!handlersRegistered && NetworkConnectionManager.Instance != null)
            {
                RegisterHandlers();
            }
        }

        /// <summary>
        /// Registers all sync message handlers once.
        /// </summary>
        private void RegisterHandlers()
        {
            if (handlersRegistered) return;
            if (NetworkConnectionManager.Instance == null)
            {
                Debug.LogWarning("[NetworkSyncManager] NetworkConnectionManager not ready, deferring handler registration");
                return;
            }

            // Transform sync (Channel 1 - high frequency)
            NetworkConnectionManager.Instance.RegisterHandler(
                NetworkMessageType.TransformSync, OnReceiveTransformSync);

            // Physics sync (Channel 1 - high frequency)
            NetworkConnectionManager.Instance.RegisterHandler(
                NetworkMessageType.PhysicsSync, OnReceivePhysicsSync);

            // Interaction messages (Channel 3 - reliable)
            NetworkConnectionManager.Instance.RegisterHandler(
                NetworkMessageType.InteractionPickup, OnReceiveInteractionPickup);
            NetworkConnectionManager.Instance.RegisterHandler(
                NetworkMessageType.InteractionDrop, OnReceiveInteractionDrop);
            NetworkConnectionManager.Instance.RegisterHandler(
                NetworkMessageType.InteractionUse, OnReceiveInteractionUse);

            handlersRegistered = true;

            if (debugLogging)
                Debug.Log("[NetworkSyncManager] Registered all sync handlers");
        }

        // ===== Transform Sync =====

        private void OnReceiveTransformSync(SteamId sender, byte[] data)
        {
            if (data == null || data.Length < 5) return;

            // Read NetworkId from packet (at offset 1, after message type byte)
            int offset = 1;
            uint netId = NetworkSerialization.ReadUInt(data, ref offset);

            // Find the object and dispatch
            var identity = NetworkIdentity.GetById(netId);
            if (identity == null)
            {
                if (debugLogging)
                    Debug.LogWarning($"[NetworkSyncManager] TransformSync: No object with NetworkId {netId}");
                return;
            }

            var syncComponent = identity.GetComponent<NetworkTransformSync>();
            if (syncComponent != null)
            {
                syncComponent.HandleTransformSync(sender, data);
            }
        }

        // ===== Physics Sync =====

        private void OnReceivePhysicsSync(SteamId sender, byte[] data)
        {
            if (data == null || data.Length < 5) return;

            int offset = 1;
            uint netId = NetworkSerialization.ReadUInt(data, ref offset);

            var identity = NetworkIdentity.GetById(netId);
            if (identity == null)
            {
                if (debugLogging)
                    Debug.LogWarning($"[NetworkSyncManager] PhysicsSync: No object with NetworkId {netId}");
                return;
            }

            var syncComponent = identity.GetComponent<NetworkPhysicsObject>();
            if (syncComponent != null)
            {
                syncComponent.HandlePhysicsSync(sender, data);
            }
        }

        // ===== Interaction Messages =====

        private void OnReceiveInteractionPickup(SteamId sender, byte[] data)
        {
            if (data == null || data.Length < 5) return;

            int offset = 1;
            uint netId = NetworkSerialization.ReadUInt(data, ref offset);

            var identity = NetworkIdentity.GetById(netId);
            if (identity == null)
            {
                if (debugLogging)
                    Debug.LogWarning($"[NetworkSyncManager] InteractionPickup: No object with NetworkId {netId}");
                return;
            }

            var syncComponent = identity.GetComponent<NetworkInteractionState>();
            if (syncComponent != null)
            {
                syncComponent.HandlePickup(sender, data);
            }
        }

        private void OnReceiveInteractionDrop(SteamId sender, byte[] data)
        {
            if (data == null || data.Length < 5) return;

            int offset = 1;
            uint netId = NetworkSerialization.ReadUInt(data, ref offset);

            var identity = NetworkIdentity.GetById(netId);
            if (identity == null)
            {
                if (debugLogging)
                    Debug.LogWarning($"[NetworkSyncManager] InteractionDrop: No object with NetworkId {netId}");
                return;
            }

            var syncComponent = identity.GetComponent<NetworkInteractionState>();
            if (syncComponent != null)
            {
                syncComponent.HandleDrop(sender, data);
            }
        }

        private void OnReceiveInteractionUse(SteamId sender, byte[] data)
        {
            if (data == null || data.Length < 5) return;

            int offset = 1;
            uint netId = NetworkSerialization.ReadUInt(data, ref offset);

            var identity = NetworkIdentity.GetById(netId);
            if (identity == null)
            {
                if (debugLogging)
                    Debug.LogWarning($"[NetworkSyncManager] InteractionUse: No object with NetworkId {netId}");
                return;
            }

            var syncComponent = identity.GetComponent<NetworkInteractionState>();
            if (syncComponent != null)
            {
                syncComponent.HandleUse(sender, data);
            }
        }
    }
}
