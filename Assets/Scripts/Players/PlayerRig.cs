using SatelliteGameJam.Networking.Messages;
using SatelliteGameJam.Networking.Identity;
using SatelliteGameJam.Networking.Sync;
using UnityEngine;

namespace SatelliteGameJam.Players
{
    /// <summary>
    /// Marker and contract for a locally playable role-specific player prefab.
    /// It intentionally contains no input or movement implementation; those stay on the role rig.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkIdentity))]
    [RequireComponent(typeof(LocalPlayerNetworkSetup))]
    public sealed class PlayerRig : MonoBehaviour
    {
        [Header("Role Contract")]
        [SerializeField] private PlayerRole role = PlayerRole.None;
        [SerializeField] private NetworkSceneId scene = NetworkSceneId.None;

        [Header("Presentation")]
        [SerializeField] private Transform cameraRoot;
        [SerializeField] private Transform interactionOrigin;

        public PlayerRole Role => role;
        public NetworkSceneId Scene => scene;
        public Transform CameraRoot => cameraRoot;
        public Transform InteractionOrigin => interactionOrigin;
    }
}
