using UnityEngine;
using SatelliteGameJam.Networking.Core;

// Attach to a UI Button; calls SteamManager to create a lobby and loads a lobby scene
public class CreateLobbyButton : MonoBehaviour
{
    [Header("Lobby Parameters")]
    [SerializeField] private int staticLobbyParams = 0; // Example data stored in lobby

    public async void CreatePublicLobby()
    {
        if (SteamManager.Instance == null)
        {
            Debug.Log("SteamManager missing");
            return;
        }

        bool ok = await SteamManager.Instance.CreateLobby(staticLobbyParams);
        if (!ok)
        {
            Debug.Log("Failed to create lobby");
            return;
        }

        RouteToLobby();
    }

    public async void CreateFriendsOnlyLobby()
    {
        if (SteamManager.Instance == null)
        {
            Debug.Log("SteamManager missing");
            return;
        }

        bool ok = await SteamManager.Instance.CreateFriendLobby();
        if (!ok)
        {
            Debug.Log("Failed to create friends-only lobby");
            return;
        }

        RouteToLobby();
    }

    private void RouteToLobby()
    {
        if (SceneFlowController.Instance != null)
        {
            SceneFlowController.Instance.LoadLobbyScene();
            return;
        }

        Debug.LogWarning("[CreateLobbyButton] Lobby created, but SceneFlowController is unavailable for scene routing.");
    }
}
