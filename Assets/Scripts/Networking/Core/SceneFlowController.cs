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
        [HideInInspector]
        [SerializeField] private NetworkingConfiguration networkingConfiguration;

        private const string FallbackMatchmakingSceneName = "Matchmaking";
        private const string FallbackLobbySceneName = "Lobby";
        private const string FallbackGroundSceneName = "BaseStation";
        private const string FallbackSpaceSceneName = "Satellite";

        [Header("Debug")]
        [SerializeField] private bool verboseLogging = true;

        // SceneManager.LoadScene calls can overlap while a Steam lobby callback and a prior UI
        // route arrive in the same frame. Keep the host-assigned destination authoritative.
        private NetworkSceneId requestedLocalScene = NetworkSceneId.None;
        private bool reassertingRequestedScene;

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

        private void Start()
        {
            SynchronizeLocalPresence(SceneManager.GetActiveScene().name);
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
                    return FallbackMatchmakingSceneName;
                case NetworkSceneId.Lobby:
                    return FallbackLobbySceneName;
                case NetworkSceneId.GroundControl:
                    return FallbackGroundSceneName;
                case NetworkSceneId.SpaceStation:
                    return FallbackSpaceSceneName;
                default:
                    return string.Empty;
            }
        }

        public bool TryGetSceneId(string sceneName, out NetworkSceneId sceneId)
        {
            GameFlowDefinition flowDefinition = ResolveFlowDefinition();
            if (flowDefinition != null && flowDefinition.TryGetSceneEntryByName(sceneName, out FlowSceneEntry entry))
            {
                sceneId = entry.sceneId;
                return true;
            }

            sceneId = NetworkSceneId.None;
            return false;
        }

        public PlayerRole ResolveDefaultRoleForScene(NetworkSceneId sceneId)
        {
            GameFlowDefinition flowDefinition = ResolveFlowDefinition();
            return flowDefinition != null
                ? flowDefinition.ResolveDefaultRoleForScene(sceneId)
                : PlayerRole.None;
        }

        public bool LoadSceneForLocal(NetworkSceneId sceneId)
        {
            string sceneName = ResolveSceneName(sceneId);
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogWarning($"[SceneFlowController] Unable to resolve scene for {sceneId}");
                return false;
            }

            requestedLocalScene = sceneId;

            if (SceneManager.GetActiveScene().name == sceneName)
            {
                requestedLocalScene = NetworkSceneId.None;
                return true;
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

            SceneManager.LoadScene(FallbackMatchmakingSceneName);
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

            return string.Equals(sceneName, FallbackLobbySceneName) || string.Equals(sceneName, FallbackMatchmakingSceneName);
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
            if (requestedLocalScene != NetworkSceneId.None &&
                TryGetSceneId(scene.name, out NetworkSceneId loadedScene) &&
                loadedScene != requestedLocalScene)
            {
                if (verboseLogging)
                {
                    Debug.Log($"[SceneFlowController] Ignoring stale scene '{scene.name}'; waiting for {requestedLocalScene}.");
                }

                if (!reassertingRequestedScene)
                {
                    StartCoroutine(ReassertRequestedScene());
                }

                return;
            }

            requestedLocalScene = NetworkSceneId.None;
            SynchronizeLocalPresence(scene.name);

        }

        private System.Collections.IEnumerator ReassertRequestedScene()
        {
            reassertingRequestedScene = true;
            yield return null;

            NetworkSceneId target = requestedLocalScene;
            reassertingRequestedScene = false;
            if (target != NetworkSceneId.None)
            {
                LoadSceneForLocal(target);
            }
        }

        private void SynchronizeLocalPresence(string sceneName)
        {
            if (!TryGetSceneId(sceneName, out NetworkSceneId sceneId) ||
                SteamManager.Instance == null ||
                SteamManager.Instance.PlayerSteamId.Value == 0 ||
                PlayerStateManager.Instance == null)
            {
                return;
            }

            PlayerState state = PlayerStateManager.Instance.GetPlayerState(SteamManager.Instance.PlayerSteamId);
            PlayerRole role = ResolveDefaultRoleForScene(sceneId);
            if (role != PlayerRole.None && state.Role != role)
            {
                PlayerStateManager.Instance.SetLocalPlayerRole(role);
            }

            if (state.Scene != sceneId)
            {
                PlayerStateManager.Instance.SetLocalPlayerScene(sceneId);
            }
        }

        private bool IsLobbyScene(string sceneName)
        {
            GameFlowDefinition flowDefinition = ResolveFlowDefinition();
            if (flowDefinition != null && flowDefinition.TryGetSceneEntryByName(sceneName, out FlowSceneEntry entry))
            {
                return entry.modeType == GameModeType.Lobby;
            }

            return string.Equals(sceneName, FallbackLobbySceneName);
        }

        private GameFlowDefinition ResolveFlowDefinition()
        {
            return networkingConfiguration != null
                ? networkingConfiguration.gameFlowDefinition
                : NetworkingConfiguration.Instance?.gameFlowDefinition;
        }
    }
}
