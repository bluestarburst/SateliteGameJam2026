using System.Collections.Generic;
using SatelliteGameJam.Networking.State;
using SatelliteGameJam.Networking.Messages;
using SatelliteGameJam.Networking.Core;
using SatelliteGameJam.Networking.Voice;
using Steamworks;
using UnityEngine;

namespace SatelliteGameJam.SceneManagers
{
    /// <summary>
    /// Scene-specific networking manager for Ground Control.
    /// Manages remote player spawning, console interaction voice gating, and satellite state UI updates.
    /// Refs: DeveloperExperienceImprovements.md Part 2
    /// </summary>
    public class GroundControlSceneManager : MonoBehaviour
    {
        [Header("Console Interaction")]
        [SerializeField] private bool isLocalPlayerAtConsole = false;

        [Header("Debug")]
        [SerializeField] private bool logDebug = false;

        private NetworkingConfiguration config;

        private void Start()
        {
            config = NetworkingConfiguration.Instance;

            // Set local player role and scene
            if (PlayerStateManager.Instance != null)
            {
                PlayerStateManager.Instance.SetLocalPlayerRole(PlayerRole.GroundControl);
                PlayerStateManager.Instance.SetLocalPlayerScene(NetworkSceneId.GroundControl);

                if (logDebug || (config != null && config.verboseLogging))
                {
                    Debug.Log("[GroundControl] Set local player to Ground Control scene/role");
                }
            }

            // Set initial voice gating (not at console by default)
            UpdateVoiceGating();

            // Subscribe to satellite state changes for UI updates
            if (SatelliteStateManager.Instance != null)
            {
                SatelliteStateManager.Instance.OnHealthChanged += OnHealthChanged;
                SatelliteStateManager.Instance.OnComponentDamaged += OnComponentDamaged;
                SatelliteStateManager.Instance.OnComponentRepaired += OnComponentRepaired;
                SatelliteStateManager.Instance.OnConsoleStateChanged += OnConsoleStateChanged;
            }
        }

        private void OnDestroy()
        {
            if (PlayerStateManager.Instance != null)
            {
                // SceneSyncManager now owns remote spawn lifecycle.
            }

            if (SatelliteStateManager.Instance != null)
            {
                SatelliteStateManager.Instance.OnHealthChanged -= OnHealthChanged;
                SatelliteStateManager.Instance.OnComponentDamaged -= OnComponentDamaged;
                SatelliteStateManager.Instance.OnComponentRepaired -= OnComponentRepaired;
                SatelliteStateManager.Instance.OnConsoleStateChanged -= OnConsoleStateChanged;
            }
        }

        // SceneSyncManager is the single authority for remote spawn/despawn.

        /// <summary>
        /// Updates voice gating based on console interaction state.
        /// Refs: GameFlowArchitecture.md - Voice Chat section
        /// </summary>
        private void UpdateVoiceGating()
        {
            if (VoiceSessionManager.Instance != null)
            {
                VoiceSessionManager.Instance.SetLocalPlayerAtConsole(isLocalPlayerAtConsole);
                
                if (logDebug || (config != null && config.verboseLogging))
                {
                    Debug.Log($"[GroundControl] Voice gating updated - At console: {isLocalPlayerAtConsole}");
                }
            }
        }

        /// <summary>
        /// Call this when player starts interacting with the transmission console.
        /// Enables hearing Space Station players through the console.
        /// </summary>
        public void OnConsoleInteractionStarted()
        {
            isLocalPlayerAtConsole = true;
            UpdateVoiceGating();
            
            if (logDebug) Debug.Log("[GroundControl] Console interaction STARTED - can now hear Space players");
        }

        /// <summary>
        /// Call this when player exits the transmission console.
        /// Disables hearing Space Station players.
        /// </summary>
        public void OnConsoleInteractionEnded()
        {
            isLocalPlayerAtConsole = false;
            UpdateVoiceGating();
            
            if (logDebug) Debug.Log("[GroundControl] Console interaction ENDED - cannot hear Space players");
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
