using UnityEngine;
using Steamworks;

namespace SatelliteGameJam.Networking.Core
{
    /// <summary>
    /// Centralized configuration for all networking settings.
    /// Use this scriptable object to adjust networking behavior from the Unity Inspector.
    /// Ref: DeveloperExperienceImprovements.md Part 1 & Part 6
    /// </summary>
    [CreateAssetMenu(fileName = "NetworkingConfiguration", menuName = "Networking/Configuration", order = 1)]
    public class NetworkingConfiguration : ScriptableObject
    {
        [Header("Connection Settings")]
        [Tooltip("Steam App ID for initialization")]
        public uint steamAppId = 480;

        [Header("Player Spawning")]
        [Tooltip("Automatically spawn remote player prefabs when they connect")]
        public bool autoSpawnPlayers = true;
        
        [Tooltip("Prefab to instantiate for remote players")]
        public GameObject remotePlayerPrefab;
        
        [Header("Scene Management")]
        [Tooltip("Optional global game flow definition. When assigned, scene routing should resolve through this asset first.")]
        public GameFlowDefinition gameFlowDefinition;

        [Tooltip("Timeout in seconds before forcing scene change if not all players acknowledge")]
        [Range(5f, 60f)]
        public float sceneChangeTimeoutSeconds = 10f;

        [Header("Voice Chat")]
        [Tooltip("Enable voice chat functionality")]
        public bool voiceChatEnabled = true;
        
        [Tooltip("Maximum distance for proximity-based voice chat (0 = unlimited)")]
        [Range(0f, 100f)]
        public float proximityVoiceDistance = 20f;
        
        [Tooltip("Apply role-based voice gating rules")]
        public bool useRoleBasedVoiceGating = true;

        [Header("Debugging")]
        [Tooltip("Log detailed networking debug information")]
        public bool verboseLogging = false;
        
        [Tooltip("Show network debug overlay in game (toggle with Tab key)")]
        public bool showNetworkDebugOverlay = true;
        
        [Tooltip("Track and display packet send/receive statistics")]
        public bool showPacketStatistics = true;

        [Header("Extensibility")]
        [Tooltip("Use the extensible INetworkMessage system (allows custom message types without code changes)")]
        public bool useExtensibleMessageSystem = false;

        // Singleton instance
        private static NetworkingConfiguration instance;
        public static NetworkingConfiguration Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = Resources.Load<NetworkingConfiguration>("NetworkingConfig");
                    if (instance == null)
                    {
                        Debug.LogWarning("[NetworkingConfiguration] NetworkingConfig not found in Resources/NetworkingConfig.asset. " +
                            "Create one via: Right-click → Create → Networking → Configuration");
                    }
                }
                return instance;
            }
        }

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
            if (gameFlowDefinition != null)
            {
                string mappedScene = gameFlowDefinition.ResolveSceneName(sceneId);
                if (!string.IsNullOrWhiteSpace(mappedScene))
                {
                    return mappedScene;
                }
            }

            return string.Empty;
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

            gameFlowDefinition?.Validate();

        }

        private void OnValidate()
        {
            // Called when values change in the Inspector
            ValidateConfiguration();
        }
    }
}
