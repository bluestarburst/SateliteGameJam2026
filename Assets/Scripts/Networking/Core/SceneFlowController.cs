using SatelliteGameJam.Networking.Messages;
using SatelliteGameJam.Networking.State;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SatelliteGameJam.Networking.Core
{
    /// <summary>
    /// Single authority for local scene resolution and scene loading.
    /// </summary>
    public class SceneFlowController : MonoBehaviour
    {
        public static SceneFlowController Instance { get; private set; }

        [Header("Flow Definition")]
        [SerializeField] private GameFlowDefinition gameFlowDefinition;
        [SerializeField] private NetworkingConfiguration networkingConfiguration;

        [Header("Fallbacks")]
        [SerializeField] private string fallbackMatchmakingSceneName = "Matchmaking";
        [SerializeField] private string fallbackLobbySceneName = "Lobby";
        [SerializeField] private string fallbackGroundSceneName = "GroundControl";
        [SerializeField] private string fallbackSpaceSceneName = "SpaceStation";

        [Header("Debug")]
        [SerializeField] private bool verboseLogging = true;

        public GameFlowDefinition Definition => ResolveFlowDefinition();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
            }
        }

        public string ResolveSceneName(NetworkSceneId sceneId)
        {
            GameFlowDefinition flowDefinition = ResolveFlowDefinition();
            if (flowDefinition != null)
            {
                if (flowDefinition.TryGetSceneEntry(sceneId, out FlowSceneEntry entry))
                {
                    if (sceneId == NetworkSceneId.GroundControl || sceneId == NetworkSceneId.SpaceStation)
                    {
                        if (entry.modeType == GameModeType.Lobby || entry.modeType == GameModeType.Matchmaking)
                        {
                            Debug.LogWarning($"[SceneFlowController] Invalid flow mapping for gameplay scene {sceneId}: {entry.sceneName} ({entry.modeType}). Falling back to NetworkingConfiguration.");
                        }
                        else
                        {
                            return entry.sceneName;
                        }
                    }
                    else if (!string.IsNullOrWhiteSpace(entry.sceneName))
                    {
                        return entry.sceneName;
                    }
                }
            }

            if (networkingConfiguration != null)
            {
                string mapped = networkingConfiguration.GetSceneName(sceneId);
                if (!string.IsNullOrWhiteSpace(mapped))
                {
                    return mapped;
                }
            }

            switch (sceneId)
            {
                case NetworkSceneId.Matchmaking:
                    return fallbackMatchmakingSceneName;
                case NetworkSceneId.Lobby:
                    return fallbackLobbySceneName;
                case NetworkSceneId.GroundControl:
                    return fallbackGroundSceneName;
                case NetworkSceneId.SpaceStation:
                    return fallbackSpaceSceneName;
                default:
                    return string.Empty;
            }
        }

        public bool LoadSceneForLocal(NetworkSceneId sceneId)
        {
            string sceneName = ResolveSceneName(sceneId);
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogWarning($"[SceneFlowController] Unable to resolve scene for {sceneId}");
                return false;
            }

            if (SceneManager.GetActiveScene().name == sceneName)
            {
                return true;
            }

            GameFlowDefinition flowDefinition = ResolveFlowDefinition();
            if (flowDefinition != null && flowDefinition.TryGetSceneEntry(sceneId, out FlowSceneEntry entry))
            {
                entry.onWillEnter?.Invoke();
            }

            if (verboseLogging)
            {
                Debug.Log($"[SceneFlowController] Loading scene '{sceneName}' for {sceneId}");
            }

            SceneManager.LoadScene(sceneName);
            return true;
        }

        public bool LoadLobbyScene()
        {
            GameFlowDefinition flowDefinition = ResolveFlowDefinition();
            NetworkSceneId lobbyScene = flowDefinition != null ? flowDefinition.LobbyScene : NetworkSceneId.Lobby;
            return LoadSceneForLocal(lobbyScene);
        }

        public bool LoadMatchmakingScene()
        {
            GameFlowDefinition flowDefinition = ResolveFlowDefinition();
            if (flowDefinition != null && flowDefinition.MatchmakingScene != NetworkSceneId.None)
            {
                return LoadSceneForLocal(flowDefinition.MatchmakingScene);
            }

            if (string.IsNullOrWhiteSpace(fallbackMatchmakingSceneName))
            {
                return false;
            }

            SceneManager.LoadScene(fallbackMatchmakingSceneName);
            return true;
        }

        public NetworkSceneId ResolveGameplaySceneForRole(PlayerRole role)
        {
            GameFlowDefinition flowDefinition = ResolveFlowDefinition();
            if (flowDefinition != null)
            {
                NetworkSceneId fallback = role == PlayerRole.SpaceStation
                    ? NetworkSceneId.SpaceStation
                    : NetworkSceneId.GroundControl;
                return flowDefinition.ResolveSceneForRole(role, fallback);
            }

            return role == PlayerRole.SpaceStation
                ? NetworkSceneId.SpaceStation
                : NetworkSceneId.GroundControl;
        }

        public bool IsLobbyOrMatchmakingScene(string sceneName)
        {
            GameFlowDefinition flowDefinition = ResolveFlowDefinition();
            if (flowDefinition != null && flowDefinition.TryGetSceneEntryByName(sceneName, out FlowSceneEntry entry))
            {
                return entry.modeType == GameModeType.Lobby || entry.modeType == GameModeType.Matchmaking;
            }

            return string.Equals(sceneName, fallbackLobbySceneName) || string.Equals(sceneName, fallbackMatchmakingSceneName);
        }

        public bool CanHostStartGame(out string reason)
        {
            reason = string.Empty;

            if (SteamManager.Instance == null || PlayerStateManager.Instance == null)
            {
                reason = "Required networking managers are not ready.";
                return false;
            }

            if (!IsLobbyScene(SceneManager.GetActiveScene().name))
            {
                reason = "Game can only start from the Lobby scene.";
                return false;
            }

            bool hasGroundControl = false;
            bool hasSpaceStation = false;

            foreach (var member in SteamManager.Instance.currentLobby.Members)
            {
                PlayerState state = PlayerStateManager.Instance.GetPlayerState(member.Id);
                if (state.Role == PlayerRole.None || state.Role == PlayerRole.Lobby)
                {
                    reason = "Every player must pick a gameplay role before starting.";
                    return false;
                }

                hasGroundControl |= state.Role == PlayerRole.GroundControl;
                hasSpaceStation |= state.Role == PlayerRole.SpaceStation;
            }

            if (!hasGroundControl || !hasSpaceStation)
            {
                reason = "At least one Ground Control player and one Space Station player are required.";
                return false;
            }

            return true;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            GameFlowDefinition flowDefinition = ResolveFlowDefinition();
            if (flowDefinition == null)
            {
                return;
            }

            if (flowDefinition.TryGetSceneEntryByName(scene.name, out FlowSceneEntry entry))
            {
                entry.onDidEnter?.Invoke();
            }
        }

        private bool IsLobbyScene(string sceneName)
        {
            GameFlowDefinition flowDefinition = ResolveFlowDefinition();
            if (flowDefinition != null && flowDefinition.TryGetSceneEntryByName(sceneName, out FlowSceneEntry entry))
            {
                return entry.modeType == GameModeType.Lobby;
            }

            return string.Equals(sceneName, fallbackLobbySceneName);
        }

        private GameFlowDefinition ResolveFlowDefinition()
        {
            if (gameFlowDefinition != null)
            {
                return gameFlowDefinition;
            }

            return networkingConfiguration != null ? networkingConfiguration.gameFlowDefinition : null;
        }
    }
}
