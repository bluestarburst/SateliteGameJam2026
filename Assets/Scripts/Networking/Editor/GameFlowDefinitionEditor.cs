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
            }
        }

        private static void ApplyDefaultJamFlow(SerializedObject serializedObject)
        {
            SerializedProperty scenes = serializedObject.FindProperty("scenes");
            EnsureScene(scenes, NetworkSceneId.Matchmaking, "Matchmaking", GameModeType.Matchmaking);
            EnsureScene(scenes, NetworkSceneId.Lobby, "Lobby", GameModeType.Lobby);
            EnsureScene(scenes, NetworkSceneId.GroundControl, "BaseStation", GameModeType.Gameplay, PlayerRole.GroundControl);
            EnsureScene(scenes, NetworkSceneId.SpaceStation, "Satellite", GameModeType.Gameplay, PlayerRole.SpaceStation);

            SerializedProperty rules = serializedObject.FindProperty("roleSceneRules");
            EnsureRoleRule(rules, PlayerRole.GroundControl, NetworkSceneId.GroundControl);
            EnsureRoleRule(rules, PlayerRole.SpaceStation, NetworkSceneId.SpaceStation);

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

                EnsureScene(scenes, sceneId, sceneName, GuessMode(sceneName));
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

        private static void EnsureRoleRule(SerializedProperty rules, PlayerRole role, NetworkSceneId targetScene)
        {
            SerializedProperty rule = FindRoleRule(rules, role);
            if (rule == null)
            {
                rules.arraySize++;
                rule = rules.GetArrayElementAtIndex(rules.arraySize - 1);
            }

            rule.FindPropertyRelative("role").enumValueIndex = (int)role;
            rule.FindPropertyRelative("targetScene").enumValueIndex = (int)targetScene;
        }

        private static SerializedProperty FindRoleRule(SerializedProperty rules, PlayerRole role)
        {
            for (int i = 0; i < rules.arraySize; i++)
            {
                SerializedProperty rule = rules.GetArrayElementAtIndex(i);
                if (rule.FindPropertyRelative("role").enumValueIndex == (int)role)
                {
                    return rule;
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
    }
}
