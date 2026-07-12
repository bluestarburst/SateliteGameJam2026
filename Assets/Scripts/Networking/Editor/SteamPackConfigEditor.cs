using SatelliteGameJam.Networking.Debugging;
using SatelliteGameJam.Networking.State;
using SatelliteGameJam.Networking.Sync;
using SatelliteGameJam.Networking.Voice;
using UnityEditor;
using UnityEngine;

namespace SatelliteGameJam.Networking.Core.Editor
{
    [CustomEditor(typeof(SteamPackConfig))]
    public class SteamPackConfigEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(10);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("SteamPack Tools", EditorStyles.boldLabel);

                if (GUILayout.Button("Load Defaults From Resources"))
                {
                    LoadDefaults((SteamPackConfig)target);
                }

                if (GUILayout.Button("Apply To Attached Managers"))
                {
                    ApplyToAttachedManagers((SteamPackConfig)target);
                }
            }

            DrawManagerStatus((SteamPackConfig)target);
        }

        private static void LoadDefaults(SteamPackConfig config)
        {
            SerializedObject serializedConfig = new SerializedObject(config);
            NetworkingConfiguration networkingConfiguration = Resources.Load<NetworkingConfiguration>("NetworkingConfig");
            RoleVisualProfile roleVisualProfile = Resources.Load<RoleVisualProfile>("RoleVisualProfile");

            SetObject(serializedConfig, "networkingConfiguration", networkingConfiguration);
            SetObject(serializedConfig, "roleVisualProfile", roleVisualProfile);

            serializedConfig.ApplyModifiedProperties();
            EditorUtility.SetDirty(config);
        }

        private static void ApplyToAttachedManagers(SteamPackConfig config)
        {
            Undo.RecordObject(config.gameObject, "Apply SteamPack Config");

            ApplyNetworkConnectionManager(config);
            ApplySceneSyncManager(config);
            ApplyVoiceManagers(config);
            ApplySceneFlowController(config);
            ApplyDebugOverlay(config);

            EditorUtility.SetDirty(config.gameObject);
            PrefabUtility.RecordPrefabInstancePropertyModifications(config.gameObject);
        }

        private static void ApplyNetworkConnectionManager(SteamPackConfig config)
        {
            NetworkConnectionManager manager = config.GetComponent<NetworkConnectionManager>();
            if (manager == null) return;

            SerializedObject so = new SerializedObject(manager);
            SetObject(so, "config", config.NetworkingConfiguration);
            SetObject(so, "roleVisualProfile", config.RoleVisualProfile);
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(manager);
        }

        private static void ApplySceneSyncManager(SteamPackConfig config)
        {
            SceneSyncManager manager = config.GetComponent<SceneSyncManager>();
            if (manager == null) return;

            SerializedObject so = new SerializedObject(manager);
            SetObject(so, "config", config.NetworkingConfiguration);
            SetObject(so, "sceneFlowController", config.GetComponent<SceneFlowController>());
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(manager);
        }

        private static void ApplyVoiceManagers(SteamPackConfig config)
        {
            SetConfig(config.GetComponent<VoiceSessionManager>(), config.NetworkingConfiguration);
            SetConfig(config.GetComponent<VoiceChatP2P>(), config.NetworkingConfiguration);
        }

        private static void ApplySceneFlowController(SteamPackConfig config)
        {
            SceneFlowController controller = config.GetComponent<SceneFlowController>();
            if (controller == null) return;

            SerializedObject so = new SerializedObject(controller);
            SetObject(so, "networkingConfiguration", config.NetworkingConfiguration);
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(controller);
        }

        private static void ApplyDebugOverlay(SteamPackConfig config)
        {
            SetConfig(config.GetComponent<NetworkDebugOverlay>(), config.NetworkingConfiguration);
        }

        private static void SetConfig(Object component, NetworkingConfiguration networkingConfiguration)
        {
            if (component == null) return;

            SerializedObject so = new SerializedObject(component);
            SetObject(so, "config", networkingConfiguration);
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(component);
        }

        private static void SetObject(SerializedObject serializedObject, string propertyName, Object value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.objectReferenceValue = value;
            }
        }

        private static void DrawManagerStatus(SteamPackConfig config)
        {
            EditorGUILayout.Space(10);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Attached Managers", EditorStyles.boldLabel);
                DrawStatus<SteamManager>(config);
                DrawStatus<NetworkConnectionManager>(config);
                DrawStatus<NetworkSyncManager>(config);
                DrawStatus<PlayerStateManager>(config);
                DrawStatus<SceneSyncManager>(config);
                DrawStatus<VoiceSessionManager>(config);
                DrawStatus<VoiceChatP2P>(config);
                DrawStatus<SceneFlowController>(config);
                DrawStatus<SceneAudioAnchorManager>(config);
                DrawStatus<NetworkDebugOverlay>(config);
            }
        }

        private static void DrawStatus<T>(SteamPackConfig config) where T : Component
        {
            T component = config.GetComponent<T>();
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField(typeof(T).Name, component, typeof(T), true);
            }
        }
    }
}
