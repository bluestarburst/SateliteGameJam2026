using UnityEngine;
using SatelliteGameJam.Networking.State;
using SatelliteGameJam.Networking.Voice;

namespace SatelliteGameJam.Networking.Core
{
    /// <summary>
    /// All-in-one network setup component for the SteamPack prefab.
    /// Automatically ensures all required managers exist and are configured.
    /// Add this to your SteamPack GameObject and configure everything in one place.
    /// </summary>
    [DefaultExecutionOrder(-100)] // Execute before other network components
    public class NetworkSetup : MonoBehaviour
    {
        [Header("General Settings")]
        [SerializeField] [Tooltip("Enable mock Steam networking for offline testing")]
        private bool useMockSteam = false;

        [Header("Player Prefab")]
        [SerializeField] [Tooltip("Prefab to spawn for remote players")]
        private GameObject remotePlayerPrefab;
        [SerializeField] [Tooltip("Automatically spawn remote players when they join")]
        private bool autoSpawnPlayers = true;

        [Header("Scene Names")]
        [SerializeField] private string lobbySceneName = "Lobby";
        [SerializeField] private string groundControlSceneName = "GroundControl";
        [SerializeField] private string spaceStationSceneName = "SpaceStation";

        [Header("Voice Settings")]
        [SerializeField] [Tooltip("Distance for space-to-space voice chat")]
        private float spaceProximityRadius = 20f;

        [Header("Debug")]
        [SerializeField] private bool enableNetworkDebugLogs = false;
        [SerializeField] private bool enableSceneSyncDebugLogs = false;
        [SerializeField] private bool enableVoiceDebugLogs = false;

        private void Awake()
        {
            Debug.Log("[NetworkSetup] Initializing network managers...");

            // Setup mock Steam if enabled
            if (useMockSteam)
            {
                SetupMockSteam();
            }

            // Ensure all required managers exist
            EnsureManager<NetworkConnectionManager>("NetworkConnectionManager", ConfigureConnectionManager);
            EnsureManager<PlayerStateManager>("PlayerStateManager", null);
            EnsureManager<SceneSyncManager>("SceneSyncManager", ConfigureSceneSync);
            EnsureManagerMonoBehaviour<VoiceSessionManager>("VoiceSessionManager", ConfigureVoiceSession);
            EnsureManager<SatelliteStateManager>("SatelliteStateManager", null);

            Debug.Log("[NetworkSetup] Network setup complete!");
        }

        private void SetupMockSteam()
        {
            var mockSteam = gameObject.GetComponent<MockSteamNetworking>();
            if (mockSteam == null)
            {
                mockSteam = gameObject.AddComponent<MockSteamNetworking>();
                Debug.Log("[NetworkSetup] Added MockSteamNetworking component");
            }
        }

        private void EnsureManager<T>(string managerName, System.Action<T> configureAction) where T : Component
        {
            var manager = FindFirstObjectByType<T>();
            if (manager == null)
            {
                // Create as ROOT GameObject for DontDestroyOnLoad to work
                var managerObj = new GameObject(managerName);
                manager = managerObj.AddComponent<T>();
                Debug.Log($"[NetworkSetup] Created {managerName}");
            }

            // Apply configuration
            configureAction?.Invoke(manager);
        }

        private void EnsureManagerMonoBehaviour<T>(string managerName, System.Action<T> configureAction) where T : MonoBehaviour
        {
            var manager = FindFirstObjectByType<T>();
            if (manager == null)
            {
                // Create as ROOT GameObject for DontDestroyOnLoad to work
                var managerObj = new GameObject(managerName);
                manager = managerObj.AddComponent<T>();
                Debug.Log($"[NetworkSetup] Created {managerName}");
            }

            // Apply configuration
            configureAction?.Invoke(manager);
        }

        private void ConfigureConnectionManager(NetworkConnectionManager manager)
        {
            // Use reflection to set private fields
            var type = typeof(NetworkConnectionManager);
            
            var playerPrefabField = type.GetField("playerPrefab", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var autoSpawnField = type.GetField("autoSpawnPlayer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (playerPrefabField != null && remotePlayerPrefab != null)
            {
                playerPrefabField.SetValue(manager, remotePlayerPrefab);
            }

            if (autoSpawnField != null)
            {
                autoSpawnField.SetValue(manager, autoSpawnPlayers);
            }
        }

        private void ConfigureSceneSync(SceneSyncManager manager)
        {
            var type = typeof(SceneSyncManager);
            
            var lobbyField = type.GetField("lobbySceneName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var groundField = type.GetField("groundControlSceneName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var spaceField = type.GetField("spaceStationSceneName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var debugField = type.GetField("logDebug", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            lobbyField?.SetValue(manager, lobbySceneName);
            groundField?.SetValue(manager, groundControlSceneName);
            spaceField?.SetValue(manager, spaceStationSceneName);
            debugField?.SetValue(manager, enableSceneSyncDebugLogs);
        }

        private void ConfigureVoiceSession(VoiceSessionManager manager)
        {
            var type = typeof(VoiceSessionManager);
            
            var proximityField = type.GetField("spaceProximityRadius", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var debugField = type.GetField("enableDebugLogs", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var lobbyField = type.GetField("LobbyScene", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            proximityField?.SetValue(manager, spaceProximityRadius);
            debugField?.SetValue(manager, enableVoiceDebugLogs);
            lobbyField?.SetValue(manager, lobbySceneName);
        }

        [ContextMenu("Validate Setup")]
        private void ValidateSetup()
        {
            Debug.Log("=== Network Setup Validation ===");
            Debug.Log($"Mock Steam: {(useMockSteam ? "ENABLED" : "Disabled")}");
            Debug.Log($"Remote Player Prefab: {(remotePlayerPrefab != null ? remotePlayerPrefab.name : "NOT SET")}");
            Debug.Log($"Auto Spawn Players: {autoSpawnPlayers}");
            Debug.Log($"Lobby Scene: {lobbySceneName}");
            Debug.Log($"Ground Control Scene: {groundControlSceneName}");
            Debug.Log($"Space Station Scene: {spaceStationSceneName}");
            Debug.Log($"Voice Proximity Radius: {spaceProximityRadius}m");
            Debug.Log("================================");
        }
    }
}