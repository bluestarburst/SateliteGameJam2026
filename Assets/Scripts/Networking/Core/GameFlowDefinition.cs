using System;
using System.Collections.Generic;
using SatelliteGameJam.Networking.Messages;
using UnityEngine;

namespace SatelliteGameJam.Networking.Core
{
    public enum GameModeType
    {
        Title = 0,
        Matchmaking = 1,
        Lobby = 2,
        Gameplay = 3,
        PostGame = 4,
        Sandbox = 5
    }

    [Serializable]
    public class FlowSceneEntry
    {
        public NetworkSceneId sceneId = NetworkSceneId.None;
        public string sceneName = string.Empty;
        public GameModeType modeType = GameModeType.Gameplay;
        [Tooltip("Optional: if empty, this scene can be entered by any role.")]
        public PlayerRole[] allowedRoles = Array.Empty<PlayerRole>();
    }

    [Serializable]
    public class DevSessionProfile
    {
        [Tooltip("Only used in the Unity Editor and development builds.")]
        public bool enabled = false;
        [Tooltip("Create a public, joinable Steam lobby while keeping the currently open scene active.")]
        public bool createJoinableLobby = true;
        [Tooltip("None derives the local role from the active scene's role mapping.")]
        public PlayerRole localRoleOverride = PlayerRole.None;
        [Tooltip("None assigns the complementary gameplay role when possible.")]
        public PlayerRole joiningPlayerRole = PlayerRole.None;
        [Tooltip("None derives the joining player's scene from their assigned role.")]
        public NetworkSceneId joiningPlayerSceneOverride = NetworkSceneId.None;
    }

    [CreateAssetMenu(fileName = "GameFlowDefinition", menuName = "Networking/Game Flow Definition", order = 2)]
    public class GameFlowDefinition : ScriptableObject
    {
        [Header("Core Scene Mapping")]
        [SerializeField] private List<FlowSceneEntry> scenes = new List<FlowSceneEntry>();

        [Header("Well Known Scenes")]
        [SerializeField] private NetworkSceneId matchmakingScene = NetworkSceneId.Matchmaking;
        [SerializeField] private NetworkSceneId lobbyScene = NetworkSceneId.Lobby;

        [Header("Development Session")]
        [SerializeField] private DevSessionProfile devSession = new DevSessionProfile();

        public IReadOnlyList<FlowSceneEntry> Scenes => scenes;
        public NetworkSceneId MatchmakingScene => matchmakingScene;
        public NetworkSceneId LobbyScene => lobbyScene;
        public DevSessionProfile DevSession => devSession;

        public bool TryGetSceneEntry(NetworkSceneId sceneId, out FlowSceneEntry entry)
        {
            entry = scenes.Find(s => s.sceneId == sceneId);
            return entry != null;
        }

        public bool TryGetSceneEntryByName(string sceneName, out FlowSceneEntry entry)
        {
            entry = scenes.Find(s => string.Equals(s.sceneName, sceneName, StringComparison.Ordinal));
            return entry != null;
        }

        public string ResolveSceneName(NetworkSceneId sceneId)
        {
            return TryGetSceneEntry(sceneId, out FlowSceneEntry entry) ? entry.sceneName : string.Empty;
        }

        public NetworkSceneId ResolveSceneForRole(PlayerRole role, NetworkSceneId fallback)
        {
            FlowSceneEntry entry = scenes.Find(candidate =>
                candidate.allowedRoles != null &&
                Array.Exists(candidate.allowedRoles, allowedRole => allowedRole == role));
            return entry != null ? entry.sceneId : fallback;
        }

        public PlayerRole ResolveDefaultRoleForScene(NetworkSceneId sceneId)
        {
            if (sceneId == lobbyScene || sceneId == matchmakingScene)
            {
                return PlayerRole.Lobby;
            }

            if (TryGetSceneEntry(sceneId, out FlowSceneEntry entry) &&
                entry.allowedRoles != null &&
                entry.allowedRoles.Length == 1)
            {
                return entry.allowedRoles[0];
            }

            return PlayerRole.None;
        }

        public PlayerRole ResolveDevelopmentLocalRole(NetworkSceneId activeScene)
        {
            return devSession.enabled && devSession.localRoleOverride != PlayerRole.None
                ? devSession.localRoleOverride
                : ResolveDefaultRoleForScene(activeScene);
        }

        public PlayerRole ResolveDevelopmentJoinRole(PlayerRole localRole)
        {
            if (devSession.joiningPlayerRole != PlayerRole.None)
            {
                return devSession.joiningPlayerRole;
            }

            switch (localRole)
            {
                case PlayerRole.GroundControl:
                    return PlayerRole.SpaceStation;
                case PlayerRole.SpaceStation:
                    return PlayerRole.GroundControl;
                default:
                    return PlayerRole.Lobby;
            }
        }

        public NetworkSceneId ResolveDevelopmentJoinScene(PlayerRole joiningRole)
        {
            if (devSession.joiningPlayerSceneOverride != NetworkSceneId.None)
            {
                return devSession.joiningPlayerSceneOverride;
            }

            if (joiningRole == PlayerRole.Lobby || joiningRole == PlayerRole.None)
            {
                return lobbyScene;
            }

            return ResolveSceneForRole(joiningRole, NetworkSceneId.None);
        }

        public bool IsSceneAllowedForRole(NetworkSceneId sceneId, PlayerRole role)
        {
            if (!TryGetSceneEntry(sceneId, out FlowSceneEntry entry))
            {
                return false;
            }

            if (entry.allowedRoles == null || entry.allowedRoles.Length == 0)
            {
                return true;
            }

            foreach (PlayerRole allowed in entry.allowedRoles)
            {
                if (allowed == role)
                {
                    return true;
                }
            }

            return false;
        }

        public void Validate()
        {
            var sceneIds = new HashSet<NetworkSceneId>();
            var sceneNames = new HashSet<string>();

            foreach (FlowSceneEntry scene in scenes)
            {
                if (!sceneIds.Add(scene.sceneId))
                {
                    Debug.LogWarning($"[GameFlowDefinition] Duplicate scene id mapping: {scene.sceneId}", this);
                }

                if (string.IsNullOrWhiteSpace(scene.sceneName))
                {
                    Debug.LogWarning($"[GameFlowDefinition] Scene {scene.sceneId} has an empty scene name.", this);
                }
                else if (!sceneNames.Add(scene.sceneName))
                {
                    Debug.LogWarning($"[GameFlowDefinition] Duplicate scene name mapping: {scene.sceneName}", this);
                }
            }

            var mappedRoles = new HashSet<PlayerRole>();
            foreach (FlowSceneEntry scene in scenes)
            {
                if (scene.allowedRoles == null)
                {
                    continue;
                }

                foreach (PlayerRole role in scene.allowedRoles)
                {
                    if (role != PlayerRole.None && !mappedRoles.Add(role))
                    {
                        Debug.LogWarning($"[GameFlowDefinition] Role {role} is assigned to more than one scene.", this);
                    }
                }
            }

            if (devSession.enabled)
            {
                PlayerRole joinRole = ResolveDevelopmentJoinRole(devSession.localRoleOverride);
                NetworkSceneId joinScene = ResolveDevelopmentJoinScene(joinRole);
                if (joinScene == NetworkSceneId.None || !sceneIds.Contains(joinScene))
                {
                    Debug.LogWarning("[GameFlowDefinition] Development join assignment resolves to an unmapped scene.", this);
                }
            }
        }

        private void OnValidate()
        {
            Validate();
        }
    }
}
