using UnityEngine;
using UnityEngine.UI;

namespace SatelliteGameJam.Networking.UI
{
    /// <summary>Attach to a UI Button to open Steam's game-invite overlay for the active lobby.</summary>
    [RequireComponent(typeof(Button))]
    public sealed class SteamInviteFriendsButton : MonoBehaviour
    {
        [SerializeField] private bool createLobbyWhenMissing;

        private Button button;

        private void Awake()
        {
            button = GetComponent<Button>();
        }

        private void OnEnable()
        {
            button.onClick.AddListener(InviteFriends);
        }

        private void OnDisable()
        {
            button.onClick.RemoveListener(InviteFriends);
        }

        public void InviteFriends()
        {
            if (SteamManager.Instance == null)
            {
                Debug.LogWarning("[SteamInviteFriendsButton] SteamManager is unavailable.");
                return;
            }

            if (createLobbyWhenMissing)
            {
                SteamManager.Instance.CreateJoinableLobbyAndOpenInvite();
                return;
            }

            SteamManager.Instance.OpenFriendOverlayForGameInvite();
        }
    }
}
