using System;
using System.Collections.Generic;
using System.Linq;
using SatelliteGameJam.Networking;
using SatelliteGameJam.Networking.Messages;
using SatelliteGameJam.Networking.State;
using SatelliteGameJam.Networking.Sync;
using SatelliteGameJam.Players;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SatelliteGameJam.Networking.Core.Editor
{
    /// <summary>
    /// Validates the configured game-flow scenes without entering Play mode.
    /// Run from Tools/Networking or with -executeMethod in CI.
    /// </summary>
    public static class GameFlowConfigurationValidator
    {
        [MenuItem("Tools/Networking/Validate Game Flow")]
        public static void ValidateFromMenu()
        {
            Validate();
        }

        public static void ValidateFromCommandLine()
        {
            Validate();
        }

        private static void Validate()
        {
            var errors = new List<string>();
            NetworkingConfiguration networkingConfig = Resources.Load<NetworkingConfiguration>("NetworkingConfig");
            GameFlowDefinition flow = networkingConfig != null ? networkingConfig.gameFlowDefinition : null;

            if (networkingConfig == null)
            {
                errors.Add("Resources/NetworkingConfig is missing.");
            }

            if (flow == null)
            {
                errors.Add("NetworkingConfig has no GameFlowDefinition assigned.");
                ThrowIfInvalid(errors);
                return;
            }

            var buildScenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .ToDictionary(scene => System.IO.Path.GetFileNameWithoutExtension(scene.path), scene => scene.path);

            foreach (FlowSceneEntry entry in flow.Scenes)
            {
                if (entry.sceneId == NetworkSceneId.None)
                {
                    errors.Add("GameFlowDefinition contains a scene entry with id None.");
                    continue;
                }

                if (!buildScenes.TryGetValue(entry.sceneName, out string scenePath))
                {
                    errors.Add($"Flow scene '{entry.sceneName}' ({entry.sceneId}) is not enabled in Build Settings.");
                    continue;
                }

                ValidateScene(scenePath, entry, networkingConfig, flow, errors);
            }

            ValidateDevelopmentAssignments(flow, errors);
            ThrowIfInvalid(errors);
            Debug.Log($"[GameFlowValidator] Validated {flow.Scenes.Count} flow scenes successfully.");
        }

        private static void ValidateDevelopmentAssignments(GameFlowDefinition flow, List<string> errors)
        {
            if (!flow.DevSession.enabled)
            {
                return;
            }

            foreach (FlowSceneEntry entry in flow.Scenes)
            {
                PlayerRole localRole = flow.ResolveDevelopmentLocalRole(entry.sceneId);
                PlayerRole joiningRole = flow.ResolveDevelopmentJoinRole(localRole);
                NetworkSceneId joiningScene = flow.ResolveDevelopmentJoinScene(joiningRole);

                if (joiningRole == PlayerRole.None || joiningScene == NetworkSceneId.None)
                {
                    errors.Add($"{entry.sceneName}: development join assignment is incomplete.");
                    continue;
                }

                if (joiningRole != PlayerRole.Lobby && !flow.IsSceneAllowedForRole(joiningScene, joiningRole))
                {
                    errors.Add($"{entry.sceneName}: development joiner role {joiningRole} is not allowed in {joiningScene}.");
                }
            }
        }

        private static void ValidateScene(
            string scenePath,
            FlowSceneEntry entry,
            NetworkingConfiguration networkingConfig,
            GameFlowDefinition flow,
            List<string> errors)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                SteamPackConfig steamPack = FindInScene<SteamPackConfig>(scene);
                if (steamPack == null)
                {
                    errors.Add($"{entry.sceneName}: SteamPackConfig is missing.");
                    return;
                }

                if (steamPack.NetworkingConfiguration != networkingConfig)
                {
                    errors.Add($"{entry.sceneName}: SteamPack references a different NetworkingConfiguration asset.");
                }

                if (steamPack.GameFlowDefinition != flow)
                {
                    errors.Add($"{entry.sceneName}: SteamPack does not resolve the validated GameFlowDefinition.");
                }

                if (steamPack.GetComponent<SteamManager>() == null ||
                    steamPack.GetComponent<NetworkConnectionManager>() == null ||
                    steamPack.GetComponent<SceneFlowController>() == null ||
                    steamPack.GetComponent<SceneSyncManager>() == null ||
                    steamPack.GetComponent<PlayerStateManager>() == null)
                {
                    errors.Add($"{entry.sceneName}: SteamPack is missing one or more required flow managers.");
                }

                GameFlowManager gameFlowManager = steamPack.GetComponent<GameFlowManager>();
                if (gameFlowManager == null || !gameFlowManager.enabled)
                {
                    errors.Add($"{entry.sceneName}: SteamPack requires an enabled GameFlowManager.");
                }

                if (entry.modeType == GameModeType.Gameplay)
                {
                    if (flowRole(entry) == PlayerRole.None)
                    {
                        errors.Add($"{entry.sceneName}: gameplay scene does not resolve to a default player role.");
                    }

                    if (FindInScene<LocalPlayerNetworkSetup>(scene) == null &&
                        FindInScene<ScenePlayerBootstrap>(scene) == null)
                    {
                        errors.Add($"{entry.sceneName}: gameplay scene has no LocalPlayerNetworkSetup or ScenePlayerBootstrap.");
                    }
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }

            PlayerRole flowRole(FlowSceneEntry flowEntry)
            {
                return flowEntry.allowedRoles != null && flowEntry.allowedRoles.Length == 1
                    ? flowEntry.allowedRoles[0]
                    : PlayerRole.None;
            }
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T component = root.GetComponentInChildren<T>(true);
                if (component != null)
                {
                    return component;
                }
            }

            return null;
        }

        private static void ThrowIfInvalid(List<string> errors)
        {
            if (errors.Count == 0)
            {
                return;
            }

            string message = "[GameFlowValidator]\n- " + string.Join("\n- ", errors);
            Debug.LogError(message);
            throw new InvalidOperationException(message);
        }
    }
}
