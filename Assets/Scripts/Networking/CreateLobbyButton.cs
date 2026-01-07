using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

// Attach to a UI Button; calls SteamManager to create a lobby and loads a lobby scene
public class CreateLobbyButton : MonoBehaviour
{
    [Header("Scene Routing")]
    [SerializeField] private string lobbySceneName = ""; // Set to your lobby scene name

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

        // Route to lobby scene after creation
        if (!string.IsNullOrEmpty(lobbySceneName))
        {
            SceneManager.LoadScene(lobbySceneName);
        }
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

        if (!string.IsNullOrEmpty(lobbySceneName))
        {
            SceneManager.LoadScene(lobbySceneName);
        }
    }
}
