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

        public GameFlowDefinition Definition => gameFlowDefinition;

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
            if (gameFlowDefinition != null)
            {
                string mapped = gameFlowDefinition.ResolveSceneName(sceneId);
                if (!string.IsNullOrWhiteSpace(mapped))
                {
                    return mapped;
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

            if (gameFlowDefinition != null && gameFlowDefinition.TryGetSceneEntry(sceneId, out FlowSceneEntry entry))
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
            NetworkSceneId lobbyScene = gameFlowDefinition != null ? gameFlowDefinition.LobbyScene : NetworkSceneId.Lobby;
            return LoadSceneForLocal(lobbyScene);
        }

        public bool LoadMatchmakingScene()
        {
            if (gameFlowDefinition != null && gameFlowDefinition.MatchmakingScene != NetworkSceneId.None)
            {
                return LoadSceneForLocal(gameFlowDefinition.MatchmakingScene);
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
            if (gameFlowDefinition != null)
            {
                NetworkSceneId fallback = role == PlayerRole.SpaceStation
                    ? NetworkSceneId.SpaceStation
                    : NetworkSceneId.GroundControl;
                return gameFlowDefinition.ResolveSceneForRole(role, fallback);
            }

            return role == PlayerRole.SpaceStation
                ? NetworkSceneId.SpaceStation
                : NetworkSceneId.GroundControl;
        }

        public bool IsLobbyOrMatchmakingScene(string sceneName)
        {
            if (gameFlowDefinition != null && gameFlowDefinition.TryGetSceneEntryByName(sceneName, out FlowSceneEntry entry))
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

            foreach (var member in SteamManager.Instance.currentLobby.Members)
            {
                PlayerState state = PlayerStateManager.Instance.GetPlayerState(member.Id);
                if (state.Role == PlayerRole.None || state.Role == PlayerRole.Lobby)
                {
                    reason = "Every player must pick a gameplay role before starting.";
                    return false;
                }
            }

            return true;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (gameFlowDefinition == null)
            {
                return;
            }

            if (gameFlowDefinition.TryGetSceneEntryByName(scene.name, out FlowSceneEntry entry))
            {
                entry.onDidEnter?.Invoke();
            }
        }
    }
}
