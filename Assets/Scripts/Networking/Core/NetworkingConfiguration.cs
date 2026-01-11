using UnityEngine;
using Steamworks.Data;

namespace SatelliteGameJam.Networking.Core
{
    /// <summary>
    /// Centralized configuration for all networking settings.
    /// Use this scriptable object to adjust networking behavior from the Unity Inspector.
    /// Ref: DeveloperExperienceImprovements.md Part 1
    /// </summary>
    [CreateAssetMenu(fileName = "NetworkingConfiguration", menuName = "Networking/Configuration", order = 1)]
    public class NetworkingConfiguration : ScriptableObject
    {
        [Header("Connection Settings")]
        [Tooltip("Channels to poll for incoming packets (0=control, 1=high-freq, 2=voice, 3=interactions, 4=low-freq)")]
        public int[] channelsToPoll = new[] { 0, 1, 3, 4 }; // Exclude voice channel 2 (handled by VoiceChatP2P)
        
        [Tooltip("Timeout in seconds before considering a packet send failed")]
        [Range(1f, 30f)]
        public float sendTimeoutSeconds = 5f;
        
        [Tooltip("Maximum number of retry attempts for reliable packets")]
        [Range(0, 10)]
        public int maxRetryAttempts = 3;

        [Header("Player Spawning")]
        [Tooltip("Automatically spawn remote player prefabs when they connect")]
        public bool autoSpawnPlayers = true;
        
        [Tooltip("Prefab to instantiate for remote players")]
        public GameObject remotePlayerPrefab;
        
        [Tooltip("Clean up player models when transitioning scenes")]
        public bool cleanupPlayersOnSceneChange = true;

        [Header("Scene Management")]
        [Tooltip("Name of the lobby scene")]
        public string lobbySceneName = "Lobby";
        
        [Tooltip("Name of the ground control scene")]
        public string groundControlSceneName = "GroundControl";
        
        [Tooltip("Name of the space station scene")]
        public string spaceStationSceneName = "SpaceStation";
        
        [Tooltip("Timeout in seconds before forcing scene change if not all players acknowledge")]
        [Range(5f, 60f)]
        public float sceneChangeTimeoutSeconds = 10f;

        [Header("Voice Chat")]
        [Tooltip("Enable voice chat functionality")]
        public bool voiceChatEnabled = true;
        
        [Tooltip("Voice activation threshold (0.0 - 1.0)")]
        [Range(0f, 1f)]
        public float voiceActivationThreshold = 0.3f;
        
        [Tooltip("Maximum distance for proximity-based voice chat (0 = unlimited)")]
        [Range(0f, 100f)]
        public float proximityVoiceDistance = 20f;
        
        [Tooltip("Apply role-based voice gating rules")]
        public bool useRoleBasedVoiceGating = true;

        [Header("State Synchronization")]
        [Tooltip("Frequency of transform sync updates per second")]
        [Range(10, 60)]
        public int transformSyncRate = 30;
        
        [Tooltip("Frequency of physics sync updates per second")]
        [Range(10, 60)]
        public int physicsSyncRate = 20;
        
        [Tooltip("Use interpolation for remote player movement")]
        public bool useInterpolation = true;
        
        [Tooltip("Interpolation delay in milliseconds")]
        [Range(50, 500)]
        public int interpolationDelayMs = 100;

        [Header("Satellite Game Specific")]
        [Tooltip("Only send satellite health updates to Space Station players")]
        public bool filterSatelliteMessagesByScene = true;
        
        [Tooltip("Satellite health update frequency per second")]
        [Range(1, 30)]
        public int satelliteHealthUpdateRate = 5;

        [Header("Debugging")]
        [Tooltip("Log detailed networking debug information")]
        public bool verboseLogging = false;
        
        [Tooltip("Show network debug overlay in game (toggle with Tab key)")]
        public bool showNetworkDebugOverlay = true;
        
        [Tooltip("Track and display packet send/receive statistics")]
        public bool showPacketStatistics = true;

        [Header("Error Handling")]
        [Tooltip("Automatically attempt to reconnect on disconnection")]
        public bool autoReconnect = true;
        
        [Tooltip("Number of reconnection attempts before giving up")]
        [Range(0, 10)]
        public int maxReconnectAttempts = 3;
        
        [Tooltip("Delay between reconnection attempts in seconds")]
        [Range(1f, 10f)]
        public float reconnectDelaySeconds = 2f;

        // Helper methods for code access
        
        /// <summary>
        /// Gets the P2PSend mode for a given channel (reliable vs unreliable).
        /// </summary>
        public P2PSend GetSendModeForChannel(int channel)
        {
            // Channel 0 (control) and 4 (low-freq state) use reliable
            // Channel 1 (high-freq sync) uses unreliable
            // Channel 2 (voice) handled separately
            // Channel 3 (interactions) uses reliable
            switch (channel)
            {
                case 0: return P2PSend.Reliable;
                case 1: return P2PSend.UnreliableNoDelay;
                case 3: return P2PSend.Reliable;
                case 4: return P2PSend.Reliable;
                default: return P2PSend.Reliable;
            }
        }

        /// <summary>
        /// Gets the scene name for a given NetworkSceneId.
        /// </summary>
        public string GetSceneName(Messages.NetworkSceneId sceneId)
        {
            switch (sceneId)
            {
                case Messages.NetworkSceneId.Lobby: return lobbySceneName;
                case Messages.NetworkSceneId.GroundControl: return groundControlSceneName;
                case Messages.NetworkSceneId.SpaceStation: return spaceStationSceneName;
                default: return string.Empty;
            }
        }

        /// <summary>
        /// Validates the configuration and logs warnings for invalid settings.
        /// </summary>
        public void ValidateConfiguration()
        {
            if (autoSpawnPlayers && remotePlayerPrefab == null)
            {
                Debug.LogWarning("[NetworkingConfiguration] autoSpawnPlayers is enabled but remotePlayerPrefab is not assigned!");
            }

            if (string.IsNullOrEmpty(lobbySceneName))
            {
                Debug.LogWarning("[NetworkingConfiguration] lobbySceneName is not set!");
            }

            if (string.IsNullOrEmpty(groundControlSceneName))
            {
                Debug.LogWarning("[NetworkingConfiguration] groundControlSceneName is not set!");
            }

            if (string.IsNullOrEmpty(spaceStationSceneName))
            {
                Debug.LogWarning("[NetworkingConfiguration] spaceStationSceneName is not set!");
            }

            if (channelsToPoll == null || channelsToPoll.Length == 0)
            {
                Debug.LogError("[NetworkingConfiguration] channelsToPoll is empty! No packets will be received!");
            }
        }

        private void OnValidate()
        {
            // Called when values change in the Inspector
            ValidateConfiguration();
        }
    }
}
