using System.Collections;
using System.Linq;
using SatelliteGameJam.Networking.Core;
using SatelliteGameJam.Networking.Messages;
using SatelliteGameJam.Networking.State;
using Steamworks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SatelliteGameJam.Players
{
    /// <summary>
    /// Scene-local factory for the one locally controlled gameplay rig.
    /// Remote avatars are created separately by SceneSyncManager/NetworkConnectionManager.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScenePlayerBootstrap : MonoBehaviour
    {
        [SerializeField] private PlayerRigCatalog catalog;
        [SerializeField] private PlayerRole offlineRole = PlayerRole.None;
        [SerializeField] private NetworkSceneId offlineScene = NetworkSceneId.None;
        [SerializeField] private bool logDebug;

        private PlayerRig localRig;

        private IEnumerator Start()
        {
            // SceneSync has already applied an online assignment before this scene is loaded.
            // One frame also lets the offline SceneFlowController publish its local state.
            yield return null;
            SpawnLocalRig();
        }

        private void SpawnLocalRig()
        {
            if (localRig != null)
            {
                return;
            }

            ResolveLocalContract(out PlayerRole role, out NetworkSceneId scene);
            PlayerRigCatalog resolvedCatalog = catalog ?? NetworkingConfiguration.Instance?.playerRigCatalog;
            PlayerRig prefab = resolvedCatalog != null ? resolvedCatalog.Resolve(role, scene) : null;
            if (prefab == null)
            {
                Debug.LogError($"[ScenePlayerBootstrap] No local rig is configured for {role}/{scene}.", this);
                return;
            }

            Transform spawn = FindSpawnPoint(role, scene);
            localRig = Instantiate(prefab, spawn != null ? spawn.position : transform.position,
                spawn != null ? spawn.rotation : transform.rotation);
            localRig.name = $"Local_{role}_Player";

            if (logDebug)
            {
                Debug.Log($"[ScenePlayerBootstrap] Spawned {localRig.name} in {SceneManager.GetActiveScene().name}.");
            }
        }

        private void ResolveLocalContract(out PlayerRole role, out NetworkSceneId scene)
        {
            scene = offlineScene;
            role = offlineRole;

            if (SceneFlowController.Instance != null &&
                SceneFlowController.Instance.TryGetSceneId(SceneManager.GetActiveScene().name, out NetworkSceneId mappedScene))
            {
                scene = mappedScene;
            }

            SteamId localId = SteamManager.Instance?.PlayerSteamId ?? default;
            PlayerState state = localId.Value != 0
                ? PlayerStateManager.Instance?.GetPlayerState(localId)
                : null;

            if (state != null)
            {
                if (state.Role != PlayerRole.None)
                {
                    role = state.Role;
                }

                if (state.Scene != NetworkSceneId.None)
                {
                    scene = state.Scene;
                }
            }

            if (role == PlayerRole.None)
            {
                role = SceneFlowController.Instance?.Definition?.ResolveDefaultRoleForScene(scene) ?? PlayerRole.None;
            }
        }

        private Transform FindSpawnPoint(PlayerRole role, NetworkSceneId scene)
        {
            return FindObjectsByType<PlayerSpawnPoint>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(point => point.gameObject.scene == gameObject.scene && point.Matches(role, scene))
                .OrderBy(point => point.Priority)
                .ThenBy(point => point.name)
                .Select(point => point.transform)
                .FirstOrDefault();
        }
    }
}
