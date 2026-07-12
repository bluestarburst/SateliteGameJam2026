using SatelliteGameJam.Networking.Core;
using SatelliteGameJam.Networking.Identity;
using SatelliteGameJam.Networking.Messages;
using SatelliteGameJam.Networking.State;
using Steamworks;
using UnityEngine;

namespace SatelliteGameJam.Networking.Sync
{
    /// <summary>
    /// Lightweight presentation and sync proxy for a peer in the same gameplay scene.
    /// It never contains local input, cameras, movement controllers, or gameplay interactors.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkIdentity))]
    [RequireComponent(typeof(NetworkTransformSync))]
    [RequireComponent(typeof(NetworkPlayerTag))]
    [RequireComponent(typeof(PlayerAvatarComposition))]
    public sealed class RemotePlayerAvatar : MonoBehaviour
    {
        private SteamId steamId;
        private string displayName;
        private RoleVisualProfile visualProfile;
        private NetworkPlayerTag playerTag;
        private PlayerAvatarComposition composition;
        private bool stateEventsSubscribed;
        private GameObject appliedVisualPrefab;

        private void Awake()
        {
            playerTag = GetComponent<NetworkPlayerTag>();
            composition = GetComponent<PlayerAvatarComposition>();
        }

        private void OnDestroy()
        {
            UnsubscribeFromPlayerState();
        }

        public void Configure(SteamId id, string name, RoleVisualProfile profile)
        {
            steamId = id;
            displayName = name ?? string.Empty;
            visualProfile = profile;
            RefreshFromState();
            SubscribeToPlayerState();
        }

        private void SubscribeToPlayerState()
        {
            if (stateEventsSubscribed || PlayerStateManager.Instance == null)
            {
                return;
            }

            PlayerStateManager.Instance.OnRoleChanged += OnRoleChanged;
            PlayerStateManager.Instance.OnPlayerSceneChanged += OnPlayerSceneChanged;
            stateEventsSubscribed = true;
        }

        private void UnsubscribeFromPlayerState()
        {
            if (!stateEventsSubscribed || PlayerStateManager.Instance == null)
            {
                return;
            }

            PlayerStateManager.Instance.OnRoleChanged -= OnRoleChanged;
            PlayerStateManager.Instance.OnPlayerSceneChanged -= OnPlayerSceneChanged;
            stateEventsSubscribed = false;
        }

        private void OnRoleChanged(SteamId changedId, PlayerRole _)
        {
            if (changedId == steamId)
            {
                RefreshFromState();
            }
        }

        private void OnPlayerSceneChanged(SteamId changedId, NetworkSceneId _)
        {
            if (changedId == steamId)
            {
                RefreshFromState();
            }
        }

        private void RefreshFromState()
        {
            if (steamId.Value == 0 || playerTag == null)
            {
                return;
            }

            PlayerState state = PlayerStateManager.Instance?.GetPlayerState(steamId);
            PlayerRole role = state?.Role ?? PlayerRole.None;
            NetworkSceneId scene = state?.Scene ?? NetworkSceneId.None;
            playerTag.Configure(steamId, displayName, NetworkPlayerKind.Remote, role, scene);

            GameObject visualPrefab = visualProfile != null ? visualProfile.Resolve(role, scene) : null;
            if (visualPrefab != null && visualPrefab != appliedVisualPrefab)
            {
                composition.ApplyVisual(visualPrefab);
                appliedVisualPrefab = visualPrefab;
            }
        }
    }
}
