using SatelliteGameJam.Networking.State;
using SatelliteGameJam.Networking.Messages;
using SatelliteGameJam.Networking.Voice;
using SatelliteGameJam.Networking.Core;
using UnityEngine;

namespace SatelliteGameJam.SceneManagers
{
    /// <summary>
    /// Scene bootstrap for Space Station.
    /// Sets local player role/scene, provides helpers to modify satellite state, and spawns remote players.
    /// </summary>
    public class SpaceStationSceneManager : MonoBehaviour
    {
        private void Start()
        {
            // Set player role and scene
            if (PlayerStateManager.Instance != null)
            {
                PlayerStateManager.Instance.SetLocalPlayerRole(PlayerRole.SpaceStation);
                PlayerStateManager.Instance.SetLocalPlayerScene(NetworkSceneId.SpaceStation);
            }

            // Spawn remote players already in the scene
            if (PlayerStateManager.Instance != null && SteamManager.Instance != null)
            {
                SpawnExistingRemotePlayers();
            }

            // Subscribe to player join/leave events
            if (PlayerStateManager.Instance != null)
            {
                PlayerStateManager.Instance.OnPlayerJoined += OnRemotePlayerJoined;
                PlayerStateManager.Instance.OnPlayerLeft += OnRemotePlayerLeft;
            }
        }

        private void OnDestroy()
        {
            if (PlayerStateManager.Instance != null)
            {
                PlayerStateManager.Instance.OnPlayerJoined -= OnRemotePlayerJoined;
                PlayerStateManager.Instance.OnPlayerLeft -= OnRemotePlayerLeft;
            }
        }

        private void SpawnExistingRemotePlayers()
        {
            if (SteamManager.Instance?.currentLobby.Id.Value == 0) return;

            foreach (var member in SteamManager.Instance.currentLobby.Members)
            {
                if (member.Id == SteamManager.Instance.PlayerSteamId) continue;

                var state = PlayerStateManager.Instance.GetPlayerState(member.Id);
                // Only spawn if they're in the same scene
                if (state.Scene == NetworkSceneId.SpaceStation)
                {
                    SpawnRemotePlayer(member.Id, member.Name);
                }
            }
        }

        private void OnRemotePlayerJoined(Steamworks.SteamId steamId)
        {
            // Spawn remote player if they're in our scene
            var state = PlayerStateManager.Instance?.GetPlayerState(steamId);
            if (state != null && state.Scene == NetworkSceneId.SpaceStation)
            {
                var friend = new Steamworks.Friend(steamId);
                SpawnRemotePlayer(steamId, friend.Name);
            }
        }

        private void OnRemotePlayerLeft(Steamworks.SteamId steamId)
        {
            if (NetworkConnectionManager.Instance != null)
            {
                NetworkConnectionManager.Instance.DespawnRemotePlayer(steamId);
            }

            if (VoiceSessionManager.Instance != null)
            {
                VoiceSessionManager.Instance.UnregisterRemotePlayer(steamId);
            }
        }

        private void SpawnRemotePlayer(Steamworks.SteamId steamId, string displayName)
        {
            if (NetworkConnectionManager.Instance != null)
            {
                NetworkConnectionManager.Instance.SpawnRemotePlayerFor(steamId, displayName);
            }

            // Register with voice system
            if (VoiceSessionManager.Instance != null && NetworkConnectionManager.Instance != null)
            {
                // Get the spawned player instance
                var playerObj = FindRemotePlayerObject(steamId);
                if (playerObj != null)
                {
                    VoiceSessionManager.Instance.RegisterRemotePlayerAvatar(steamId, playerObj);
                }
            }
        }

        private GameObject FindRemotePlayerObject(Steamworks.SteamId steamId)
        {
            // Find player by name pattern
            return GameObject.Find($"RemotePlayer_{steamId}") ?? GameObject.Find($"RemotePlayer_{new Steamworks.Friend(steamId).Name}");
        }

        /// <summary>
        /// Example: Repair a component by index. Authority required (enforced by manager).
        /// </summary>
        public void RepairComponent(int componentIndex)
        {
            if (SatelliteStateManager.Instance != null)
            {
                SatelliteStateManager.Instance.SetComponentRepaired(componentIndex);
                Debug.Log($"[SpaceStation] Repaired component {componentIndex}");
            }
        }

        /// <summary>
        /// Example: Apply damage to the satellite.
        /// </summary>
        public void DamageSatellite(float damage)
        {
            if (SatelliteStateManager.Instance != null)
            {
                float currentHealth = SatelliteStateManager.Instance.GetHealth();
                SatelliteStateManager.Instance.SetHealth(currentHealth - Mathf.Abs(damage));
                Debug.Log($"[SpaceStation] Damaged satellite by {damage}");
            }
        }
    }
}
