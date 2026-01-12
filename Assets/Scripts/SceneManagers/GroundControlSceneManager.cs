using SatelliteGameJam.Networking.State;
using SatelliteGameJam.Networking.Messages;
using UnityEngine;

namespace SatelliteGameJam.SceneManagers
{
    /// <summary>
    /// Scene bootstrap for Ground Control.
    /// Sets local player role/scene and listens to satellite state events for UI updates.
    /// </summary>
    public class GroundControlSceneManager : MonoBehaviour
    {
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
