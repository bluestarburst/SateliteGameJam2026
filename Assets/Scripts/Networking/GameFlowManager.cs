using System;
using Steamworks;
using UnityEngine;
using SatelliteGameJam.Networking.Core;
using SatelliteGameJam.Networking.State;
using SatelliteGameJam.Networking.Voice;
using SatelliteGameJam.Networking.Messages;
using SatelliteGameJam.Networking.Identity;
using SatelliteGameJam.Networking.Interactions;

namespace SatelliteGameJam.Networking
{
    /// <summary>
    /// High-level API for game flow that abstracts networking implementation details.
    /// Game code calls these methods; internally uses networking managers.
    /// This decouples gameplay from networking layer.
    /// Refs: DeveloperExperienceImprovements.md Part 4
    /// </summary>
    public class GameFlowManager : MonoBehaviour
    {
        public static GameFlowManager Instance { get; private set; }

        // Game flow events
        public event Action<NetworkSceneId> OnSceneLoading;
        public event Action<NetworkSceneId> OnSceneLoaded;
        public event Action<SteamId> OnRemotePlayerJoined;
        public event Action<SteamId> OnRemotePlayerLeft;
        public event Action OnGameStarted;
        public event Action OnGameEnded;

        [Header("Debug")]
        [SerializeField] private bool logDebug = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (logDebug) Debug.Log("[GameFlowManager] Initialized");
        }

        private void Start()
        {
            // Subscribe to player state events and forward them
            if (PlayerStateManager.Instance != null)
            {
                PlayerStateManager.Instance.OnPlayerJoined += (steamId) => OnRemotePlayerJoined?.Invoke(steamId);
                PlayerStateManager.Instance.OnPlayerLeft += (steamId) => OnRemotePlayerLeft?.Invoke(steamId);
            }
        }

        // ===== LOBBY PHASE =====

        /// <summary>
        /// Marks local player as ready to start game (hosted by lobby owner).
        /// Call this from UI Ready button.
        /// </summary>
        public void MarkPlayerReady()
        {
            if (PlayerStateManager.Instance != null)
            {
                PlayerStateManager.Instance.SetLocalPlayerReady();
                
                if (logDebug) Debug.Log("[GameFlowManager] Local player marked as ready");
            }
            else
            {
                Debug.LogError("[GameFlowManager] Cannot mark ready - PlayerStateManager not available");
            }
        }

        /// <summary>
        /// Sets local player's role for the upcoming game.
        /// Call this from UI role selection dropdown.
        /// </summary>
        public void SelectRole(PlayerRole role)
        {
            if (PlayerStateManager.Instance != null)
            {
                PlayerStateManager.Instance.SetLocalPlayerRole(role);
                
                if (logDebug) Debug.Log($"[GameFlowManager] Local player selected role: {role}");
            }
            else
            {
                Debug.LogError("[GameFlowManager] Cannot select role - PlayerStateManager not available");
            }
        }

        /// <summary>
        /// Gets the current role of the local player.
        /// </summary>
        public PlayerRole GetLocalPlayerRole()
        {
            if (PlayerStateManager.Instance != null && SteamManager.Instance != null)
            {
                var state = PlayerStateManager.Instance.GetPlayerState(SteamManager.Instance.PlayerSteamId);
                return state.Role;
            }
            return PlayerRole.None;
        }

        /// <summary>
        /// Gets the current scene of the local player.
        /// </summary>
        public NetworkSceneId GetLocalPlayerScene()
        {
            if (PlayerStateManager.Instance != null && SteamManager.Instance != null)
            {
                var state = PlayerStateManager.Instance.GetPlayerState(SteamManager.Instance.PlayerSteamId);
                return state.Scene;
            }
            return NetworkSceneId.None;
        }

        /// <summary>
        /// Checks if the local player is ready.
        /// </summary>
        public bool IsLocalPlayerReady()
        {
            if (PlayerStateManager.Instance != null && SteamManager.Instance != null)
            {
                var state = PlayerStateManager.Instance.GetPlayerState(SteamManager.Instance.PlayerSteamId);
                return state.IsReady;
            }
            return false;
        }

        /// <summary>
        /// Checks if the local player is the lobby host.
        /// </summary>
        public bool IsLocalPlayerHost()
        {
            if (SteamManager.Instance == null) return false;
            return SteamManager.Instance.currentLobby.Owner.Id == SteamManager.Instance.PlayerSteamId;
        }

        // ===== GAME START/END =====

        /// <summary>
        /// Starts the game (must be called by lobby owner).
        /// Triggers role-based scene transitions for all players.
        /// Call this from UI Start Game button.
        /// </summary>
        public void StartGame()
        {
            if (!IsLocalPlayerHost())
            {
                Debug.LogWarning("[GameFlowManager] Only the host can start the game");
                return;
            }

            if (SceneSyncManager.Instance != null)
            {
                SceneSyncManager.Instance.RequestStartGame();
                OnGameStarted?.Invoke();
                
                if (logDebug) Debug.Log("[GameFlowManager] Game started");
            }
            else
            {
                Debug.LogError("[GameFlowManager] Cannot start game - SceneSyncManager not available");
            }
        }

        /// <summary>
        /// Ends the game and returns all players to lobby.
        /// Must be called by lobby owner.
        /// Call this when game session completes.
        /// </summary>
        public void EndGame()
        {
            if (!IsLocalPlayerHost())
            {
                Debug.LogWarning("[GameFlowManager] Only the host can end the game");
                return;
            }

            if (SceneSyncManager.Instance != null)
            {
                SceneSyncManager.Instance.RequestEndGame();
                OnGameEnded?.Invoke();
                
                if (logDebug) Debug.Log("[GameFlowManager] Game ended");
            }
            else
            {
                Debug.LogError("[GameFlowManager] Cannot end game - SceneSyncManager not available");
            }
        }

        // ===== IN-GAME VOICE CONTROL =====

        /// <summary>
        /// Called when local player starts/stops interacting with communication console.
        /// Enables hearing of remote players on other side of console.
        /// Call this from console interaction scripts.
        /// </summary>
        public void SetConsoleInteraction(bool isInteracting)
        {
            if (VoiceSessionManager.Instance != null)
            {
                VoiceSessionManager.Instance.SetLocalPlayerAtConsole(isInteracting);
                
                if (logDebug) Debug.Log($"[GameFlowManager] Console interaction: {isInteracting}");
            }
            else
            {
                Debug.LogWarning("[GameFlowManager] Cannot set console interaction - VoiceSessionManager not available");
            }
        }

        /// <summary>
        /// Called when local player position changes (for proximity-based voice).
        /// Can be expanded for proximity-based voice gating.
        /// </summary>
        public void UpdateLocalPlayerPosition(Vector3 newPosition)
        {
            // Future: Implement proximity-based voice gating
            // For now, voice is controlled by console interaction and role-based rules
        }

        // ===== SATELLITE STATE =====

        /// <summary>
        /// Reports damage to satellite component (authority only).
        /// Call this from damage systems.
        /// </summary>
        public void ReportSatelliteDamage(int componentIndex)
        {
            if (SatelliteStateManager.Instance != null)
            {
                SatelliteStateManager.Instance.SetComponentDamaged(componentIndex);
                
                if (logDebug) Debug.Log($"[GameFlowManager] Satellite component {componentIndex} damaged");
            }
            else
            {
                Debug.LogWarning("[GameFlowManager] Cannot report damage - SatelliteStateManager not available");
            }
        }

        /// <summary>
        /// Reports repair to satellite component (authority only).
        /// Call this from repair systems.
        /// </summary>
        public void ReportSatelliteRepair(int componentIndex)
        {
            if (SatelliteStateManager.Instance != null)
            {
                SatelliteStateManager.Instance.SetComponentRepaired(componentIndex);
                
                if (logDebug) Debug.Log($"[GameFlowManager] Satellite component {componentIndex} repaired");
            }
            else
            {
                Debug.LogWarning("[GameFlowManager] Cannot report repair - SatelliteStateManager not available");
            }
        }

        /// <summary>
        /// Subscribe to satellite health changes.
        /// Use this to update UI health bars.
        /// </summary>
        public event Action<float> OnSatelliteHealthChanged
        {
            add
            {
                if (SatelliteStateManager.Instance != null)
                    SatelliteStateManager.Instance.OnHealthChanged += value;
            }
            remove
            {
                if (SatelliteStateManager.Instance != null)
                    SatelliteStateManager.Instance.OnHealthChanged -= value;
            }
        }

        /// <summary>
        /// Subscribe to satellite component damage events.
        /// Use this to trigger visual effects or audio.
        /// </summary>
        public event Action<int> OnSatelliteComponentDamaged
        {
            add
            {
                if (SatelliteStateManager.Instance != null)
                    SatelliteStateManager.Instance.OnComponentDamaged += value;
            }
            remove
            {
                if (SatelliteStateManager.Instance != null)
                    SatelliteStateManager.Instance.OnComponentDamaged -= value;
            }
        }

        /// <summary>
        /// Subscribe to satellite component repair events.
        /// Use this to trigger visual effects or audio.
        /// </summary>
        public event Action<int> OnSatelliteComponentRepaired
        {
            add
            {
                if (SatelliteStateManager.Instance != null)
                    SatelliteStateManager.Instance.OnComponentRepaired += value;
            }
            remove
            {
                if (SatelliteStateManager.Instance != null)
                    SatelliteStateManager.Instance.OnComponentRepaired -= value;
            }
        }

        /// <summary>
        /// Get current satellite health (0-100).
        /// </summary>
        public float GetSatelliteHealth()
        {
            return SatelliteStateManager.Instance?.GetHealth() ?? 100f;
        }

        /// <summary>
        /// Check if a specific satellite component is damaged.
        /// </summary>
        public bool IsSatelliteComponentDamaged(int componentIndex)
        {
            if (SatelliteStateManager.Instance != null)
            {
                return SatelliteStateManager.Instance.IsComponentDamaged(componentIndex);
            }
            return false;
        }

        // ===== OBJECT INTERACTION =====

        /// <summary>
        /// Request to pick up a networked object.
        /// Call this from player interaction scripts.
        /// </summary>
        public void PickupNetworkedObject(uint networkId, SteamId pickerId)
        {
            var identity = NetworkIdentity.GetById(networkId);
            if (identity != null)
            {
                var interactionState = identity.GetComponent<NetworkInteractionState>();
                if (interactionState != null)
                {
                    interactionState.TryPickup(pickerId);
                    
                    if (logDebug) Debug.Log($"[GameFlowManager] Player {pickerId} picked up object {networkId}");
                }
                else
                {
                    Debug.LogWarning($"[GameFlowManager] Object {networkId} has no NetworkInteractionState component");
                }
            }
            else
            {
                Debug.LogWarning($"[GameFlowManager] Network object {networkId} not found");
            }
        }

        /// <summary>
        /// Request to drop a networked object.
        /// Call this from player interaction scripts.
        /// </summary>
        public void DropNetworkedObject(uint networkId, Vector3 position, Vector3 velocity)
        {
            var identity = NetworkIdentity.GetById(networkId);
            if (identity != null)
            {
                var interactionState = identity.GetComponent<NetworkInteractionState>();
                if (interactionState != null)
                {
                    interactionState.Drop(position, velocity);
                    
                    if (logDebug) Debug.Log($"[GameFlowManager] Dropped object {networkId} at {position}");
                }
                else
                {
                    Debug.LogWarning($"[GameFlowManager] Object {networkId} has no NetworkInteractionState component");
                }
            }
            else
            {
                Debug.LogWarning($"[GameFlowManager] Network object {networkId} not found");
            }
        }

        /// <summary>
        /// Check if a networked object is currently being held by a player.
        /// </summary>
        public bool IsObjectHeld(uint networkId)
        {
            var identity = NetworkIdentity.GetById(networkId);
            if (identity != null)
            {
                var interactionState = identity.GetComponent<NetworkInteractionState>();
                if (interactionState != null)
                {
                    return interactionState.IsHeld;
                }
            }
            return false;
        }

        /// <summary>
        /// Get the SteamId of the player holding a networked object (if any).
        /// Returns 0 if object is not held.
        /// </summary>
        public SteamId GetObjectHolder(uint networkId)
        {
            var identity = NetworkIdentity.GetById(networkId);
            if (identity != null)
            {
                var interactionState = identity.GetComponent<NetworkInteractionState>();
                if (interactionState != null && interactionState.IsHeld)
                {
                    return interactionState.HolderSteamId;
                }
            }
            return 0;
        }

        // ===== UTILITY METHODS =====

        /// <summary>
        /// Get the display name of a player by SteamId.
        /// </summary>
        public string GetPlayerName(SteamId steamId)
        {
            if (SteamManager.Instance != null)
            {
                var member = SteamManager.Instance.currentLobby.GetMember(steamId);
                if (member.HasValue)
                {
                    return member.Value.Name;
                }
            }
            return $"Player {steamId.Value}";
        }

        /// <summary>
        /// Get the count of connected players in the lobby.
        /// </summary>
        public int GetPlayerCount()
        {
            return SteamManager.Instance?.currentLobby.MemberCount ?? 0;
        }

        /// <summary>
        /// Get all connected players' SteamIds.
        /// </summary>
        public SteamId[] GetAllPlayerIds()
        {
            if (SteamManager.Instance == null) return new SteamId[0];

            var players = new System.Collections.Generic.List<SteamId>();
            foreach (var member in SteamManager.Instance.currentLobby.Members)
            {
                players.Add(member.Id);
            }
            return players.ToArray();
        }
    }
}
