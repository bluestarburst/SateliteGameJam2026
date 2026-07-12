using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Steamworks;
using Steamworks.Data;
using UnityEngine;
using UnityEngine.SceneManagement;
using SatelliteGameJam.Networking.Core;
using SatelliteGameJam.Networking.State;
using SatelliteGameJam.Networking.Messages;
using SatelliteGameJam.Networking.Voice;

/// <summary>
/// Steamworks entry point that mirrors the Facepunch Steamworks tutorial core loop.
/// Keeps lobby state, accepts P2P, and optionally relays traffic through SteamNetworkingSockets.
/// </summary>
public class SteamManager : MonoBehaviour
{
    public static SteamManager Instance;

    private int playerElo = 0;

    public string PlayerName { get; private set; } = string.Empty;
    public SteamId PlayerSteamId { get; private set; }
    public string PlayerSteamIdString { get; private set; } = "NoSteamId";

    private Friend lobbyPartner;
    public Friend LobbyPartner
    {
        get => lobbyPartner;
        set => lobbyPartner = value;
    }

    // Multi-peer tracking
    public event Action<SteamId, string> RemotePlayerJoined;
    public event Action<SteamId> RemotePlayerLeft;
    private readonly Dictionary<SteamId, Friend> remoteMembers = new();
    public IReadOnlyCollection<SteamId> RemotePlayerIds => remoteMembers.Keys;

    public List<Lobby> activeUnrankedLobbies = new();
    public List<Lobby> activeRankedLobbies = new();
    public Lobby currentLobby;
    private Lobby hostedMultiplayerLobby;
    private ulong authorityLobbyId;
    private SteamId lobbyHostId;
    private bool isLobbyTransitionInProgress;
    private ulong pendingLobbyId;

    // Socket state
    private SteamSocketManager steamSocketManager;
    private SteamConnectionManager steamConnectionManager;
    private bool activeSteamSocketServer;
    private bool activeSteamSocketConnection;
    private bool isHost;

    // Lobby data keys
    private const string TRUE = "true";
    private const string FALSE = "false";
    private const string isFriendLobby = "is_friend_lobby";
    private const string isRankedDataString = "is_ranked";
    private const string staticDataString = "static_data";
    private const string ownerNameDataString = "owner_name";
    private const string playerEloDataString = "player_elo";

    private bool applicationHasQuit;
    private bool theRealOne;

    public bool IsDevelopmentSessionActive { get; private set; }

    public void Awake()
    {
        if (Instance == null)
        {
            theRealOne = true;
            DontDestroyOnLoad(gameObject);
            Instance = this;
            PlayerName = string.Empty;
            TryInitSteamClient();
            SteamNetworkingUtils.InitRelayNetworkAccess();
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void TryInitSteamClient()
    {
        try
        {
            SteamClient.Init(GetSteamAppId(), true);
            if (!SteamClient.IsValid)
            {
                Debug.Log("Steam client not valid");
                throw new Exception();
            }

            PlayerName = SteamClient.Name;
            PlayerSteamId = SteamClient.SteamId;
            PlayerSteamIdString = PlayerSteamId.ToString();
            activeUnrankedLobbies = new List<Lobby>();
            activeRankedLobbies = new List<Lobby>();
            Debug.Log("Steam initialized: " + PlayerName);
        }
        catch (Exception e)
        {
            Debug.Log("Error connecting to Steam");
            Debug.Log(e);
        }
    }

    public bool TryToReconnectToSteam()
    {
        Debug.Log("Attempting to reconnect to Steam");
        try
        {
            SteamClient.Init(GetSteamAppId(), true);
            if (!SteamClient.IsValid)
            {
                Debug.Log("Steam client not valid");
                throw new Exception();
            }

            PlayerName = SteamClient.Name;
            PlayerSteamId = SteamClient.SteamId;
            PlayerSteamIdString = PlayerSteamId.ToString();
            activeUnrankedLobbies = new List<Lobby>();
            activeRankedLobbies = new List<Lobby>();
            Debug.Log("Steam initialized: " + PlayerName);
            return true;
        }
        catch (Exception e)
        {
            Debug.Log("Error connecting to Steam");
            Debug.Log(e);
            return false;
        }
    }

    public bool ConnectedToSteam()
    {
        return SteamClient.IsValid;
    }

    private uint GetSteamAppId()
    {
        return NetworkingConfiguration.Instance?.steamAppId ?? 480;
    }

    private void Start()
    {
        SteamMatchmaking.OnLobbyGameCreated += OnLobbyGameCreatedCallback;
        SteamMatchmaking.OnLobbyCreated += OnLobbyCreatedCallback;
        SteamMatchmaking.OnLobbyEntered += OnLobbyEnteredCallback;
        SteamMatchmaking.OnLobbyMemberJoined += OnLobbyMemberJoinedCallback;
        SteamMatchmaking.OnChatMessage += OnChatMessageCallback;
        SteamMatchmaking.OnLobbyMemberDisconnected += OnLobbyMemberDisconnectedCallback;
        SteamMatchmaking.OnLobbyMemberLeave += OnLobbyMemberLeaveCallback;
        SteamFriends.OnGameLobbyJoinRequested += OnGameLobbyJoinRequestedCallback;
        SteamApps.OnDlcInstalled += OnDlcInstalledCallback;
        SceneManager.sceneLoaded += OnSceneLoaded;

        UpdateRichPresenceStatus(SceneManager.GetActiveScene().name);

        RunDevelopmentSession();
    }

    private void Update()
    {
        SteamClient.RunCallbacks();
        try
        {
            if (activeSteamSocketServer)
            {
                steamSocketManager.Receive();
            }

            if (activeSteamSocketConnection)
            {
                steamConnectionManager.Receive();
            }
        }
        catch
        {
            Debug.Log("Error receiving data on socket/connection");
        }
    }

    private void OnDisable()
    {
        if (theRealOne)
        {
            GameCleanup();
        }
    }

    private void OnDestroy()
    {
        if (theRealOne)
        {
            GameCleanup();
        }
    }

    private void OnApplicationQuit()
    {
        if (theRealOne)
        {
            GameCleanup();
        }
    }

    private void GameCleanup()
    {
        if (applicationHasQuit)
        {
            return;
        }

        applicationHasQuit = true;
        LeaveLobby();
        try
        {
            SteamClient.Shutdown();
        }
        catch
        {
            // ignore
        }
    }

    private void OnLobbyMemberDisconnectedCallback(Lobby lobby, Friend friend)
    {
        if (!ShouldAcceptLobbyCallback(lobby)) return;
        OtherLobbyMemberLeft(friend);
    }

    private void OnLobbyMemberLeaveCallback(Lobby lobby, Friend friend)
    {
        if (!ShouldAcceptLobbyCallback(lobby)) return;
        OtherLobbyMemberLeft(friend);
    }

    private void OtherLobbyMemberLeft(Friend friend)
    {
        if (friend.Id == PlayerSteamId)
        {
            return;
        }

        Debug.Log("Opponent has left the lobby");
        RemoveRemoteMember(friend.Id);
    }

    private void OnLobbyGameCreatedCallback(Lobby lobby, uint ip, ushort port, SteamId steamId)
    {
        if (!ShouldAcceptLobbyCallback(lobby))
        {
            return;
        }

        CaptureLobbyHost(lobby);
        SyncRemoteMembersWithLobby(lobby);
        if (ShouldRouteToLobbyFromSteamEvent())
        {
            RouteToLobbyScene();
        }
    }

    private void AcceptP2P(SteamId opponentId)
    {
        try
        {
            SteamNetworking.AcceptP2PSessionWithUser(opponentId);
        }
        catch
        {
            Debug.Log("Unable to accept P2P Session with user");
        }
    }

    private void OnChatMessageCallback(Lobby lobby, Friend friend, string message)
    {
        if (friend.Id == PlayerSteamId)
        {
            return;
        }

        Debug.Log("incoming chat message");
        Debug.Log(message);
    }

    private void OnLobbyEnteredCallback(Lobby lobby)
    {
        if (!ShouldAcceptLobbyCallback(lobby))
        {
            return;
        }

        ActivateLobby(lobby, forceLobbyRoute: false);

        // An invite join waits for the host's PlayerSceneState assignment. Loading Lobby here
        // can finish after that assignment and overwrite the authoritative gameplay scene.
        if (!isLobbyTransitionInProgress && lobby.MemberCount != 1 && ShouldRouteToLobbyFromSteamEvent())
        {
            RouteToLobbyScene();
        }
    }

    private async void OnGameLobbyJoinRequestedCallback(Lobby joinedLobby, SteamId id)
    {
        await JoinLobbyAsync(joinedLobby, id);
    }

    private void OnLobbyCreatedCallback(Result result, Lobby lobby)
    {
        if (result != Result.OK)
        {
            Debug.Log("lobby creation result not ok");
            Debug.Log(result.ToString());
        }
    }

    private void OnLobbyMemberJoinedCallback(Lobby lobby, Friend friend)
    {
        Debug.Log("someone else joined lobby");
        if (!ShouldAcceptLobbyCallback(lobby)) return;
        if (friend.Id == PlayerSteamId)
        {
            return;
        }

        AddRemoteMember(friend);
        SceneSyncManager.Instance?.AssignJoiningPlayer(friend.Id);
    }

    private void OnDlcInstalledCallback(AppId appId)
    {
        // hook for DLC install events
    }

    public async Task<bool> RefreshMultiplayerLobbies(bool ranked)
    {
        try
        {
            if (ranked)
            {
                activeRankedLobbies.Clear();
                Lobby[] lobbies = await SteamMatchmaking.LobbyList
                    .WithMaxResults(20)
                    .WithKeyValue(isRankedDataString, TRUE)
                    .OrderByNear(playerEloDataString, playerElo)
                    .RequestAsync();
                if (lobbies != null)
                {
                    activeRankedLobbies.AddRange(lobbies.ToList());
                }
            }
            else
            {
                activeUnrankedLobbies.Clear();
                Lobby[] lobbies = await SteamMatchmaking.LobbyList
                    .WithMaxResults(20)
                    .WithKeyValue(isRankedDataString, FALSE)
                    .RequestAsync();
                if (lobbies != null)
                {
                    activeUnrankedLobbies.AddRange(lobbies.ToList());
                }
            }

            return true;
        }
        catch (Exception e)
        {
            Debug.Log(e.ToString());
            Debug.Log("Error fetching multiplayer lobbies");
            return true;
        }
    }

    public void LeaveLobby()
    {
        ResetSessionForLobbyTransition();

        try
        {
            if (currentLobby.Id.Value != 0)
            {
                currentLobby.Leave();
            }
        }
        catch
        {
            Debug.Log("Error leaving current lobby");
        }

        foreach (var remote in remoteMembers.Keys.ToList())
        {
            RemoveRemoteMember(remote);
        }
        remoteMembers.Clear();
        currentLobby = default;
        hostedMultiplayerLobby = default;
        authorityLobbyId = 0;
        lobbyHostId = 0;
        isHost = false;
        IsDevelopmentSessionActive = false;
        PlayerStateManager.Instance?.ResetForLobbyTransition();
    }

    /// <summary>
    /// Leaves any current lobby, clears lobby-owned runtime state, then joins the requested lobby.
    /// Steam invite callbacks and lobby-browser clicks both use this path.
    /// </summary>
    public async Task<bool> JoinLobbyAsync(Lobby lobby, SteamId inviter = default)
    {
        if (!ConnectedToSteam())
        {
            Debug.LogWarning("[SteamManager] Cannot join a lobby because Steam is not available.");
            return false;
        }

        if (lobby.Id.Value == 0)
        {
            Debug.LogWarning("[SteamManager] Cannot join an invalid lobby.");
            return false;
        }

        if (isLobbyTransitionInProgress)
        {
            Debug.LogWarning("[SteamManager] A lobby transition is already in progress.");
            return false;
        }

        if (HasActiveLobby && currentLobby.Id == lobby.Id)
        {
            ActivateLobby(lobby, forceLobbyRoute: false, inviter);
            return true;
        }

        isLobbyTransitionInProgress = true;
        pendingLobbyId = lobby.Id.Value;
        try
        {
            LeaveLobby();
            isLobbyTransitionInProgress = true;

            RoomEnter result = await lobby.Join();
            if (result != RoomEnter.Success)
            {
                Debug.LogWarning($"[SteamManager] Failed to join lobby {lobby.Id}: {result}");
                return false;
            }

            // SceneSyncManager receives the host's role/scene assignment after the lobby join.
            // Do not load Lobby as an intermediate scene; it can overwrite that assignment.
            ActivateLobby(lobby, forceLobbyRoute: false, inviter);
            Debug.Log($"[SteamManager] Joined lobby {lobby.Id} hosted by {lobby.Owner.Name}.");
            return true;
        }
        finally
        {
            pendingLobbyId = 0;
            isLobbyTransitionInProgress = false;
        }
    }

    public async Task<bool> CreateFriendLobby(int maxPlayers = 4)
    {
        try
        {
            var createLobbyOutput = await SteamMatchmaking.CreateLobbyAsync(maxPlayers);
            if (!createLobbyOutput.HasValue)
            {
                Debug.Log("Lobby created but not correctly instantiated");
                throw new Exception();
            }

            hostedMultiplayerLobby = createLobbyOutput.Value;
            hostedMultiplayerLobby.SetData(isFriendLobby, TRUE);
            hostedMultiplayerLobby.SetData(ownerNameDataString, PlayerName);
            hostedMultiplayerLobby.SetFriendsOnly();

            currentLobby = hostedMultiplayerLobby;
            CaptureLobbyHost(hostedMultiplayerLobby);
            isHost = true;
            PlayerStateManager.Instance?.HandleLocalLobbyEntered();
            return true;
        }
        catch (Exception exception)
        {
            Debug.Log("Failed to create multiplayer lobby");
            Debug.Log(exception.ToString());
            return false;
        }
    }

    public async Task<bool> CreateLobby(int lobbyParameters, int maxPlayers = 4)
    {
        try
        {
            var createLobbyOutput = await SteamMatchmaking.CreateLobbyAsync(maxPlayers);
            if (!createLobbyOutput.HasValue)
            {
                Debug.Log("Lobby created but not correctly instantiated");
                throw new Exception();
            }

            hostedMultiplayerLobby = createLobbyOutput.Value;
            hostedMultiplayerLobby.SetPublic();
            hostedMultiplayerLobby.SetJoinable(true);
            hostedMultiplayerLobby.SetData(staticDataString, lobbyParameters.ToString());
            hostedMultiplayerLobby.SetData(isRankedDataString, FALSE);
            hostedMultiplayerLobby.SetData(ownerNameDataString, PlayerName);
            hostedMultiplayerLobby.SetData(playerEloDataString, playerElo.ToString());

            currentLobby = hostedMultiplayerLobby;
            CaptureLobbyHost(hostedMultiplayerLobby);
            isHost = true;
            PlayerStateManager.Instance?.HandleLocalLobbyEntered();
            return true;
        }
        catch (Exception exception)
        {
            Debug.Log("Failed to create multiplayer lobby");
            Debug.Log(exception.ToString());
            return false;
        }
    }

    public void OpenFriendOverlayForGameInvite()
    {
        if (!ConnectedToSteam())
        {
            Debug.LogWarning("[SteamManager] Steam overlay is unavailable because Steam is not initialized.");
            return;
        }

        if (!HasActiveLobby)
        {
            Debug.LogWarning("[SteamManager] Create or join a lobby before inviting friends.");
            return;
        }

        SteamFriends.OpenGameInviteOverlay(currentLobby.Id);
    }

    /// <summary>Creates a public joinable lobby when necessary, then opens Steam's invite UI.</summary>
    public async void CreateJoinableLobbyAndOpenInvite()
    {
        if (!ConnectedToSteam())
        {
            Debug.LogWarning("[SteamManager] Steam is unavailable, so an invite lobby cannot be created.");
            return;
        }

        if (!HasActiveLobby && !await CreateLobby(0))
        {
            return;
        }

        OpenFriendOverlayForGameInvite();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode loadSceneMode)
    {
        UpdateRichPresenceStatus(scene.name);
    }

    public void UpdateRichPresenceStatus(string sceneName)
    {
        if (!SteamClient.IsValid)
        {
            return;
        }

        string richPresenceKey = "steam_display";
        SteamFriends.SetRichPresence(richPresenceKey, "#" + sceneName);
    }

    // --- Multi-peer helpers ---

    public bool HasActiveLobby => currentLobby.Id.Value != 0;

    public bool IsLocalPlayerLobbyHost => IsLobbyHost(PlayerSteamId);

    public bool TryGetLobbyHost(out SteamId hostId)
    {
        hostId = 0;
        if (!HasActiveLobby)
        {
            return false;
        }

        CaptureLobbyHost(currentLobby);
        hostId = lobbyHostId;
        return hostId.Value != 0;
    }

    public bool IsLobbyHost(SteamId steamId)
    {
        return TryGetLobbyHost(out SteamId hostId) && hostId == steamId;
    }

    private void CaptureLobbyHost(Lobby lobby)
    {
        if (lobby.Id.Value == 0)
        {
            return;
        }

        if (authorityLobbyId == lobby.Id.Value && lobbyHostId.Value != 0)
        {
            return;
        }

        authorityLobbyId = lobby.Id.Value;
        lobbyHostId = lobby.Owner.Id;
    }

    private bool ShouldAcceptLobbyCallback(Lobby lobby)
    {
        if (!isLobbyTransitionInProgress || pendingLobbyId == 0)
        {
            return currentLobby.Id.Value != 0 && currentLobby.Id == lobby.Id;
        }

        return lobby.Id.Value == pendingLobbyId;
    }

    private void ActivateLobby(Lobby lobby, bool forceLobbyRoute, SteamId inviter = default)
    {
        bool newLobby = currentLobby.Id != lobby.Id;
        currentLobby = lobby;
        hostedMultiplayerLobby = default;
        isHost = lobby.Owner.Id == PlayerSteamId;
        CaptureLobbyHost(lobby);

        if (inviter.Value != 0)
        {
            lobbyPartner = SteamFriends.GetFriends().FirstOrDefault(friend => friend.Id == inviter);
        }
        else
        {
            lobbyPartner = lobby.Owner;
        }

        SyncRemoteMembersWithLobby(lobby);
        if (newLobby)
        {
            PlayerStateManager.Instance?.HandleLocalLobbyEntered();
        }

        if (forceLobbyRoute)
        {
            RouteToLobbyScene();
        }
    }

    private void ResetSessionForLobbyTransition()
    {
        SceneSyncManager.Instance?.ResetForLobbyTransition();
        NetworkConnectionManager.Instance?.CleanupAllRemotePlayers();
        VoiceSessionManager.Instance?.ResetForLobbyTransition();
        SatelliteStateManager.Instance?.ResetForLobbyTransition();
    }

    private void SyncRemoteMembersWithLobby(Lobby lobby)
    {
        if (lobby.Id.Value == 0) return;

        foreach (var member in lobby.Members)
        {
            AddRemoteMember(member);
        }

        var toRemove = remoteMembers.Keys
            .Where(id => lobby.Members.All(m => m.Id != id))
            .ToList();

        foreach (var id in toRemove)
        {
            RemoveRemoteMember(id);
        }
    }

    private void AddRemoteMember(Friend friend)
    {
        if (friend.Id == PlayerSteamId) return;

        remoteMembers[friend.Id] = friend;

        AcceptP2P(friend.Id);

        RemotePlayerJoined?.Invoke(friend.Id, friend.Name);
    }

    private void RemoveRemoteMember(SteamId steamId)
    {
        if (steamId == PlayerSteamId) return;

        bool removed = remoteMembers.Remove(steamId);
        if (removed)
        {
            try
            {
                SteamNetworking.CloseP2PSessionWithUser(steamId);
            }
            catch
            {
                Debug.Log("Unable to close P2P session cleanly for remote member");
            }

            RemotePlayerLeft?.Invoke(steamId);
            PlayerStateManager.Instance?.SetPlayerConnected(steamId, false);
            TryDespawnRemotePlayer(steamId);
        }
    }

    private void RouteToLobbyScene()
    {
        if (SceneFlowController.Instance != null && SceneFlowController.Instance.LoadLobbyScene())
        {
            return;
        }

        string fallbackLobby = NetworkingConfiguration.Instance?.GetSceneName(NetworkSceneId.Lobby);

        if (!string.IsNullOrWhiteSpace(fallbackLobby))
        {
            SceneManager.LoadScene(fallbackLobby);
        }
    }

    private bool ShouldRouteToLobbyFromSteamEvent()
    {
        // If we are already in a gameplay scene, ignore late/duplicate Steam lobby callbacks.
        if (PlayerStateManager.Instance != null && PlayerSteamId.Value != 0)
        {
            PlayerState localState = PlayerStateManager.Instance.GetPlayerState(PlayerSteamId);
            if (localState.Scene == NetworkSceneId.GroundControl || localState.Scene == NetworkSceneId.SpaceStation)
            {
                return false;
            }
        }

        string activeScene = SceneManager.GetActiveScene().name;
        if (SceneFlowController.Instance != null)
        {
            return SceneFlowController.Instance.IsLobbyOrMatchmakingScene(activeScene);
        }

        return activeScene == "Matchmaking" || activeScene == "Lobby";
    }

    public bool TryGetDevelopmentJoinAssignment(out PlayerRole role, out NetworkSceneId scene)
    {
        role = PlayerRole.None;
        scene = NetworkSceneId.None;

        if (!IsDevelopmentSessionActive)
        {
            return false;
        }

        GameFlowDefinition definition = SceneFlowController.Instance?.Definition ??
            NetworkingConfiguration.Instance?.gameFlowDefinition;
        if (definition == null)
        {
            return false;
        }

        PlayerRole localRole = PlayerStateManager.Instance?.GetPlayerState(PlayerSteamId).Role ?? PlayerRole.None;
        role = definition.ResolveDevelopmentJoinRole(localRole);
        scene = definition.ResolveDevelopmentJoinScene(role);
        return role != PlayerRole.None && scene != NetworkSceneId.None;
    }

    private async void RunDevelopmentSession()
    {
#if !(UNITY_EDITOR || DEVELOPMENT_BUILD)
        return;
#else
        GameFlowDefinition definition = SceneFlowController.Instance?.Definition ??
            NetworkingConfiguration.Instance?.gameFlowDefinition;
        if (definition == null || !definition.DevSession.enabled)
        {
            return;
        }

        if (SceneFlowController.Instance == null ||
            !SceneFlowController.Instance.TryGetSceneId(SceneManager.GetActiveScene().name, out NetworkSceneId activeScene))
        {
            Debug.LogWarning("[SteamManager] Development session requires the active scene to be mapped in GameFlowDefinition.");
            return;
        }

        if (PlayerSteamId.Value == 0)
        {
            Debug.Log("[SteamManager] Development session is running locally because Steam is unavailable.");
            return;
        }

        PlayerRole localRole = definition.ResolveDevelopmentLocalRole(activeScene);
        if (PlayerStateManager.Instance != null)
        {
            if (localRole != PlayerRole.None)
            {
                PlayerStateManager.Instance.SetLocalPlayerRole(localRole);
            }

            PlayerStateManager.Instance.SetLocalPlayerScene(activeScene);
        }

        if (!definition.DevSession.createJoinableLobby || HasActiveLobby)
        {
            IsDevelopmentSessionActive = true;
            return;
        }

        if (!await CreateLobby(0))
        {
            Debug.LogWarning("[SteamManager] Development session could not create its joinable lobby.");
            return;
        }

        IsDevelopmentSessionActive = true;

        // Re-broadcast after the lobby exists so late joiners have an authoritative baseline.
        if (PlayerStateManager.Instance != null)
        {
            if (localRole != PlayerRole.None)
            {
                PlayerStateManager.Instance.SetLocalPlayerRole(localRole);
            }

            PlayerStateManager.Instance.SetLocalPlayerScene(activeScene);
        }
#endif
    }

    private void TryDespawnRemotePlayer(SteamId steamId)
    {
        if (NetworkConnectionManager.Instance == null) return;
        NetworkConnectionManager.Instance.DespawnRemotePlayer(steamId);
    }

    // --- SteamNetworkingSockets helpers (optional relay path) ---
    public void CreateSteamSocketServer()
    {
        steamSocketManager = SteamNetworkingSockets.CreateRelaySocket<SteamSocketManager>(0);
        steamConnectionManager = SteamNetworkingSockets.ConnectRelay<SteamConnectionManager>(PlayerSteamId);
        activeSteamSocketServer = true;
        activeSteamSocketConnection = true;
        isHost = true;
    }

    public void JoinSteamSocketServer()
    {
        if (isHost)
        {
            return;
        }

        Debug.Log("joining socket server");
        activeSteamSocketServer = false;
        activeSteamSocketConnection = true;
    }

    public void LeaveSteamSocketServer()
    {
        activeSteamSocketServer = false;
        activeSteamSocketConnection = false;
        try
        {
            steamConnectionManager?.Close();
            steamSocketManager?.Close();
        }
        catch
        {
            Debug.Log("Error closing socket server / connection manager");
        }
    }

    public void RelaySocketMessageReceived(IntPtr message, int size, uint connectionSendingMessageId)
    {
        try
        {
            foreach (var connection in steamSocketManager.Connected)
            {
                if (connection.Id == connectionSendingMessageId)
                {
                    continue;
                }

                Result success = connection.SendMessage(message, size);
                if (success != Result.OK)
                {
                    _ = connection.SendMessage(message, size);
                }
            }
        }
        catch
        {
            Debug.Log("Unable to relay socket server message");
        }
    }

    public bool SendMessageToSocketServer(byte[] messageToSend)
    {
        try
        {
            int sizeOfMessage = messageToSend.Length;
            IntPtr intPtrMessage = Marshal.AllocHGlobal(sizeOfMessage);
            Marshal.Copy(messageToSend, 0, intPtrMessage, sizeOfMessage);
            Result success = steamConnectionManager.Connection.SendMessage(intPtrMessage, sizeOfMessage, SendType.Reliable);
            if (success != Result.OK)
            {
                success = steamConnectionManager.Connection.SendMessage(intPtrMessage, sizeOfMessage, SendType.Reliable);
            }

            Marshal.FreeHGlobal(intPtrMessage);
            return success == Result.OK;
        }
        catch (Exception e)
        {
            Debug.Log(e.Message);
            Debug.Log("Unable to send message to socket server");
            return false;
        }
    }

    public void ProcessMessageFromSocketServer(IntPtr messageIntPtr, int dataBlockSize)
    {
        try
        {
            byte[] message = new byte[dataBlockSize];
            Marshal.Copy(messageIntPtr, message, 0, dataBlockSize);
            string messageString = System.Text.Encoding.UTF8.GetString(message);
            Debug.Log($"Socket message received: {messageString}");
            // Handle socket payload here.
        }
        catch
        {
            Debug.Log("Unable to process message from socket server");
        }
    }
}
