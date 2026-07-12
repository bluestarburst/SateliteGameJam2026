using System;
using System.Collections.Generic;
using SatelliteGameJam.Networking.Messages;
using SatelliteGameJam.Networking.State;
using Steamworks;
using UnityEngine;

namespace SatelliteGameJam.Networking.Voice
{
    public enum VoiceAnchorMode
    {
        FollowPlayerAvatar = 0,
        FixedAnchor = 1,
        NonSpatial = 2
    }

    [Serializable]
    public class VoiceAnchorRule
    {
        public NetworkSceneId localScene = NetworkSceneId.None;
        public PlayerRole remoteRole = PlayerRole.None;
        public VoiceAnchorMode anchorMode = VoiceAnchorMode.FollowPlayerAvatar;
        [Tooltip("Name of a scene object used when FixedAnchor is selected.")]
        public string anchorObjectName = string.Empty;
        [Range(0f, 1f)] public float spatialBlend = 1f;
    }

    /// <summary>
    /// Scene-aware audio anchor resolver used by VoiceSessionManager.
    /// </summary>
    public class SceneAudioAnchorManager : MonoBehaviour
    {
        public static SceneAudioAnchorManager Instance { get; private set; }

        [SerializeField] private List<VoiceAnchorRule> rules = new List<VoiceAnchorRule>();
        [SerializeField] private bool verboseLogging;

        private readonly Dictionary<string, Transform> anchorCache = new Dictionary<string, Transform>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void ApplyAnchor(SteamId remoteId, GameObject hostObject, AudioSource source, GameObject avatarFallback, bool atConsole)
        {
            if (hostObject == null || source == null || PlayerStateManager.Instance == null || SteamManager.Instance == null)
            {
                return;
            }

            PlayerState localState = PlayerStateManager.Instance.GetPlayerState(SteamManager.Instance.PlayerSteamId);
            PlayerState remoteState = PlayerStateManager.Instance.GetPlayerState(remoteId);
            VoiceAnchorRule rule = ResolveRule(localState.Scene, remoteState.Role, atConsole);
            if (rule == null)
            {
                // Default behavior: follow avatar for in-world players; otherwise
                // keep proxy playback non-spatial so lobby audio remains audible.
                if (avatarFallback != null)
                {
                    hostObject.transform.SetParent(avatarFallback.transform, false);
                    hostObject.transform.localPosition = Vector3.zero;
                    hostObject.transform.localRotation = Quaternion.identity;
                    source.spatialBlend = 1f;
                }
                else
                {
                    hostObject.transform.SetParent(null, false);
                    source.spatialBlend = 0f;
                }
                return;
            }

            source.spatialBlend = rule.spatialBlend;

            switch (rule.anchorMode)
            {
                case VoiceAnchorMode.NonSpatial:
                    hostObject.transform.SetParent(null, false);
                    source.spatialBlend = 0f;
                    break;
                case VoiceAnchorMode.FixedAnchor:
                {
                    Transform anchor = ResolveAnchor(rule.anchorObjectName);
                    if (anchor != null)
                    {
                        hostObject.transform.SetParent(anchor, false);
                        hostObject.transform.localPosition = Vector3.zero;
                        hostObject.transform.localRotation = Quaternion.identity;
                    }
                    break;
                }
                case VoiceAnchorMode.FollowPlayerAvatar:
                default:
                    if (avatarFallback != null)
                    {
                        hostObject.transform.SetParent(avatarFallback.transform, false);
                        hostObject.transform.localPosition = Vector3.zero;
                        hostObject.transform.localRotation = Quaternion.identity;
                    }
                    break;
            }

            if (verboseLogging)
            {
                Debug.Log($"[SceneAudioAnchorManager] Applied {rule.anchorMode} for {remoteId} in {localState.Scene}");
            }
        }

        private VoiceAnchorRule ResolveRule(NetworkSceneId localScene, PlayerRole remoteRole, bool atConsole)
        {
            // Prefer exact scene + role
            VoiceAnchorRule exact = rules.Find(r => r.localScene == localScene && r.remoteRole == remoteRole);
            if (exact != null)
            {
                return exact;
            }

            // Optional policy: if at console and no exact mapping for GroundControl->Space,
            // allow fallback by scene with remoteRole wildcard (None).
            if (atConsole)
            {
                VoiceAnchorRule sceneWildcard = rules.Find(r => r.localScene == localScene && r.remoteRole == PlayerRole.None);
                if (sceneWildcard != null)
                {
                    return sceneWildcard;
                }
            }

            return rules.Find(r => r.localScene == NetworkSceneId.None && r.remoteRole == remoteRole);
        }

        private Transform ResolveAnchor(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return null;
            }

            if (anchorCache.TryGetValue(objectName, out Transform cached) && cached != null)
            {
                return cached;
            }

            GameObject go = GameObject.Find(objectName);
            if (go == null)
            {
                return null;
            }

            anchorCache[objectName] = go.transform;
            return go.transform;
        }
    }
}
