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
        [SerializeField] private RoleVisualProfile roleVisualProfile;

        public NetworkingConfiguration NetworkingConfiguration => networkingConfiguration;
        public GameFlowDefinition GameFlowDefinition => networkingConfiguration != null
            ? networkingConfiguration.gameFlowDefinition
            : null;
        public RoleVisualProfile RoleVisualProfile => roleVisualProfile;
        public GameObject RemotePlayerPrefab => networkingConfiguration != null
            ? networkingConfiguration.remotePlayerPrefab
            : null;
        private void Reset()
        {
            networkingConfiguration = Resources.Load<NetworkingConfiguration>("NetworkingConfig");
            roleVisualProfile = Resources.Load<RoleVisualProfile>("RoleVisualProfile");
        }
    }
}
