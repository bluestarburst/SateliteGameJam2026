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

/// <summary>
/// Steamworks entry point that mirrors the Facepunch Steamworks tutorial core loop.
/// Keeps lobby state, accepts P2P, and optionally relays traffic through SteamNetworkingSockets.
/// </summary>
public class SteamManager : MonoBehaviour
{
    public static SteamManager Instance;

    [Header("Steam Config")]
    [Tooltip("Replace with your own Steam App ID before shipping.")]
    [SerializeField] private uint gameAppId = 480; // Spacewar default for local testing

    [Header("Scenes & Flow")]
    [Tooltip("Optional scene to load when a lobby is ready.")]
    [SerializeField] private string gameSceneName = string.Empty;
    [SerializeField] private bool autoCreateLobbyForTesting = false;
    
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
            SteamClient.Init(gameAppId, true);
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
            SteamClient.Init(gameAppId, true);
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

        if (autoCreateLobbyForTesting)
        {
            CreateLobby(0);
        }
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
        if (currentLobby.Id != 0 && lobby.Id != currentLobby.Id) return;
        OtherLobbyMemberLeft(friend);
    }

    private void OnLobbyMemberLeaveCallback(Lobby lobby, Friend friend)
    {
        if (currentLobby.Id != 0 && lobby.Id != currentLobby.Id) return;
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
        SyncRemoteMembersWithLobby(lobby);
        if (!string.IsNullOrEmpty(gameSceneName))
        {
            SceneManager.LoadScene(gameSceneName);
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
        lobby.SetJoinable(false);
        lobby.SetGameServer(PlayerSteamId);
    }

    private void OnLobbyEnteredCallback(Lobby lobby)
    {
        SyncRemoteMembersWithLobby(lobby);

        if (lobby.MemberCount != 1 && !string.IsNullOrEmpty(gameSceneName))
        {
            SceneManager.LoadScene(gameSceneName);
        }
    }

    private async void OnGameLobbyJoinRequestedCallback(Lobby joinedLobby, SteamId id)
    {
        RoomEnter joinedLobbySuccess = await joinedLobby.Join();
        if (joinedLobbySuccess != RoomEnter.Success)
        {
            Debug.Log("failed to join lobby");
            return;
        }

        foreach (Friend friend in SteamFriends.GetFriends())
        {
            if (friend.Id == id)
            {
                lobbyPartner = friend;
                break;
            }
        }

        foreach (var remote in remoteMembers.Keys.ToList())
        {
            RemoveRemoteMember(remote);
        }
        remoteMembers.Clear();

        currentLobby = joinedLobby;
        SyncRemoteMembersWithLobby(joinedLobby);
        if (!string.IsNullOrEmpty(gameSceneName))
        {
            SceneManager.LoadScene(gameSceneName);
        }
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
        if (currentLobby.Id != 0 && lobby.Id != currentLobby.Id) return;
        if (friend.Id == PlayerSteamId)
        {
            return;
        }

        AddRemoteMember(friend);
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
        try
        {
            currentLobby.Leave();
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
            isHost = true;
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
            isHost = true;
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
        if (currentLobby.Id != null)
        {
            SteamFriends.OpenGameInviteOverlay(currentLobby.Id);
        }
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
        TryAutoSpawnRemotePlayer(friend.Id, friend.Name);
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
            TryDespawnRemotePlayer(steamId);
        }
    }

    private void TryAutoSpawnRemotePlayer(SteamId steamId, string displayName)
    {
        if (NetworkConnectionManager.Instance == null) return;

        // CRITICAL: Don't spawn remote player prefabs in the Lobby or Matchmaking scenes
        // Lobby uses lightweight voice proxies only, managed by LobbyNetworkingManager
        // Matchmaking scene doesn't need remote player prefabs at all
        // Check both: local player's state AND current scene name (scene name is more reliable during transitions)
        string currentSceneName = SceneManager.GetActiveScene().name;
        bool isLobbyOrMatchmaking = currentSceneName == "Lobby" || currentSceneName == "Matchmaking";

        // Also check PlayerStateManager as a secondary check
        var localState = PlayerStateManager.Instance?.GetPlayerState(PlayerSteamId);
        if (localState != null && localState.Scene == NetworkSceneId.Lobby)
        {
            isLobbyOrMatchmaking = true;
        }

        if (isLobbyOrMatchmaking)
        {
            // Don't spawn - LobbyNetworkingManager will handle voice proxies in Lobby
            Debug.Log($"[SteamManager] Skipping remote player spawn for {displayName} in {currentSceneName} scene");
            return;
        }

        Debug.Log($"Attempting to spawn remote player for {steamId} ({displayName})");
        NetworkConnectionManager.Instance.SpawnRemotePlayerFor(steamId, displayName);
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
