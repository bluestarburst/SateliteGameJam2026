using UnityEngine;

namespace SatelliteGameJam.Networking.Core
{
    /// <summary>
    /// Inspector facade for the SteamPack prefab. It keeps high-touch config in one place
    /// while the specialized runtime managers remain separate components.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Networking/Steam Pack Config")]
    public class SteamPackConfig : MonoBehaviour
    {
        [Header("Shared Assets")]
        [SerializeField] private NetworkingConfiguration networkingConfiguration;
        [SerializeField] private GameFlowDefinition gameFlowDefinition;
        [SerializeField] private RoleVisualProfile roleVisualProfile;

        [Header("Player Prefabs")]
        [SerializeField] private GameObject remotePlayerPrefab;

        [Header("Fallback Scene Names")]
        [SerializeField] private string matchmakingSceneName = "Matchmaking";
        [SerializeField] private string lobbySceneName = "Lobby";
        [SerializeField] private string groundControlSceneName = "BaseStation";
        [SerializeField] private string spaceStationSceneName = "Satellite";

        public NetworkingConfiguration NetworkingConfiguration => networkingConfiguration;
        public GameFlowDefinition GameFlowDefinition => gameFlowDefinition;
        public RoleVisualProfile RoleVisualProfile => roleVisualProfile;
        public GameObject RemotePlayerPrefab => remotePlayerPrefab;
        public string MatchmakingSceneName => matchmakingSceneName;
        public string LobbySceneName => lobbySceneName;
        public string GroundControlSceneName => groundControlSceneName;
        public string SpaceStationSceneName => spaceStationSceneName;

        private void Reset()
        {
            networkingConfiguration = Resources.Load<NetworkingConfiguration>("NetworkingConfig");
            if (networkingConfiguration != null)
            {
                gameFlowDefinition = networkingConfiguration.gameFlowDefinition;
                remotePlayerPrefab = networkingConfiguration.remotePlayerPrefab;
            }

            roleVisualProfile = Resources.Load<RoleVisualProfile>("RoleVisualProfile");
        }
    }
}
