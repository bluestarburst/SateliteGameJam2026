using System.Threading.Tasks;
using Steamworks;
using Steamworks.Data;
using UnityEngine;
using UnityEngine.UI;

// Displays active unranked lobbies in a ScrollView and joins on click
public class UnrankedLobbiesView : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private RectTransform contentRoot;   // Assign ScrollView content transform
    [SerializeField] private Button lobbyItemButtonPrefab; // Assign a Button prefab with a Text child
    [SerializeField] private bool autoRefreshOnEnable = true;

    [Header("Optional")]
    [SerializeField] private string emptyStateMessage = "No lobbies found";

    private void OnEnable()
    {
        if (autoRefreshOnEnable)
        {
            RefreshList();
        }
    }

    public async void RefreshList()
    {
        if (SteamManager.Instance == null || !SteamManager.Instance.ConnectedToSteam())
        {
            Debug.Log("Steam not initialized; cannot refresh lobbies.");
            return;
        }

        // Fetch latest unranked lobbies
        await SteamManager.Instance.RefreshMultiplayerLobbies(ranked: false);

        // Rebuild UI
        ClearContent();

        var lobbies = SteamManager.Instance.activeUnrankedLobbies;
        if (lobbies == null || lobbies.Count == 0)
        {
            if (lobbyItemButtonPrefab != null && contentRoot != null)
            {
                var placeholder = Instantiate(lobbyItemButtonPrefab, contentRoot);
                var text = placeholder.GetComponentInChildren<TMPro.TMP_Text>();
                if (text) text.text = emptyStateMessage;
                placeholder.interactable = false;
            }
            return;
        }

        foreach (var lobby in lobbies)
        {
            CreateLobbyButton(lobby);
        }
    }

    private void CreateLobbyButton(Lobby lobby)
    {
        if (lobbyItemButtonPrefab == null || contentRoot == null)
        {
            Debug.LogWarning("UnrankedLobbiesView not configured: assign contentRoot and lobbyItemButtonPrefab.");
            return;
        }

        var btn = Instantiate(lobbyItemButtonPrefab, contentRoot);
        string ownerName = lobby.Owner.Name ?? "Host";
        int memberCount = lobby.MemberCount;
        int maxMembers = lobby.MaxMembers;

        var text = btn.GetComponentInChildren<TMPro.TMP_Text>();
        if (text) text.text = $"{ownerName}  ({memberCount}/{maxMembers})";

        Debug.Log($"Found lobby: {lobby.Id} hosted by {ownerName} with {memberCount}/{maxMembers} members");

        Debug.Log(btn);

        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => TryJoinLobby(lobby));
    }

    private async void TryJoinLobby(Lobby lobby)
    {
        if (SteamManager.Instance == null)
        {
            Debug.Log("SteamManager missing");
            return;
        }

        // Leave current lobby if any
        if (SteamManager.Instance.currentLobby.Id.Value != 0)
        {
            SteamManager.Instance.LeaveLobby();
        }

        // Attempt join
        RoomEnter result = await lobby.Join();
        if (result != RoomEnter.Success)
        {
            Debug.Log("Failed to join lobby: " + result);
            return;
        }

        // Set opponent to lobby owner (host) so OnLobbyEntered/flow can proceed
        SteamManager.Instance.currentLobby = lobby;
        SteamManager.Instance.LobbyPartner = lobby.Owner;
        SteamManager.Instance.OpponentSteamId = lobby.Owner.Id;
        SteamManager.Instance.LobbyPartnerDisconnected = false;

        // Proactively accept P2P in case callback order differs
        try { SteamNetworking.AcceptP2PSessionWithUser(lobby.Owner.Id); } catch { }

        Debug.Log($"Joined lobby {lobby.Id} hosted by {lobby.Owner.Name}");
    }

    private void ClearContent()
    {
        if (contentRoot == null) return;
        for (int i = contentRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(contentRoot.GetChild(i).gameObject);
        }
    }
}
