using Steamworks;
using Steamworks.Data;
using UnityEngine;
using UnityEngine.UI;
using SatelliteGameJam.Networking.Core;

// Displays active unranked lobbies in a ScrollView and joins on click
public class UnrankedLobbiesView : MonoBehaviour
{
    private const float RefreshIntervalSeconds = 5f;

    [Header("UI References")]
    [SerializeField] private RectTransform contentRoot;   // Assign ScrollView content transform
    [SerializeField] private Button lobbyItemButtonPrefab; // Assign a Button prefab with a Text child
    [Header("Optional")]
    [SerializeField] private string emptyStateMessage = "No lobbies found";

    private void Start()
    {
        if (SteamManager.Instance == null || !SteamManager.Instance.ConnectedToSteam())
        {
            Debug.Log("Steam not initialized; cannot display lobbies.");
            return;
        }

        // create repeating refresh every 30 seconds
        InvokeRepeating(nameof(RefreshList), 0f, RefreshIntervalSeconds);
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
        string ownerName = lobby.GetData("owner_name") ?? lobby.Owner.Name;
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

        if (!await SteamManager.Instance.JoinLobbyAsync(lobby, lobby.Owner.Id))
        {
            return;
        }
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
