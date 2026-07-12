using System.IO;
using SatelliteGameJam.Networking.Messages;
using UnityEditor;
using UnityEngine;

namespace SatelliteGameJam.Networking.Core.Editor
{
    [CustomEditor(typeof(GameFlowDefinition))]
    public class GameFlowDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            DrawFlowPreview((GameFlowDefinition)target);

            EditorGUILayout.Space(10);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Scene Flow Tools", EditorStyles.boldLabel);

                if (GUILayout.Button("Apply Default Jam Flow"))
                {
                    ApplyDefaultJamFlow(serializedObject);
                }

                if (GUILayout.Button("Add Known Scenes From Build Settings"))
                {
                    AddKnownBuildSettingsScenes(serializedObject);
                }

                if (GUILayout.Button("Validate Flow"))
                {
                    ((GameFlowDefinition)target).Validate();
                }
            }
        }

        private static void DrawFlowPreview(GameFlowDefinition definition)
        {
            EditorGUILayout.Space(10);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Resolved Flow", EditorStyles.boldLabel);

                foreach (FlowSceneEntry scene in definition.Scenes)
                {
                    PlayerRole role = definition.ResolveDefaultRoleForScene(scene.sceneId);
                    string roleLabel = role == PlayerRole.None ? "Role selected at runtime" : role.ToString();
                    EditorGUILayout.LabelField(scene.sceneId.ToString(), $"{scene.sceneName}  -  {roleLabel}");
                }

                if (definition.DevSession.enabled)
                {
                    EditorGUILayout.Space(3);
                    EditorGUILayout.LabelField("Development Join Assignment", EditorStyles.boldLabel);
                    DrawDevelopmentJoinPreview(definition, PlayerRole.GroundControl);
                    DrawDevelopmentJoinPreview(definition, PlayerRole.SpaceStation);
                }
            }
        }

        private static void DrawDevelopmentJoinPreview(GameFlowDefinition definition, PlayerRole localRole)
        {
            PlayerRole joiningRole = definition.ResolveDevelopmentJoinRole(localRole);
            NetworkSceneId joiningScene = definition.ResolveDevelopmentJoinScene(joiningRole);
            EditorGUILayout.LabelField(
                $"Host {localRole}",
                $"joiner: {joiningRole} -> {joiningScene}");
        }

        private static void ApplyDefaultJamFlow(SerializedObject serializedObject)
        {
            SerializedProperty scenes = serializedObject.FindProperty("scenes");
            EnsureScene(scenes, NetworkSceneId.Matchmaking, "Matchmaking", GameModeType.Matchmaking);
            EnsureScene(scenes, NetworkSceneId.Lobby, "Lobby", GameModeType.Lobby);
            EnsureScene(scenes, NetworkSceneId.GroundControl, "BaseStation", GameModeType.Gameplay, PlayerRole.GroundControl);
            EnsureScene(scenes, NetworkSceneId.SpaceStation, "Satellite", GameModeType.Gameplay, PlayerRole.SpaceStation);

            serializedObject.FindProperty("matchmakingScene").enumValueIndex = (int)NetworkSceneId.Matchmaking;
            serializedObject.FindProperty("lobbyScene").enumValueIndex = (int)NetworkSceneId.Lobby;
            serializedObject.ApplyModifiedProperties();
        }

        private static void AddKnownBuildSettingsScenes(SerializedObject serializedObject)
        {
            SerializedProperty scenes = serializedObject.FindProperty("scenes");

            foreach (EditorBuildSettingsScene buildScene in EditorBuildSettings.scenes)
            {
                if (!buildScene.enabled)
                {
                    continue;
                }

                string sceneName = Path.GetFileNameWithoutExtension(buildScene.path);
                NetworkSceneId sceneId = GuessSceneId(sceneName);
                if (sceneId == NetworkSceneId.None)
                {
                    continue;
                }

                EnsureScene(scenes, sceneId, sceneName, GuessMode(sceneName), GetDefaultRoles(sceneId));
            }

            serializedObject.ApplyModifiedProperties();
        }

        private static void EnsureScene(
            SerializedProperty scenes,
            NetworkSceneId sceneId,
            string sceneName,
            GameModeType mode,
            params PlayerRole[] allowedRoles)
        {
            SerializedProperty entry = FindScene(scenes, sceneId);
            if (entry == null)
            {
                scenes.arraySize++;
                entry = scenes.GetArrayElementAtIndex(scenes.arraySize - 1);
            }

            entry.FindPropertyRelative("sceneId").enumValueIndex = (int)sceneId;
            entry.FindPropertyRelative("sceneName").stringValue = sceneName;
            entry.FindPropertyRelative("modeType").enumValueIndex = (int)mode;

            SerializedProperty roles = entry.FindPropertyRelative("allowedRoles");
            roles.arraySize = allowedRoles?.Length ?? 0;
            for (int i = 0; i < roles.arraySize; i++)
            {
                roles.GetArrayElementAtIndex(i).enumValueIndex = (int)allowedRoles[i];
            }
        }

        private static SerializedProperty FindScene(SerializedProperty scenes, NetworkSceneId sceneId)
        {
            for (int i = 0; i < scenes.arraySize; i++)
            {
                SerializedProperty entry = scenes.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("sceneId").enumValueIndex == (int)sceneId)
                {
                    return entry;
                }
            }

            return null;
        }

        private static NetworkSceneId GuessSceneId(string sceneName)
        {
            if (sceneName == "Matchmaking") return NetworkSceneId.Matchmaking;
            if (sceneName == "Lobby") return NetworkSceneId.Lobby;
            if (sceneName == "BaseStation" || sceneName == "GroundControl") return NetworkSceneId.GroundControl;
            if (sceneName == "Satellite" || sceneName == "SpaceStation") return NetworkSceneId.SpaceStation;
            return NetworkSceneId.None;
        }

        private static GameModeType GuessMode(string sceneName)
        {
            if (sceneName == "Matchmaking") return GameModeType.Matchmaking;
            if (sceneName == "Lobby") return GameModeType.Lobby;
            return GameModeType.Gameplay;
        }

        private static PlayerRole[] GetDefaultRoles(NetworkSceneId sceneId)
        {
            switch (sceneId)
            {
                case NetworkSceneId.GroundControl:
                    return new[] { PlayerRole.GroundControl };
                case NetworkSceneId.SpaceStation:
                    return new[] { PlayerRole.SpaceStation };
                default:
                    return System.Array.Empty<PlayerRole>();
            }
        }
    }
}
