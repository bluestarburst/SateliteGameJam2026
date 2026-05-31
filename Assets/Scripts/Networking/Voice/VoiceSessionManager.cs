using System.Collections.Generic;
using SatelliteGameJam.Networking.Core;
using SatelliteGameJam.Networking.Messages;
using SatelliteGameJam.Networking.State;
using Steamworks;
using UnityEngine;

namespace SatelliteGameJam.Networking.Voice
{
    /// <summary>
    /// Authoritative player-to-audio mapping for incoming remote voice playback.
    /// </summary>
    public class VoiceSessionManager : MonoBehaviour
    {
        private class VoiceBinding
        {
            public GameObject HostObject;
            public GameObject Avatar;
            public VoiceRemotePlayer VoicePlayer;
            public bool IsProxy;
        }

        public static VoiceSessionManager Instance { get; private set; }

        [Header("Voice Rules")]
        [SerializeField] private NetworkingConfiguration config;
        [SerializeField] private float fallbackSpaceProximityRadius = 20f;
        [SerializeField] private bool enableDebugLogs;

        private readonly HashSet<SteamId> playersAtConsole = new HashSet<SteamId>();
        private readonly Dictionary<SteamId, VoiceBinding> bindings = new Dictionary<SteamId, VoiceBinding>();

        private bool isLocalPlayerAtConsole;
        private bool consoleHandlerRegistered;

        public bool IsLocalPlayerAtConsole => isLocalPlayerAtConsole;
        private float SpaceProximityRadius => config != null ? config.proximityVoiceDistance : fallbackSpaceProximityRadius;
        private bool VoiceEnabled => config == null || config.voiceChatEnabled;
        private bool UseRoleGating => config == null || config.useRoleBasedVoiceGating;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            RegisterConsoleHandlerWhenReady();

            if (PlayerStateManager.Instance != null)
            {
                PlayerStateManager.Instance.OnPlayerLeft += OnPlayerLeft;
                PlayerStateManager.Instance.OnPlayerSceneChanged += OnPlayerSceneChanged;
            }
        }

        private void RegisterConsoleHandlerWhenReady()
        {
            if (consoleHandlerRegistered)
            {
                return;
            }

            if (NetworkConnectionManager.Instance == null)
            {
                Invoke(nameof(RegisterConsoleHandlerWhenReady), 0.5f);
                return;
            }

            NetworkConnectionManager.Instance.RegisterHandler(NetworkMessageType.ConsoleInteraction, OnReceiveConsoleInteraction);
            consoleHandlerRegistered = true;
        }

        private void OnDestroy()
        {
            CancelInvoke(nameof(RegisterConsoleHandlerWhenReady));

            if (NetworkConnectionManager.Instance != null && consoleHandlerRegistered)
            {
                NetworkConnectionManager.Instance.UnregisterHandler(NetworkMessageType.ConsoleInteraction, OnReceiveConsoleInteraction);
            }

            if (PlayerStateManager.Instance != null)
            {
                PlayerStateManager.Instance.OnPlayerLeft -= OnPlayerLeft;
                PlayerStateManager.Instance.OnPlayerSceneChanged -= OnPlayerSceneChanged;
            }
        }

        private void Update()
        {
            ApplyVoiceGating();
        }

        public void RegisterRemotePlayerAvatar(SteamId steamId, GameObject avatar)
        {
            if (avatar == null)
            {
                Debug.LogWarning($"[VoiceSessionManager] Tried to register null avatar for {steamId}");
                return;
            }

            VoiceBinding binding = GetOrCreateBinding(steamId);
            binding.Avatar = avatar;

            VoiceRemotePlayer avatarPlayer = avatar.GetComponent<VoiceRemotePlayer>();
            if (avatarPlayer == null)
            {
                avatarPlayer = avatar.AddComponent<VoiceRemotePlayer>();
                avatarPlayer.Initialize(steamId);
            }

            if (binding.IsProxy)
            {
                // Migrate from fallback proxy to avatar-bound playback.
                if (binding.HostObject != null)
                {
                    Destroy(binding.HostObject);
                }

                binding.HostObject = avatar;
                binding.IsProxy = false;
                binding.VoicePlayer = avatarPlayer;
            }
            else
            {
                binding.HostObject = avatar;
                binding.VoicePlayer = avatarPlayer;
            }

            ApplyAnchor(steamId, binding);

            if (enableDebugLogs)
            {
                Debug.Log($"[VoiceSessionManager] Registered avatar voice binding for {steamId}");
            }
        }

        public void UnregisterRemotePlayer(SteamId steamId)
        {
            if (!bindings.TryGetValue(steamId, out VoiceBinding binding))
            {
                return;
            }

            if (binding.IsProxy && binding.HostObject != null)
            {
                Destroy(binding.HostObject);
            }
            else if (!binding.IsProxy && binding.VoicePlayer != null && binding.Avatar != null)
            {
                Destroy(binding.VoicePlayer);
            }

            bindings.Remove(steamId);
            playersAtConsole.Remove(steamId);

            if (enableDebugLogs)
            {
                Debug.Log($"[VoiceSessionManager] Unregistered player {steamId}");
            }
        }

        public VoiceRemotePlayer GetOrCreateVoiceRemotePlayer(SteamId steamId)
        {
            VoiceBinding binding = GetOrCreateBinding(steamId);
            return binding.VoicePlayer;
        }

        public void SetLocalPlayerAtConsole(bool atConsole)
        {
            isLocalPlayerAtConsole = atConsole;
            BroadcastConsoleInteraction(atConsole);

            foreach (KeyValuePair<SteamId, VoiceBinding> kvp in bindings)
            {
                ApplyAnchor(kvp.Key, kvp.Value);
            }
        }

        public void SetRemotePlayerAtConsole(SteamId steamId, bool atConsole)
        {
            if (atConsole)
            {
                playersAtConsole.Add(steamId);
            }
            else
            {
                playersAtConsole.Remove(steamId);
            }
        }

        public bool IsWithinProximityForSending(SteamId remoteSteamId)
        {
            return IsWithinProximity(remoteSteamId);
        }

        private VoiceBinding GetOrCreateBinding(SteamId steamId)
        {
            if (bindings.TryGetValue(steamId, out VoiceBinding existing))
            {
                if (existing.VoicePlayer == null)
                {
                    existing.VoicePlayer = existing.HostObject != null
                        ? existing.HostObject.GetComponent<VoiceRemotePlayer>()
                        : null;
                }

                return existing;
            }

            var proxy = new GameObject($"VoiceProxy_{steamId}");
            DontDestroyOnLoad(proxy);

            var voicePlayer = proxy.AddComponent<VoiceRemotePlayer>();
            voicePlayer.Initialize(steamId);

            var binding = new VoiceBinding
            {
                HostObject = proxy,
                Avatar = null,
                VoicePlayer = voicePlayer,
                IsProxy = true
            };

            bindings[steamId] = binding;
            ApplyAnchor(steamId, binding);
            return binding;
        }

        private void ApplyVoiceGating()
        {
            if (!VoiceEnabled || PlayerStateManager.Instance == null || SteamManager.Instance == null)
            {
                return;
            }

            SteamId localId = SteamManager.Instance.PlayerSteamId;
            PlayerState localState = PlayerStateManager.Instance.GetPlayerState(localId);

            foreach (KeyValuePair<SteamId, VoiceBinding> kvp in bindings)
            {
                SteamId remoteSteamId = kvp.Key;
                VoiceBinding binding = kvp.Value;
                if (binding == null || binding.VoicePlayer == null)
                {
                    continue;
                }

                PlayerState remoteState = PlayerStateManager.Instance.GetPlayerState(remoteSteamId);
                bool shouldHear = !UseRoleGating || localState.Scene == NetworkSceneId.Lobby || ShouldHearPlayer(localState, remoteState, remoteSteamId);
                AudioSource source = binding.VoicePlayer.GetComponent<AudioSource>();
                if (source != null)
                {
                    source.enabled = shouldHear;
                }
            }
        }

        private bool ShouldHearPlayer(PlayerState localState, PlayerState remoteState, SteamId remoteSteamId)
        {
            if (localState.Role == PlayerRole.Lobby)
            {
                return true;
            }

            if (localState.Role == PlayerRole.GroundControl)
            {
                if (remoteState.Role == PlayerRole.GroundControl)
                {
                    return true;
                }

                if (remoteState.Role == PlayerRole.SpaceStation)
                {
                    return isLocalPlayerAtConsole;
                }

                return false;
            }

            if (localState.Role == PlayerRole.SpaceStation)
            {
                if (remoteState.Role == PlayerRole.GroundControl)
                {
                    return true;
                }

                if (remoteState.Role == PlayerRole.SpaceStation)
                {
                    return IsWithinProximity(remoteSteamId);
                }

                return false;
            }

            return false;
        }

        private bool IsWithinProximity(SteamId remoteSteamId)
        {
            if (!bindings.TryGetValue(remoteSteamId, out VoiceBinding binding) || binding.Avatar == null)
            {
                return false;
            }

            GameObject localAvatar = FindLocalPlayerAvatar();
            if (localAvatar == null)
            {
                return true;
            }

            float distance = Vector3.Distance(localAvatar.transform.position, binding.Avatar.transform.position);
            return distance <= SpaceProximityRadius;
        }

        private GameObject FindLocalPlayerAvatar()
        {
            if (SteamManager.Instance == null)
            {
                return null;
            }

            var allIdentities = FindObjectsByType<SatelliteGameJam.Networking.Identity.NetworkIdentity>(FindObjectsSortMode.None);
            foreach (var identity in allIdentities)
            {
                if (identity.OwnerSteamId == SteamManager.Instance.PlayerSteamId)
                {
                    return identity.gameObject;
                }
            }

            return null;
        }

        private void BroadcastConsoleInteraction(bool atConsole)
        {
            if (NetworkConnectionManager.Instance == null || SteamManager.Instance == null)
            {
                return;
            }

            byte[] packet = new byte[10];
            int offset = 0;
            packet[offset++] = (byte)NetworkMessageType.ConsoleInteraction;
            NetworkSerialization.WriteULong(packet, ref offset, SteamManager.Instance.PlayerSteamId);
            packet[offset] = (byte)(atConsole ? 1 : 0);
            NetworkConnectionManager.Instance.SendToAll(packet, 0, P2PSend.Reliable);
        }

        private void OnReceiveConsoleInteraction(SteamId sender, byte[] data)
        {
            if (data == null || data.Length < 10)
            {
                return;
            }

            int offset = 1;
            SteamId playerSteamId = NetworkSerialization.ReadULong(data, ref offset);
            bool atConsole = data[offset] == 1;
            SetRemotePlayerAtConsole(playerSteamId, atConsole);
        }

        private void OnPlayerLeft(SteamId steamId)
        {
            UnregisterRemotePlayer(steamId);
        }

        private void OnPlayerSceneChanged(SteamId steamId, NetworkSceneId sceneId)
        {
            if (bindings.TryGetValue(steamId, out VoiceBinding binding))
            {
                ApplyAnchor(steamId, binding);
            }
        }

        private void ApplyAnchor(SteamId steamId, VoiceBinding binding)
        {
            if (binding == null || binding.VoicePlayer == null)
            {
                return;
            }

            AudioSource source = binding.VoicePlayer.GetComponent<AudioSource>();
            if (source == null)
            {
                return;
            }

            if (SceneAudioAnchorManager.Instance != null)
            {
                SceneAudioAnchorManager.Instance.ApplyAnchor(
                    steamId,
                    binding.VoicePlayer.gameObject,
                    source,
                    binding.Avatar,
                    isLocalPlayerAtConsole);
                return;
            }

            // Safe fallback when no SceneAudioAnchorManager is present:
            // proxy-only lobby players should be audible without requiring
            // world-space avatar anchors.
            if (binding.Avatar != null)
            {
                binding.VoicePlayer.transform.SetParent(binding.Avatar.transform, false);
                source.spatialBlend = 1f;
            }
            else
            {
                binding.VoicePlayer.transform.SetParent(null, false);
                source.spatialBlend = 0f;
            }
        }
    }
}
