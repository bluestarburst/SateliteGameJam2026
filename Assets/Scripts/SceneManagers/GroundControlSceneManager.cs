using SatelliteGameJam.Networking.State;
using SatelliteGameJam.Networking.Messages;
using SatelliteGameJam.Networking.Voice;
using UnityEngine;
using SatelliteGameJam.Networking.Core;

namespace SatelliteGameJam.SceneManagers
{
    /// <summary>
    /// Scene bootstrap for Ground Control.
    /// Sets local player role/scene, listens to satellite state events, and spawns remote players.
    /// </summary>
    public class GroundControlSceneManager : MonoBehaviour
    {
        [Header("Console Interaction")]
        [SerializeField] private KeyCode consoleInteractKey = KeyCode.E;
        [SerializeField] private bool debugConsoleToggle = false;

        private bool isAtConsole = false;

        private void Start()
        {
            // Set player role and scene
            if (PlayerStateManager.Instance != null)
            {
                PlayerStateManager.Instance.SetLocalPlayerRole(PlayerRole.GroundControl);
                PlayerStateManager.Instance.SetLocalPlayerScene(NetworkSceneId.GroundControl);
            }

            // Subscribe to satellite state changes
            if (SatelliteStateManager.Instance != null)
            {
                SatelliteStateManager.Instance.OnHealthChanged += OnHealthChanged;
                SatelliteStateManager.Instance.OnComponentDamaged += OnComponentDamaged;
                SatelliteStateManager.Instance.OnComponentRepaired += OnComponentRepaired;
                SatelliteStateManager.Instance.OnConsoleStateChanged += OnConsoleStateChanged;
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

        private void Update()
        {
            // Debug toggle for console interaction
            if (debugConsoleToggle && Input.GetKeyDown(consoleInteractKey))
            {
                ToggleConsoleInteraction();
            }
        }

        private void OnDestroy()
        {
            if (SatelliteStateManager.Instance != null)
            {
                SatelliteStateManager.Instance.OnHealthChanged -= OnHealthChanged;
                SatelliteStateManager.Instance.OnComponentDamaged -= OnComponentDamaged;
                SatelliteStateManager.Instance.OnComponentRepaired -= OnComponentRepaired;
                SatelliteStateManager.Instance.OnConsoleStateChanged -= OnConsoleStateChanged;
            }

            if (PlayerStateManager.Instance != null)
            {
                PlayerStateManager.Instance.OnPlayerJoined -= OnRemotePlayerJoined;
                PlayerStateManager.Instance.OnPlayerLeft -= OnRemotePlayerLeft;
            }

            // Disable console interaction on scene exit
            if (VoiceSessionManager.Instance != null)
            {
                VoiceSessionManager.Instance.SetLocalPlayerAtConsole(false);
            }
        }

        /// <summary>
        /// Call this from your console interaction script when player starts/stops using console.
        /// </summary>
        public void SetConsoleInteraction(bool interacting)
        {
            isAtConsole = interacting;
            
            if (VoiceSessionManager.Instance != null)
            {
                VoiceSessionManager.Instance.SetLocalPlayerAtConsole(interacting);
            }

            Debug.Log($"[GroundControl] Console interaction: {interacting}");
        }

        private void ToggleConsoleInteraction()
        {
            SetConsoleInteraction(!isAtConsole);
        }

        private void SpawnExistingRemotePlayers()
        {
            if (SteamManager.Instance?.currentLobby.Id.Value == 0) return;

            foreach (var member in SteamManager.Instance.currentLobby.Members)
            {
                if (member.Id == SteamManager.Instance.PlayerSteamId) continue;

                var state = PlayerStateManager.Instance.GetPlayerState(member.Id);
                // Only spawn if they're in the same scene
                if (state.Scene == NetworkSceneId.GroundControl)
                {
                    SpawnRemotePlayer(member.Id, member.Name);
                }
            }
        }

        private void OnRemotePlayerJoined(Steamworks.SteamId steamId)
        {
            // Spawn remote player if they're in our scene
            var state = PlayerStateManager.Instance?.GetPlayerState(steamId);
            if (state != null && state.Scene == NetworkSceneId.GroundControl)
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

        private void OnHealthChanged(float health)
        {
            // TODO: Hook up to UI health bar
            Debug.Log($"[GroundControl] Satellite health: {health}%");
        }

        private void OnComponentDamaged(uint componentIndex)
        {
            // TODO: Show damage indicator on UI
            Debug.Log($"[GroundControl] Component {componentIndex} damaged");
        }

        private void OnComponentRepaired(uint componentIndex)
        {
            // TODO: Clear damage indicator on UI
            Debug.Log($"[GroundControl] Component {componentIndex} repaired");
        }

        private void OnConsoleStateChanged(uint consoleId, ConsoleStateData state)
        {
            // TODO: Update console screen/display
            Debug.Log($"[GroundControl] Console {consoleId} state changed: {state.StateByte}");
        }
    }
}
