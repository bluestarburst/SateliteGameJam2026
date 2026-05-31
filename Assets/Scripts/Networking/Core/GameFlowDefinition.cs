using System;
using System.Collections.Generic;
using SatelliteGameJam.Networking.Messages;
using UnityEngine;
using UnityEngine.Events;

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

    public enum DevStartupMode
    {
        Normal = 0,
        AutoCreateLobby = 1,
        AutoJoinByCode = 2,
        SkipToLobby = 3,
        SkipToGameplay = 4
    }

    [Serializable]
    public class FlowSceneEntry
    {
        public NetworkSceneId sceneId = NetworkSceneId.None;
        public string sceneName = string.Empty;
        public GameModeType modeType = GameModeType.Gameplay;
        [Tooltip("Optional: if empty, this scene can be entered by any role.")]
        public PlayerRole[] allowedRoles = Array.Empty<PlayerRole>();
        [Tooltip("Invoked before a local transition into this scene.")]
        public UnityEvent onWillEnter = new UnityEvent();
        [Tooltip("Invoked after the local transition has finished loading.")]
        public UnityEvent onDidEnter = new UnityEvent();
    }

    [Serializable]
    public class RoleSceneRule
    {
        public PlayerRole role = PlayerRole.None;
        public NetworkSceneId targetScene = NetworkSceneId.None;
    }

    [Serializable]
    public class DevStartupProfile
    {
        public bool enabled = false;
        public DevStartupMode mode = DevStartupMode.Normal;
        public PlayerRole forcedRole = PlayerRole.None;
        public bool autoStartWhenMinimumPeers;
        [Min(1)] public int minimumPeers = 2;
        public NetworkSceneId gameplayOverrideScene = NetworkSceneId.None;
        [Tooltip("Optional lobby id used for AutoJoinByCode flow.")]
        public ulong autoJoinLobbyId;
    }

    [CreateAssetMenu(fileName = "GameFlowDefinition", menuName = "Networking/Game Flow Definition", order = 2)]
    public class GameFlowDefinition : ScriptableObject
    {
        [Header("Core Scene Mapping")]
        [SerializeField] private List<FlowSceneEntry> scenes = new List<FlowSceneEntry>();

        [Header("Role To Scene Mapping")]
        [SerializeField] private List<RoleSceneRule> roleSceneRules = new List<RoleSceneRule>();

        [Header("Well Known Scenes")]
        [SerializeField] private NetworkSceneId matchmakingScene = NetworkSceneId.None;
        [SerializeField] private NetworkSceneId lobbyScene = NetworkSceneId.Lobby;

        [Header("Dev Startup")]
        [SerializeField] private DevStartupProfile devStartup = new DevStartupProfile();

        public IReadOnlyList<FlowSceneEntry> Scenes => scenes;
        public IReadOnlyList<RoleSceneRule> RoleSceneRules => roleSceneRules;
        public NetworkSceneId MatchmakingScene => matchmakingScene;
        public NetworkSceneId LobbyScene => lobbyScene;
        public DevStartupProfile DevStartup => devStartup;

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
            RoleSceneRule rule = roleSceneRules.Find(r => r.role == role);
            return rule != null ? rule.targetScene : fallback;
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
            foreach (RoleSceneRule rule in roleSceneRules)
            {
                if (!mappedRoles.Add(rule.role))
                {
                    Debug.LogWarning($"[GameFlowDefinition] Duplicate role mapping for role {rule.role}", this);
                }

                if (!sceneIds.Contains(rule.targetScene))
                {
                    Debug.LogWarning($"[GameFlowDefinition] Role {rule.role} targets unmapped scene {rule.targetScene}", this);
                }
            }
        }

        private void OnValidate()
        {
            Validate();
        }
    }
}
