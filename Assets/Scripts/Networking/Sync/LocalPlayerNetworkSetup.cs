using UnityEngine;
using Steamworks;
using SatelliteGameJam.Networking.Identity;
using SatelliteGameJam.Networking.Core;

namespace SatelliteGameJam.Networking.Sync
{
    /// <summary>
    /// Attach this to the local player GameObject to configure networking.
    /// Sets up NetworkIdentity with the local player's SteamId so that
    /// NetworkTransformSync knows this is the owner and will SEND transform data.
    ///
    /// Usage:
    /// 1. Add NetworkIdentity to your local player
    /// 2. Add NetworkTransformSync to your local player
    /// 3. Add this LocalPlayerNetworkSetup script
    ///
    /// On Start, this will set the NetworkId and Owner to the local SteamId,
    /// causing NetworkTransformSync to broadcast position/rotation to all peers.
    /// </summary>
    [RequireComponent(typeof(NetworkIdentity))]
    public class LocalPlayerNetworkSetup : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("If true, will retry setup if SteamManager isn't ready yet")]
        [SerializeField] private bool retryUntilReady = true;

        [Tooltip("Delay between retry attempts in seconds")]
        [SerializeField] private float retryDelay = 0.1f;

        [Header("Debug")]
        [SerializeField] private bool logDebug = false;

        private NetworkIdentity networkIdentity;
        private bool isSetup = false;

        private void Awake()
        {
            networkIdentity = GetComponent<NetworkIdentity>();
        }

        private void Start()
        {
            TrySetup();
        }

        private void TrySetup()
        {
            if (isSetup) return;

            // Check if SteamManager is ready
            if (SteamManager.Instance == null || SteamManager.Instance.PlayerSteamId.Value == 0)
            {
                if (retryUntilReady)
                {
                    if (logDebug)
                        Debug.Log("[LocalPlayerNetworkSetup] SteamManager not ready, retrying...");
                    Invoke(nameof(TrySetup), retryDelay);
                }
                else
                {
                    Debug.LogWarning("[LocalPlayerNetworkSetup] SteamManager not ready and retry disabled");
                }
                return;
            }

            SetupNetworkIdentity();
        }

        private void SetupNetworkIdentity()
        {
            SteamId localSteamId = SteamManager.Instance.PlayerSteamId;

            // Set the NetworkId to our SteamId so remote players can find us
            // This matches what NetworkConnectionManager.SpawnRemotePlayerFor does for remote players
            networkIdentity.SetNetworkId((uint)localSteamId.Value);
            networkIdentity.SetOwner(localSteamId);

            isSetup = true;

            if (logDebug)
            {
                Debug.Log($"[LocalPlayerNetworkSetup] Configured local player:" +
                    $"\n  NetworkId: {networkIdentity.NetworkId}" +
                    $"\n  Owner: {localSteamId}" +
                    $"\n  Name: {SteamManager.Instance.PlayerName}");
            }
        }

        /// <summary>
        /// Returns true if this is the local player (owner matches local SteamId).
        /// Useful for other scripts to check ownership.
        /// </summary>
        public bool IsLocalPlayer()
        {
            if (SteamManager.Instance == null) return false;
            return networkIdentity.OwnerSteamId == SteamManager.Instance.PlayerSteamId;
        }

        /// <summary>
        /// Force re-setup (useful if Steam connection was re-established).
        /// </summary>
        public void ForceSetup()
        {
            isSetup = false;
            TrySetup();
        }
    }
}
