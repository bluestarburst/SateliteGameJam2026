using System.Collections.Generic;
using Steamworks;
using UnityEngine;
using SatelliteGameJam.Networking.Messages;
using SatelliteGameJam.Networking.State;
using SatelliteGameJam.Networking.Core;
using Steamworks.Data;

namespace SatelliteGameJam.Networking.Voice
{
    /// <summary>
    /// Manages voice routing and gating based on player roles, scenes, and proximity.
    /// Attaches VoiceRemotePlayer components to avatars and controls AudioSource enable/volume.
    /// Implements role-specific rules:
    /// - Lobby: All players hear each other
    /// - Ground Control: Only hear space when at console; always hear other ground players
    /// - Space Station: Always hear ground; hear other space players within radius
    /// </summary>
    public class VoiceSessionManager : MonoBehaviour
{
    public static VoiceSessionManager Instance { get; private set; }

    [Header("Voice Rules")]
    [SerializeField] private float spaceProximityRadius = 20f; // Distance for space-to-space voice
    [SerializeField] private bool enableDebugLogs = false;
    [SerializeField] private string LobbyScene = "Lobby"; // Toggle for testing

    // Console interaction tracking
    private readonly HashSet<SteamId> playersAtConsole = new();

    // Remote player tracking (avatar GameObjects)
    private readonly Dictionary<SteamId, GameObject> remotePlayerAvatars = new();
    private readonly Dictionary<SteamId, VoiceRemotePlayer> voiceRemotePlayers = new();

    // Local player state
    private bool isLocalPlayerAtConsole = false;

    /// <summary>
    /// Public accessor for console state (used by VoiceChatP2P for send gating).
    /// </summary>
    public bool IsLocalPlayerAtConsole => isLocalPlayerAtConsole;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Register handler for console interaction broadcasts
        if (NetworkConnectionManager.Instance != null)
        {
            NetworkConnectionManager.Instance.RegisterHandler(
                NetworkMessageType.ConsoleInteraction, OnReceiveConsoleInteraction);
        }

        // Subscribe to player state events
        if (PlayerStateManager.Instance != null)
        {
            PlayerStateManager.Instance.OnPlayerJoined += OnPlayerJoined;
            PlayerStateManager.Instance.OnPlayerLeft += OnPlayerLeft;
            PlayerStateManager.Instance.OnPlayerSceneChanged += OnPlayerSceneChanged;
        }
    }

    private void Update()
    {
        // Apply voice gating rules every frame based on current state
        ApplyVoiceGating();
    }

    // ===== Public API =====

    /// <summary>
    /// Registers a remote player avatar for voice playback.
    /// VoiceRemotePlayer will be attached to this GameObject for positional audio.
    /// </summary>
    public void RegisterRemotePlayerAvatar(SteamId steamId, GameObject avatar)
    {
        if (avatar == null)
        {
            Debug.LogWarning($"[VoiceSessionManager] Tried to register null avatar for {steamId}");
            return;
        }

        remotePlayerAvatars[steamId] = avatar;

        // Attach VoiceRemotePlayer if not already present
        if (!voiceRemotePlayers.ContainsKey(steamId))
        {
            var voicePlayer = avatar.GetComponent<VoiceRemotePlayer>();
            if (voicePlayer == null)
            {
                voicePlayer = avatar.AddComponent<VoiceRemotePlayer>();
                voicePlayer.Initialize(steamId);
            }
            voiceRemotePlayers[steamId] = voicePlayer;
        }

        if (enableDebugLogs)
            Debug.Log($"[VoiceSessionManager] Registered avatar for {steamId}");
    }

    /// <summary>
    /// Unregisters and cleans up a remote player's voice components.
    /// </summary>
    public void UnregisterRemotePlayer(SteamId steamId)
    {
        if (voiceRemotePlayers.TryGetValue(steamId, out var voicePlayer))
        {
            if (voicePlayer != null)
                Destroy(voicePlayer);
            voiceRemotePlayers.Remove(steamId);
        }

        remotePlayerAvatars.Remove(steamId);

        if (enableDebugLogs)
            Debug.Log($"[VoiceSessionManager] Unregistered player {steamId}");
    }

    /// <summary>
    /// Marks the local player as interacting with a console (for ground control voice gating).
    /// Broadcasts this state to all peers.
    /// </summary>
    public void SetLocalPlayerAtConsole(bool atConsole)
    {
        isLocalPlayerAtConsole = atConsole;
        
        if (enableDebugLogs)
            Debug.Log($"[VoiceSessionManager] Local player at console: {atConsole}");
        
        // Broadcast console interaction state to all peers
        BroadcastConsoleInteraction(atConsole);
    }

    /// <summary>
    /// Broadcasts the local player's console interaction state to all peers.
    /// </summary>
    private void BroadcastConsoleInteraction(bool atConsole)
    {
        if (NetworkConnectionManager.Instance == null) return;

        byte[] packet = new byte[10]; // 1 (type) + 8 (steamId) + 1 (atConsole)
        int offset = 0;

        packet[offset++] = (byte)NetworkMessageType.ConsoleInteraction;
        NetworkSerialization.WriteULong(packet, ref offset, SteamManager.Instance.PlayerSteamId);
        packet[offset++] = (byte)(atConsole ? 1 : 0);

        // Send to all peers reliably
        NetworkConnectionManager.Instance.SendToAll(packet, 0, P2PSend.Reliable);

        if (enableDebugLogs)
            Debug.Log($"[VoiceSessionManager] Broadcast console interaction: {atConsole}");
    }

    /// <summary>
    /// Handles received console interaction state from remote players.
    /// </summary>
    private void OnReceiveConsoleInteraction(SteamId sender, byte[] data)
    {
        if (data == null || data.Length < 10) return;

        int offset = 1; // Skip message type
        SteamId playerSteamId = NetworkSerialization.ReadULong(data, ref offset);
        bool atConsole = data[offset] == 1;

        SetRemotePlayerAtConsole(playerSteamId, atConsole);

        if (enableDebugLogs)
            Debug.Log($"[VoiceSessionManager] Received console interaction from {playerSteamId}: {atConsole}");
    }

    /// <summary>
    /// Marks a remote player as interacting with a console (optional for future use).
    /// </summary>
    public void SetRemotePlayerAtConsole(SteamId steamId, bool atConsole)
    {
        if (atConsole)
            playersAtConsole.Add(steamId);
        else
            playersAtConsole.Remove(steamId);
    }

    /// <summary>
    /// Gets the VoiceRemotePlayer for a specific sender (used by VoiceChatP2P).
    /// Creates one with a fallback proxy GameObject if avatar not yet registered.
    /// </summary>
    public VoiceRemotePlayer GetOrCreateVoiceRemotePlayer(SteamId steamId)
    {
        if (voiceRemotePlayers.TryGetValue(steamId, out var existing))
            return existing;

        // Create fallback proxy if no avatar registered yet
        GameObject proxy = new GameObject($"VoiceProxy_{steamId}");
        DontDestroyOnLoad(proxy);
        
        var voicePlayer = proxy.AddComponent<VoiceRemotePlayer>();
        voicePlayer.Initialize(steamId);
        voiceRemotePlayers[steamId] = voicePlayer;

        if (enableDebugLogs)
            Debug.Log($"[VoiceSessionManager] Created fallback voice proxy for {steamId}");

        return voicePlayer;
    }

    // ===== Voice Gating Logic =====

    /// <summary>
    /// Applies role-based voice gating rules to all remote players.
    /// </summary>
    private void ApplyVoiceGating()
    {
        if (PlayerStateManager.Instance == null) return;

        SteamId localId = SteamManager.Instance.PlayerSteamId;
        PlayerState localState = PlayerStateManager.Instance.GetPlayerState(localId);

        foreach (var kvp in voiceRemotePlayers)
        {
            SteamId remoteSteamId = kvp.Key;
            VoiceRemotePlayer voicePlayer = kvp.Value;

            if (voicePlayer == null) continue;

            PlayerState remoteState = PlayerStateManager.Instance.GetPlayerState(remoteSteamId);

            bool shouldHear = localState.Scene == NetworkSceneId.Lobby || ShouldHearPlayer(localState, remoteState, remoteSteamId);
            
            // Control AudioSource enabled state
            AudioSource audioSource = voicePlayer.GetComponent<AudioSource>();
            if (audioSource != null)
            {
                audioSource.enabled = shouldHear;
            }
        }
    }

    /// <summary>
    /// Determines if the local player should hear a remote player based on roles and rules.
    /// </summary>
    private bool ShouldHearPlayer(PlayerState localState, PlayerState remoteState, SteamId remoteSteamId)
    {
        // Lobby: Everyone hears everyone
        if (localState.Role == PlayerRole.Lobby)
            return true;

        // Ground Control rules
        if (localState.Role == PlayerRole.GroundControl)
        {
            // Always hear other ground control players
            if (remoteState.Role == PlayerRole.GroundControl)
                return true;

            // Only hear space players when at console
            if (remoteState.Role == PlayerRole.SpaceStation)
                return isLocalPlayerAtConsole;

            return false;
        }

        // Space Station rules
        if (localState.Role == PlayerRole.SpaceStation)
        {
            // Always hear ground control
            if (remoteState.Role == PlayerRole.GroundControl)
                return true;

            // Hear other space players within proximity
            if (remoteState.Role == PlayerRole.SpaceStation)
            {
                return IsWithinProximity(remoteSteamId);
            }

            return false;
        }

        // Default: don't hear
        return false;
    }

    /// <summary>
    /// Public method to check proximity for voice SENDING (used by VoiceChatP2P).
    /// </summary>
    public bool IsWithinProximityForSending(SteamId remoteSteamId)
    {
        return IsWithinProximity(remoteSteamId);
    }

    /// <summary>
    /// Checks if a remote player is within voice proximity (for space-to-space).
    /// </summary>
    private bool IsWithinProximity(SteamId remoteSteamId)
    {
        // Try to find the remote player's avatar position
        if (!remotePlayerAvatars.TryGetValue(remoteSteamId, out var remoteAvatar))
            return false;

        if (remoteAvatar == null)
            return false;

        // Find local player position (you'll need a reference to the local player object)
        GameObject localPlayer = FindLocalPlayerAvatar();
        if (localPlayer == null)
            return true; // If we can't find local player, allow voice by default

        float distance = Vector3.Distance(localPlayer.transform.position, remoteAvatar.transform.position);
        return distance <= spaceProximityRadius;
    }

    /// <summary>
    /// Finds the local player's avatar GameObject. Customize based on your player setup.
    /// </summary>
    private GameObject FindLocalPlayerAvatar()
    {
        // TODO: Replace with actual local player reference
        // Example: return PlayerController.Instance?.gameObject;
        GameObject localPlayer = GameObject.FindGameObjectWithTag("Player");
        return localPlayer;
    }

    // ===== Event Handlers =====

    private void OnPlayerJoined(SteamId steamId)
    {
        if (enableDebugLogs)
            Debug.Log($"[VoiceSessionManager] Player joined: {steamId}");
        
        // VoiceRemotePlayer will be created on-demand when voice data arrives
        // or when avatar is registered
    }

    private void OnPlayerLeft(SteamId steamId)
    {
        UnregisterRemotePlayer(steamId);
    }

    private void OnPlayerSceneChanged(SteamId steamId, NetworkSceneId sceneId)
    {
        if (enableDebugLogs)
            Debug.Log($"[VoiceSessionManager] Player {steamId} changed scene to {sceneId}");
        
        // Re-evaluate voice gating on next frame
    }
}
}
