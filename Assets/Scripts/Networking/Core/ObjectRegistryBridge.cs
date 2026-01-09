using UnityEngine;
using UnityEngine.SceneManagement;

namespace SatelliteGameJam.Networking.Core
{
    /// <summary>
    /// Bridges NetworkIdentity registry across scene transitions.
    /// Ensures registry stays consistent when scenes load/unload.
    /// Attach to a DontDestroyOnLoad object or let NetworkConnectionManager manage it.
    /// </summary>
    public class ObjectRegistryBridge : MonoBehaviour
{
    public static ObjectRegistryBridge Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        // Subscribe to scene events
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    private void OnDestroy()
    {
        // Unsubscribe from scene events
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
    }

    /// <summary>
    /// Called when a scene is loaded. NetworkIdentity objects will re-register in their Awake.
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"[ObjectRegistryBridge] Scene loaded: {scene.name}");
        // NetworkIdentity objects auto-register in Awake, so nothing extra needed here
    }

    /// <summary>
    /// Called when a scene is unloaded. NetworkIdentity objects will unregister in their OnDestroy.
    /// </summary>
    private void OnSceneUnloaded(Scene scene)
    {
        Debug.Log($"[ObjectRegistryBridge] Scene unloaded: {scene.name}");
        // NetworkIdentity objects auto-unregister in OnDestroy, so nothing extra needed here
    }

    /// <summary>
    /// Optional: Manually clear all entries (useful for cleanup or lobby exit).
    /// </summary>
    public void ClearRegistry()
    {
        // This would require exposing a Clear method in NetworkIdentity's registry
        Debug.Log("[ObjectRegistryBridge] Manual registry clear requested (not yet implemented)");
    }
}
}
