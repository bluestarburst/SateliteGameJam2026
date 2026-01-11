using UnityEngine;
using SatelliteGameJam.Networking.Identity;

namespace SatelliteGameJam.Networking.Sync
{
    /// <summary>
    /// Automatically adds NetworkIdentity and required components to networked objects.
    /// Use [RequireComponent] instead of manual script assignment.
    /// </summary>
    public abstract class NetworkSyncBase : MonoBehaviour
    {
        [Header("Auto-Setup")]
        [SerializeField] [Tooltip("Automatically add NetworkIdentity if missing")]
        private bool autoAddNetworkIdentity = true;

        protected NetworkIdentity netIdentity;
        protected bool isInitialized = false;

        protected virtual void Awake()
        {
            SetupNetworkComponents();
        }

        /// <summary>
        /// Ensures required components exist. Called automatically in Awake.
        /// </summary>
        private void SetupNetworkComponents()
        {
            // Get or add NetworkIdentity
            netIdentity = GetComponent<NetworkIdentity>();
            if (netIdentity == null && autoAddNetworkIdentity)
            {
                netIdentity = gameObject.AddComponent<NetworkIdentity>();
                Debug.Log($"[{GetType().Name}] Auto-added NetworkIdentity to {gameObject.name}");
            }

            if (netIdentity == null)
            {
                Debug.LogError($"[{GetType().Name}] NetworkIdentity is required but missing on {gameObject.name}");
                enabled = false;
                return;
            }

            // Call derived class initialization
            OnNetworkSetupComplete();
            isInitialized = true;
        }

        /// <summary>
        /// Override this for initialization logic that requires NetworkIdentity to be ready.
        /// Called after NetworkIdentity is guaranteed to exist.
        /// </summary>
        protected virtual void OnNetworkSetupComplete() { }

        /// <summary>
        /// Returns true if the local player owns this object.
        /// </summary>
        protected bool IsOwner()
        {
            if (!isInitialized || netIdentity == null) return false;
            return netIdentity.OwnerSteamId == SteamManager.Instance?.PlayerSteamId;
        }
    }
}