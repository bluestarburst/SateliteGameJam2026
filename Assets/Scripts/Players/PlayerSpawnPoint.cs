using SatelliteGameJam.Networking.Messages;
using UnityEngine;

namespace SatelliteGameJam.Players
{
    /// <summary>Named, scene-local spawn location for a playable rig.</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerSpawnPoint : MonoBehaviour
    {
        [SerializeField] private PlayerRole role = PlayerRole.None;
        [SerializeField] private NetworkSceneId scene = NetworkSceneId.None;
        [SerializeField] private int priority;

        public PlayerRole Role => role;
        public NetworkSceneId Scene => scene;
        public int Priority => priority;

        public bool Matches(PlayerRole requestedRole, NetworkSceneId requestedScene)
        {
            return (role == PlayerRole.None || role == requestedRole) &&
                (scene == NetworkSceneId.None || scene == requestedScene);
        }
    }
}
