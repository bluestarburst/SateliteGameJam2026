using Steamworks.Data;
using Steamworks;
using UnityEngine;
using TMPro;

// Displays members of the current lobby in a ScrollView
public class LobbyPlayersView : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private RectTransform contentRoot;   // ScrollView content
    [SerializeField] private TMP_Text playerItemTextPrefab;   // Simple TMP_Text prefab per player
    [SerializeField] private bool autoRefreshOnEnable = true;

    [Header("Optional")]
    [SerializeField] private string emptyStateMessage = "Waiting for players…";

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

    public void RefreshList()
    {
        if (SteamManager.Instance == null || SteamManager.Instance.currentLobby.Id.Value == 0)
        {
            Debug.Log("No active lobby to display");
            ClearContent();
            AddPlaceholder(emptyStateMessage);
            return;
        }

        ClearContent();

        var lobby = SteamManager.Instance.currentLobby;
        int count = lobby.MemberCount;
        bool any = false;
        foreach (var member in lobby.Members)
        {
            any = true;
            AddPlayerItem(member.Name);
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
        }
    }

    private void AddPlayerItem(string name)
    {
        if (playerItemTextPrefab == null || contentRoot == null)
        {
            Debug.LogWarning("LobbyPlayersView not configured: assign contentRoot and playerItemTextPrefab.");
            return;
        }

        var text = Instantiate(playerItemTextPrefab, contentRoot);
        text.text = name;
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
}
