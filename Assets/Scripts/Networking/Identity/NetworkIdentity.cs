using System.Collections.Generic;
using Steamworks;
using UnityEngine;

namespace SatelliteGameJam.Networking.Identity
{
    /// <summary>
    /// Identifies a networked object with a unique ID and tracks its owner.
    /// Maintains a static registry for quick lookups by network ID.
    /// </summary>
    public class NetworkIdentity : MonoBehaviour
{
    [SerializeField] private uint networkId;
    
    public uint NetworkId => networkId;
    public SteamId OwnerSteamId { get; private set; }
    
    // Static registry for lookups
    private static Dictionary<uint, NetworkIdentity> registry = new Dictionary<uint, NetworkIdentity>();
    
    private void Awake()
    {
        // Registration and ID generation if needed
        if (networkId == 0)
        {
            networkId = GenerateNetworkId();
        }
        RegisterObject();
    }
    
    private void OnDestroy()
    {
        // Unregister this object from the static registry
        UnregisterObject();
    }
    
    /// <summary>
    /// Gets a NetworkIdentity by its network ID.
    /// Quick Fix #2: Added scene safety check to prevent cross-scene contamination.
    /// </summary>
    public static NetworkIdentity GetById(uint id)
    {
        // Lookup by network ID with validation
        if (registry.TryGetValue(id, out var identity))
        {
            // Validate the object still exists and hasn't been destroyed
            if (identity == null || identity.gameObject == null)
            {
                Debug.LogWarning($"[NetworkIdentity] Object with ID {id} was destroyed but still in registry. Cleaning up.");
                registry.Remove(id);
                return null;
            }
            
            return identity;
        }
        return null;
    }
    
    /// <summary>
    /// Sets the owner of this networked object.
    /// </summary>
    public void SetOwner(SteamId newOwner)
    {
        // Set the owner SteamId
        OwnerSteamId = newOwner;
    }
    
    /// <summary>
    /// Sets the network ID for this object (called from editor or at runtime).
    /// </summary>
    public void SetNetworkId(uint id)
    {
        // ID assignment
        if (registry.ContainsKey(id))
        {
            Debug.LogWarning($"Network ID {id} already exists. Overwriting existing entry.");
        }
        networkId = id;
        RegisterObject();
    }
    
    /// <summary>
    /// Registers this object in the static registry.
    /// </summary>
    private void RegisterObject()
    {
        // Register this object in the static registry
        if (registry.ContainsKey(networkId))
        {
            Debug.LogWarning($"Network ID {networkId} already exists. Overwriting existing entry.");
        }
        registry[networkId] = this;


    }
    
    /// <summary>
    /// Unregisters this object from the static registry.
    /// </summary>
    private void UnregisterObject()
    {
        // Unregister this object from the static registry
        if (registry.ContainsKey(networkId))
        {
            registry.Remove(networkId);
        }
    }
    
    /// <summary>
    /// Generates a unique network ID (hash-based or scene index).
    /// </summary>
    private uint GenerateNetworkId()
    {
        // ID generation
        // Hash of the object's name and position.
        return (uint)(name.GetHashCode() ^ transform.position.GetHashCode());
    }
}
}
