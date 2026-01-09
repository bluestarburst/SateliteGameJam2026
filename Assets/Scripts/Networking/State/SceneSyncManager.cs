using System;
using System.Collections.Generic;
using System.Linq;
using Steamworks;
using UnityEngine;
using UnityEngine.SceneManagement;
using SatelliteGameJam.Networking.Messages;
using SatelliteGameJam.Networking.Core;

namespace SatelliteGameJam.Networking.State
{
    /// <summary>
    /// Coordinates scene transitions across the network.
    /// Handles scene change requests/acknowledgments and load barriers to ensure all players sync up.
    /// Works with PlayerStateManager to track who is where.
    /// </summary>
    public class SceneSyncManager : MonoBehaviour
{
    public static SceneSyncManager Instance { get; private set; }

    [Header("Scene Sync Settings")]
    [SerializeField] private float sceneChangeTimeout = 10f; // Seconds to wait for all acks

    // Scene change coordination
    private NetworkSceneId pendingSceneChange = NetworkSceneId.None;
    private HashSet<SteamId> receivedAcknowledgments = new();
    private float sceneChangeRequestTime;
    private bool awaitingSceneChange = false;

    // Events
    public event Action<NetworkSceneId> OnSceneChangeRequested;
    public event Action<NetworkSceneId> OnAllPlayersReady; // All players acked the scene change
    public event Action<SteamId, NetworkSceneId> OnPlayerSceneChangeAck;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Register message handlers
        if (NetworkConnectionManager.Instance != null)
        {
            NetworkConnectionManager.Instance.RegisterHandler(NetworkMessageType.SceneChangeRequest, OnReceiveSceneChangeRequest);
            NetworkConnectionManager.Instance.RegisterHandler(NetworkMessageType.SceneChangeAcknowledge, OnReceiveSceneChangeAck);
        }

        // Subscribe to Unity scene events
        SceneManager.sceneLoaded += OnUnitySceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnUnitySceneLoaded;
    }

    private void Update()
    {
        // Timeout check for scene changes
        if (awaitingSceneChange && Time.time - sceneChangeRequestTime > sceneChangeTimeout)
        {
            Debug.LogWarning($"[SceneSyncManager] Scene change timeout - not all players acknowledged {pendingSceneChange}");
            // Proceed anyway or handle timeout logic
            CompleteSceneChange();
        }
    }

    /// <summary>
    /// Requests a scene change for all players. Typically called by the host/lobby leader.
    /// </summary>
    public void RequestSceneChange(NetworkSceneId newScene)
    {
        if (awaitingSceneChange)
        {
            Debug.LogWarning($"[SceneSyncManager] Already awaiting scene change to {pendingSceneChange}");
            return;
        }

        Debug.Log($"[SceneSyncManager] Requesting scene change to {newScene}");
        
        pendingSceneChange = newScene;
        awaitingSceneChange = true;
        sceneChangeRequestTime = Time.time;
        receivedAcknowledgments.Clear();

        // Broadcast scene change request
        BroadcastSceneChangeRequest(newScene);
        
        OnSceneChangeRequested?.Invoke(newScene);
    }

    /// <summary>
    /// Acknowledges a scene change request. Called after local scene load completes.
    /// </summary>
    public void AcknowledgeSceneChange(NetworkSceneId sceneId)
    {
        SteamId localId = SteamManager.Instance.PlayerSteamId;
        
        Debug.Log($"[SceneSyncManager] Acknowledging scene change to {sceneId}");
        
        // Broadcast acknowledgment
        BroadcastSceneChangeAck(localId, sceneId);
        
        // Update player state
        if (PlayerStateManager.Instance != null)
        {
            PlayerStateManager.Instance.SetLocalPlayerScene(sceneId);
        }
    }

    /// <summary>
    /// Checks if all expected players have acknowledged the scene change.
    /// </summary>
    private void CheckAllPlayersReady()
    {
        if (!awaitingSceneChange) return;

        // Get current lobby members
        if (SteamManager.Instance?.currentLobby == null) return;
        
        var expectedPlayers = SteamManager.Instance.currentLobby.Members
            .Select(m => m.Id)
            .Where(id => id != SteamManager.Instance.PlayerSteamId) // Exclude self
            .ToHashSet();

        // Check if all have acked
        if (expectedPlayers.All(id => receivedAcknowledgments.Contains(id)))
        {
            Debug.Log($"[SceneSyncManager] All players ready for scene {pendingSceneChange}");
            CompleteSceneChange();
        }
    }

    private void CompleteSceneChange()
    {
        awaitingSceneChange = false;
        OnAllPlayersReady?.Invoke(pendingSceneChange);
        pendingSceneChange = NetworkSceneId.None;
        receivedAcknowledgments.Clear();
    }

    /// <summary>
    /// Called when Unity finishes loading a scene locally.
    /// </summary>
    private void OnUnitySceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"[SceneSyncManager] Unity scene loaded: {scene.name}");
        
        // Map Unity scene name to NetworkSceneId (you'll need to customize this)
        NetworkSceneId sceneId = MapUnitySceneToNetworkScene(scene.name);
        
        // Auto-acknowledge if we were waiting for this scene
        if (sceneId != NetworkSceneId.None && sceneId == pendingSceneChange)
        {
            AcknowledgeSceneChange(sceneId);
        }
    }

    /// <summary>
    /// Maps Unity scene names to NetworkSceneId enum values.
    /// Customize this based on your actual scene names.
    /// </summary>
    private NetworkSceneId MapUnitySceneToNetworkScene(string sceneName)
    {
        return sceneName.ToLower() switch
        {
            "lobby" => NetworkSceneId.Lobby,
            "groundcontrol" => NetworkSceneId.GroundControl,
            "spacestation" => NetworkSceneId.SpaceStation,
            _ => NetworkSceneId.None
        };
    }

    // ===== Message Sending =====

    private void BroadcastSceneChangeRequest(NetworkSceneId sceneId)
    {
        byte[] packet = new byte[11];
        packet[0] = (byte)NetworkMessageType.SceneChangeRequest;
        int offset = 1;
        NetworkSerialization.WriteULong(packet, ref offset, SteamManager.Instance.PlayerSteamId);
        packet[offset++] = (byte)((ushort)sceneId >> 8);
        packet[offset++] = (byte)((ushort)sceneId & 0xFF);
        NetworkConnectionManager.Instance.SendToAll(packet, 0, P2PSend.Reliable);
    }

    private void BroadcastSceneChangeAck(SteamId steamId, NetworkSceneId sceneId)
    {
        byte[] packet = new byte[11];
        packet[0] = (byte)NetworkMessageType.SceneChangeAcknowledge;
        int offset = 1;
        NetworkSerialization.WriteULong(packet, ref offset, steamId);
        packet[offset++] = (byte)((ushort)sceneId >> 8);
        packet[offset++] = (byte)((ushort)sceneId & 0xFF);
        NetworkConnectionManager.Instance.SendToAll(packet, 0, P2PSend.Reliable);
    }

    // ===== Message Receiving =====

    private void OnReceiveSceneChangeRequest(SteamId sender, byte[] data)
    {
        if (data.Length < 11) return;
        
        int offset = 1;
        SteamId requesterId = NetworkSerialization.ReadULong(data, ref offset);
        ushort sceneIdValue = (ushort)((data[offset++] << 8) | data[offset++]);
        NetworkSceneId sceneId = (NetworkSceneId)sceneIdValue;
        
        Debug.Log($"[SceneSyncManager] Received scene change request to {sceneId} from {requesterId}");
        
        pendingSceneChange = sceneId;
        awaitingSceneChange = true;
        sceneChangeRequestTime = Time.time;
        receivedAcknowledgments.Clear();
        
        OnSceneChangeRequested?.Invoke(sceneId);
        
        // TODO: Trigger local scene load here or let game logic handle it via the event
    }

    private void OnReceiveSceneChangeAck(SteamId sender, byte[] data)
    {
        if (data.Length < 11) return;
        
        int offset = 1;
        SteamId playerId = NetworkSerialization.ReadULong(data, ref offset);
        ushort sceneIdValue = (ushort)((data[offset++] << 8) | data[offset++]);
        NetworkSceneId sceneId = (NetworkSceneId)sceneIdValue;
        
        Debug.Log($"[SceneSyncManager] Received ack from {playerId} for scene {sceneId}");
        
        receivedAcknowledgments.Add(playerId);
        OnPlayerSceneChangeAck?.Invoke(playerId, sceneId);
        
        CheckAllPlayersReady();
    }
}
}
