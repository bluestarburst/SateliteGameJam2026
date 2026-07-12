using Steamworks;
using SatelliteGameJam.Networking.Messages;
using UnityEngine;

namespace SatelliteGameJam.Networking.Identity
{
    public enum NetworkPlayerKind
    {
        Unknown = 0,
        Local = 1,
        Remote = 2
    }

    /// <summary>
    /// Inspector-friendly player label for spawned local and remote network avatars.
    /// </summary>
    [DisallowMultipleComponent]
    public class NetworkPlayerTag : MonoBehaviour
    {
        [SerializeField] private NetworkPlayerKind playerKind = NetworkPlayerKind.Unknown;
        [SerializeField] private ulong steamId;
        [SerializeField] private string displayName = string.Empty;
        [SerializeField] private PlayerRole role = PlayerRole.None;
        [SerializeField] private NetworkSceneId scene = NetworkSceneId.None;

        public NetworkPlayerKind PlayerKind => playerKind;
        public SteamId SteamId => steamId;
        public string DisplayName => displayName;
        public PlayerRole Role => role;
        public NetworkSceneId Scene => scene;

        public void Configure(
            SteamId playerSteamId,
            string playerDisplayName,
            NetworkPlayerKind kind,
            PlayerRole playerRole,
            NetworkSceneId playerScene)
        {
            steamId = playerSteamId;
            displayName = playerDisplayName ?? string.Empty;
            playerKind = kind;
            role = playerRole;
            scene = playerScene;
        }

        public void ApplyState(PlayerRole playerRole, NetworkSceneId playerScene)
        {
            role = playerRole;
            scene = playerScene;
        }
    }
}
