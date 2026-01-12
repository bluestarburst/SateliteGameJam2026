using System.Collections.Generic;
using Steamworks;
using UnityEngine;
using SatelliteGameJam.Networking.Core;
using SatelliteGameJam.Networking.State;
using SatelliteGameJam.Networking.Identity;
using UnityEngine.InputSystem;

namespace SatelliteGameJam.Networking.Debugging
{
    /// <summary>
    /// Real-time networking debug overlay shown in-game.
    /// Toggle with Tab key or via NetworkingConfiguration.
    /// Shows connection status, player states, packet statistics, and scene info.
    /// Refs: DeveloperExperienceImprovements.md Part 3
    /// </summary>
    public class NetworkDebugOverlay : MonoBehaviour
    {
        public static NetworkDebugOverlay Instance { get; private set; }

        [SerializeField] NetworkingConfiguration config;

        [SerializeField] private bool startEnabled = true;
        [SerializeField] private Key toggleKey = Key.Tab;
        
        private bool isVisible = true;
        private Rect windowRect = new Rect(10, 10, 450, 650);

        // Packet statistics tracking
        private int packetsSentThisSecond = 0;
        private int packetsReceivedThisSecond = 0;
        private Queue<int> packetsSentHistory = new Queue<int>(60);
        private Queue<int> packetsReceivedHistory = new Queue<int>(60);
        private float statisticsTimer = 0f;

        // Byte statistics
        private int bytesSentThisSecond = 0;
        private int bytesReceivedThisSecond = 0;
        private Queue<int> bytesSentHistory = new Queue<int>(60);
        private Queue<int> bytesReceivedHistory = new Queue<int>(60);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            isVisible = startEnabled;
        }

        private void Update()
        {
            // Toggle overlay with configured key
            if (Keyboard.current[toggleKey].wasPressedThisFrame)
            {
                ToggleOverlay();
            }

            // Update statistics every second
            statisticsTimer += Time.deltaTime;
            if (statisticsTimer >= 1f)
            {
                statisticsTimer = 0f;

                // Record packet statistics
                if (packetsSentHistory.Count >= 60)
                    packetsSentHistory.Dequeue();
                packetsSentHistory.Enqueue(packetsSentThisSecond);
                packetsSentThisSecond = 0;

                if (packetsReceivedHistory.Count >= 60)
                    packetsReceivedHistory.Dequeue();
                packetsReceivedHistory.Enqueue(packetsReceivedThisSecond);
                packetsReceivedThisSecond = 0;

                // Record byte statistics
                if (bytesSentHistory.Count >= 60)
                    bytesSentHistory.Dequeue();
                bytesSentHistory.Enqueue(bytesSentThisSecond);
                bytesSentThisSecond = 0;

                if (bytesReceivedHistory.Count >= 60)
                    bytesReceivedHistory.Dequeue();
                bytesReceivedHistory.Enqueue(bytesReceivedThisSecond);
                bytesReceivedThisSecond = 0;
            }
        }

        private void OnGUI()
        {
            if (!isVisible) return;

            var config = NetworkingConfiguration.Instance;
            if (config != null && !config.showNetworkDebugOverlay) return;

            GUI.skin.box.alignment = TextAnchor.UpperLeft;
            GUI.skin.label.fontSize = 12;
            GUI.skin.window.padding = new RectOffset(10, 10, 20, 10);

            windowRect = GUILayout.Window(99999, windowRect, DrawDebugWindow, "Network Debug Overlay");
        }

        private void DrawDebugWindow(int windowID)
        {
            GUILayout.BeginVertical();

            DrawConnectionStatus();
            GUILayout.Space(8);

            DrawLocalPlayerInfo();
            GUILayout.Space(8);

            DrawRemotePlayersInfo();
            GUILayout.Space(8);

            DrawPacketStatistics();
            GUILayout.Space(8);

            DrawSceneInfo();
            GUILayout.Space(8);

            DrawNetworkObjects();

            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }

        private void DrawConnectionStatus()
        {
            GUILayout.Label("<b><color=yellow>CONNECTION STATUS</color></b>", CreateRichTextStyle());

            var steamMgr = SteamManager.Instance;
            if (steamMgr == null)
            {
                GUI.color = Color.red;
                GUILayout.Label("SteamManager: NOT INITIALIZED");
                GUI.color = Color.white;
                return;
            }

            bool isConnected = steamMgr.currentLobby.Id.Value != 0;
            string status = isConnected ? "CONNECTED" : "DISCONNECTED";
            Color statusColor = isConnected ? Color.green : Color.red;

            GUI.color = statusColor;
            GUILayout.Label($"Status: {status}");
            GUI.color = Color.white;

            GUILayout.Label($"Local SteamId: {steamMgr.PlayerSteamId.Value}");
            GUILayout.Label($"Local Name: {steamMgr.PlayerName}");
            GUILayout.Label($"Lobby ID: {steamMgr.currentLobby.Id.Value}");
            GUILayout.Label($"Lobby Size: {steamMgr.currentLobby.MemberCount}");

            if (steamMgr.currentLobby.Owner.Id == steamMgr.PlayerSteamId)
            {
                GUI.color = Color.cyan;
                GUILayout.Label("Role: HOST");
                GUI.color = Color.white;
            }
        }

        private void DrawLocalPlayerInfo()
        {
            GUILayout.Label("<b><color=yellow>LOCAL PLAYER</color></b>", CreateRichTextStyle());

            var playerMgr = PlayerStateManager.Instance;
            var steamMgr = SteamManager.Instance;

            if (playerMgr != null && steamMgr != null)
            {
                var localState = playerMgr.GetPlayerState(steamMgr.PlayerSteamId);
                GUILayout.Label($"Scene: <color=lime>{localState.Scene}</color>", CreateRichTextStyle());
                GUILayout.Label($"Role: <color=lime>{localState.Role}</color>", CreateRichTextStyle());
                GUILayout.Label($"Ready: {(localState.IsReady ? "<color=lime>YES</color>" : "<color=red>NO</color>")}", CreateRichTextStyle());
            }
            else
            {
                GUI.color = Color.red;
                GUILayout.Label("PlayerStateManager: NOT AVAILABLE");
                GUI.color = Color.white;
            }
        }

        private void DrawRemotePlayersInfo()
        {
            GUILayout.Label("<b><color=yellow>REMOTE PLAYERS</color></b>", CreateRichTextStyle());

            var playerMgr = PlayerStateManager.Instance;
            var steamMgr = SteamManager.Instance;

            if (playerMgr == null || steamMgr == null)
            {
                GUILayout.Label("No player data available");
                return;
            }

            int remoteCount = 0;
            foreach (var member in steamMgr.currentLobby.Members)
            {
                if (member.Id == steamMgr.PlayerSteamId) continue;

                remoteCount++;
                var state = playerMgr.GetPlayerState(member.Id);
                
                string playerInfo = $"<color=cyan>{member.Name}</color> (ID: {member.Id.Value})\n" +
                    $"  Scene: <color=lime>{state.Scene}</color> | Role: <color=lime>{state.Role}</color> | " +
                    $"Ready: {(state.IsReady ? "<color=lime>YES</color>" : "<color=red>NO</color>")}";

                GUILayout.Label(playerInfo, CreateRichTextStyle());
            }

            if (remoteCount == 0)
            {
                GUILayout.Label("<color=gray>No remote players</color>", CreateRichTextStyle());
            }
        }

        private void DrawPacketStatistics()
        {
            GUILayout.Label("<b><color=yellow>PACKET STATISTICS</color></b>", CreateRichTextStyle());

            var config = NetworkingConfiguration.Instance;
            if (config != null && !config.showPacketStatistics)
            {
                GUILayout.Label("<color=gray>Packet statistics disabled in config</color>", CreateRichTextStyle());
                return;
            }

            // Calculate averages
            int avgSent = 0, avgReceived = 0;
            int avgBytesSent = 0, avgBytesReceived = 0;

            foreach (var count in packetsSentHistory) avgSent += count;
            foreach (var count in packetsReceivedHistory) avgReceived += count;
            foreach (var bytes in bytesSentHistory) avgBytesSent += bytes;
            foreach (var bytes in bytesReceivedHistory) avgBytesReceived += bytes;

            int historyCount = Mathf.Max(packetsSentHistory.Count, 1);
            avgSent /= historyCount;
            avgReceived /= historyCount;
            avgBytesSent /= historyCount;
            avgBytesReceived /= historyCount;

            GUILayout.Label($"Packets Sent/sec (avg): <color=lime>{avgSent}</color>", CreateRichTextStyle());
            GUILayout.Label($"Packets Received/sec (avg): <color=lime>{avgReceived}</color>", CreateRichTextStyle());
            GUILayout.Label($"Bytes Sent/sec (avg): <color=lime>{FormatBytes(avgBytesSent)}</color>", CreateRichTextStyle());
            GUILayout.Label($"Bytes Received/sec (avg): <color=lime>{FormatBytes(avgBytesReceived)}</color>", CreateRichTextStyle());

            // Calculate totals
            int totalSent = 0, totalReceived = 0;
            int totalBytesSent = 0, totalBytesReceived = 0;
            foreach (var count in packetsSentHistory) totalSent += count;
            foreach (var count in packetsReceivedHistory) totalReceived += count;
            foreach (var bytes in bytesSentHistory) totalBytesSent += bytes;
            foreach (var bytes in bytesReceivedHistory) totalBytesReceived += bytes;

            GUILayout.Label($"Total Sent (last 60s): {totalSent} packets ({FormatBytes(totalBytesSent)})");
            GUILayout.Label($"Total Received (last 60s): {totalReceived} packets ({FormatBytes(totalBytesReceived)})");
        }

        private void DrawSceneInfo()
        {
            GUILayout.Label("<b><color=yellow>SCENE INFO</color></b>", CreateRichTextStyle());

            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            GUILayout.Label($"Active Scene: <color=lime>{activeScene.name}</color>", CreateRichTextStyle());
            GUILayout.Label($"Scene Build Index: {activeScene.buildIndex}");
            GUILayout.Label($"Scene Path: {activeScene.path}");
        }

        private void DrawNetworkObjects()
        {
            GUILayout.Label("<b><color=yellow>NETWORK OBJECTS</color></b>", CreateRichTextStyle());

            var allIdentities = FindObjectsOfType<NetworkIdentity>();
            GUILayout.Label($"Total Network Objects: <color=lime>{allIdentities.Length}</color>", CreateRichTextStyle());

            if (allIdentities.Length > 0)
            {
                int localOwned = 0;
                int remoteOwned = 0;

                foreach (var identity in allIdentities)
                {
                    if (SteamManager.Instance != null && identity.OwnerSteamId == SteamManager.Instance.PlayerSteamId)
                        localOwned++;
                    else
                        remoteOwned++;
                }

                GUILayout.Label($"  Local Owned: <color=cyan>{localOwned}</color>", CreateRichTextStyle());
                GUILayout.Label($"  Remote Owned: <color=orange>{remoteOwned}</color>", CreateRichTextStyle());
            }
        }

        private GUIStyle CreateRichTextStyle()
        {
            return new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true };
        }

        private string FormatBytes(int bytes)
        {
            if (bytes < 1024)
                return $"{bytes} B";
            else if (bytes < 1024 * 1024)
                return $"{bytes / 1024f:F1} KB";
            else
                return $"{bytes / (1024f * 1024f):F2} MB";
        }

        // ===== PUBLIC API =====

        /// <summary>
        /// Toggle the debug overlay visibility.
        /// </summary>
        public void ToggleOverlay()
        {
            isVisible = !isVisible;
            Debug.Log($"[NetworkDebugOverlay] Overlay {(isVisible ? "enabled" : "disabled")}");
        }

        /// <summary>
        /// Show the debug overlay.
        /// </summary>
        public void ShowOverlay()
        {
            isVisible = true;
        }

        /// <summary>
        /// Hide the debug overlay.
        /// </summary>
        public void HideOverlay()
        {
            isVisible = false;
        }

        /// <summary>
        /// Called by NetworkConnectionManager when a packet is sent.
        /// </summary>
        public void RecordPacketSent(int byteCount = 0)
        {
            packetsSentThisSecond++;
            bytesSentThisSecond += byteCount;
        }

        /// <summary>
        /// Called by NetworkConnectionManager when a packet is received.
        /// </summary>
        public void RecordPacketReceived(int byteCount = 0)
        {
            packetsReceivedThisSecond++;
            bytesReceivedThisSecond += byteCount;
        }
    }
}
