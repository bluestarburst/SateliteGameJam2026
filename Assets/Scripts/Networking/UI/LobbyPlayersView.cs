using Steamworks.Data;
using Steamworks;
using UnityEngine;
using TMPro;
using SatelliteGameJam.Networking.State;
using SatelliteGameJam.Networking.Messages;
using System.Collections.Generic;
using System.Linq;

// Displays members of the current lobby in a ScrollView
public class LobbyPlayersView : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private RectTransform contentRoot;   // ScrollView content
    [SerializeField] private TMP_Text playerItemTextPrefab;   // Simple TMP_Text prefab per player
    [SerializeField] private bool autoRefreshOnEnable = true;

    [Header("Optional")]
    [SerializeField] private string emptyStateMessage = "Waiting for players…";

    private Dictionary<SteamId, GameObject> playerItems = new Dictionary<SteamId, GameObject>();

    public GameObject startButton;

    public UnityEngine.Color spaceTeamColor = new UnityEngine.Color(0.5f, 0.8f, 1f);
    public UnityEngine.Color groundTeamColor = new UnityEngine.Color(0.8f, 0.5f, 0.5f);
    public UnityEngine.Color defaultColor = new UnityEngine.Color(1f, 1f, 1f);

    public bool gateStartButton = true;

    private void Start()
    {
        startButton.SetActive(false);

        if (SteamManager.Instance == null || !SteamManager.Instance.ConnectedToSteam())
        {
            Debug.Log("Steam not initialized; cannot display lobby players.");
            return;
        }

        if (autoRefreshOnEnable) RefreshList();

        // Listen to player state changes to update the list when players change scenes or roles
        if (PlayerStateManager.Instance != null)
        {
            PlayerStateManager.Instance.OnRoleChanged += OnPlayerRoleChanged;
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe when destroyed to prevent callbacks on destroyed object
        if (PlayerStateManager.Instance != null)
        {
            PlayerStateManager.Instance.OnRoleChanged -= OnPlayerRoleChanged;
        }
    }

    private void OnPlayerRoleChanged(SteamId steamId, PlayerRole newRole)
    {
        // Don't process if this component has been destroyed
        if (this == null) return;

        // Update the player's item color based on their new role
        SetPlayerItemColor(steamId, newRole == PlayerRole.SpaceStation ? new UnityEngine.Color(0.5f, 0.8f, 1f) : new UnityEngine.Color(0.8f, 0.5f, 0.5f));

        CanStartGame();
    }

    private void OnEnable()
    {
        SteamMatchmaking.OnLobbyMemberJoined += OnLobbyMemberChanged;
        SteamMatchmaking.OnLobbyMemberLeave += OnLobbyMemberChanged;
        SteamMatchmaking.OnLobbyMemberDisconnected += OnLobbyMemberChanged;

        if (autoRefreshOnEnable) RefreshList();        
    }

    private void OnDisable()
    {
        SteamMatchmaking.OnLobbyMemberJoined -= OnLobbyMemberChanged;
        SteamMatchmaking.OnLobbyMemberLeave -= OnLobbyMemberChanged;
        SteamMatchmaking.OnLobbyMemberDisconnected -= OnLobbyMemberChanged;
    }

    private void CanStartGame()
    {
        if (gateStartButton == false)
        {
            startButton.SetActive(true);
            return;
        }

        if (SteamManager.Instance == null || !SteamManager.Instance.currentLobby.IsOwnedBy(SteamManager.Instance.PlayerSteamId))
        {
            Debug.Log("Cannot start game - not enough players.");
            startButton.SetActive(false);
            return;
        }

        // need at least 2 players on different teams to start
        var members = SteamManager.Instance.currentLobby.Members;
        if (members.Any(m => PlayerStateManager.Instance.GetPlayerState(m.Id)?.Role == PlayerRole.SpaceStation) &&
            members.Any(m => PlayerStateManager.Instance.GetPlayerState(m.Id)?.Role == PlayerRole.GroundControl))
        {
            startButton.SetActive(true);
        }
        else
        {
            Debug.Log("Cannot start game - all players must be on different teams.");
            startButton.SetActive(false);
            return;
        }

        var lobby = SteamManager.Instance.currentLobby;
        startButton.SetActive(lobby.MemberCount > 1);
    }

    public void RefreshList()
    {
        if (SteamManager.Instance == null || SteamManager.Instance.currentLobby.Id.Value == 0)
        {
            Debug.Log("No active lobby to display");
            startButton.SetActive(false);
            ClearContent();
            AddPlaceholder(emptyStateMessage);

            // call again in 2 seconds to check for lobby creation
            Invoke(nameof(RefreshList), 2f);

            return;
        }

        ClearContent();

        var lobby = SteamManager.Instance.currentLobby;
        int count = lobby.MemberCount;
        bool any = false;
        foreach (var member in lobby.Members)
        {
            any = true;
            AddPlayerItem(member.Id, member.Name);
        }

        if (!any)
        {
            AddPlaceholder(emptyStateMessage);
        }
    }

    private void OnLobbyMemberChanged(Lobby lobby, Steamworks.Friend friend)
    {
        // Only refresh for the current lobby
        if (SteamManager.Instance != null && lobby.Id == SteamManager.Instance.currentLobby.Id)
        {
            RefreshList();
            CanStartGame();
        }
    }

    // listen to connection manager for 

    private void AddPlayerItem(SteamId steamId, string name)
    {
        if (playerItemTextPrefab == null || contentRoot == null)
        {
            Debug.LogWarning("LobbyPlayersView not configured: assign contentRoot and playerItemTextPrefab.");
            return;
        }

        var text = Instantiate(playerItemTextPrefab, contentRoot);
        text.text = name;


        // Store reference for future updates (e.g. role changes)
        playerItems[steamId] = text.gameObject;

        // Set initial color based on role
        var playerState = PlayerStateManager.Instance.GetPlayerState(steamId);
        if (playerState != null)
        {
            switch (playerState.Role)
            {
                case PlayerRole.SpaceStation:
                    SetPlayerItemColor(steamId, spaceTeamColor);
                    break;
                case PlayerRole.GroundControl:
                    SetPlayerItemColor(steamId, groundTeamColor);
                    break;
                default:
                    SetPlayerItemColor(steamId, defaultColor);
                    break;
            }
        }
    }

    private void AddPlaceholder(string message)
    {
        if (playerItemTextPrefab == null || contentRoot == null) return;
        var text = Instantiate(playerItemTextPrefab, contentRoot);
        text.text = message;
        text.color = new UnityEngine.Color(text.color.r, text.color.g, text.color.b, 0.7f);
    }

    private void ClearContent()
    {
        if (contentRoot == null) return;
        for (int i = contentRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(contentRoot.GetChild(i).gameObject);
        }
    }

    private void SetPlayerItemColor(SteamId steamId, UnityEngine.Color color)
    {
        if (playerItems.TryGetValue(steamId, out var item))
        {
            // Check if the GameObject still exists
            if (item == null)
            {
                playerItems.Remove(steamId);
                return;
            }

            var text = item.GetComponent<TMP_Text>();
            if (text != null)
            {
                text.color = color;
            }
        }
    }

    public void JoinSpaceTeam()
    {
        if (SteamManager.Instance == null || SteamManager.Instance.currentLobby.Id.Value == 0)
        {
            Debug.LogWarning("Cannot join space team - not in a lobby.");
            return;
        }

        Debug.Log("Joining space team");

        PlayerStateManager.Instance.SetLocalPlayerRole(PlayerRole.SpaceStation);

        SetPlayerItemColor(SteamManager.Instance.PlayerSteamId, new UnityEngine.Color(0.5f, 0.8f, 1f)); // Light blue for space team

        CanStartGame();
    }

    public void JoinGroundTeam()
    {
        if (SteamManager.Instance == null || SteamManager.Instance.currentLobby.Id.Value == 0)
        {
            Debug.LogWarning("Cannot join ground team - not in a lobby.");
            return;
        }

        Debug.Log("Joining ground team");

        PlayerStateManager.Instance.SetLocalPlayerRole(PlayerRole.GroundControl);

        SetPlayerItemColor(SteamManager.Instance.PlayerSteamId, new UnityEngine.Color(0.8f, 0.5f, 0.5f)); // Light red for ground team  

        CanStartGame();
    }

    public void StartGame()
    {
        // Initiate collective scene change if local player owns the lobby
        if (SteamManager.Instance == null || SceneSyncManager.Instance == null)
        {
            Debug.LogWarning("Cannot start game - missing SteamManager or SceneSyncManager.");
            return;
        }

        if (!SteamManager.Instance.currentLobby.IsOwnedBy(SteamManager.Instance.PlayerSteamId))
        {
            Debug.LogWarning("Cannot start game - you are not the lobby owner.");
            return;
        }

        SceneSyncManager.Instance.RequestStartGame();
    }
}
