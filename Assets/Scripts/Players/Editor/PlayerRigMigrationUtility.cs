using SatelliteGameJam.Networking.Core;
using SatelliteGameJam.Networking.Messages;
using UnityEditor;
using UnityEngine;

namespace SatelliteGameJam.Players.Editor
{
    /// <summary>Converts an existing scene-local single-player object into a reusable local rig prefab.</summary>
    public static class PlayerRigMigrationUtility
    {
        private const string PrefabFolder = "Assets/Prefabs/Players";

        [MenuItem("Tools/Players/Migrate Selected Local Player To Prefab")]
        private static void MigrateSelectedPlayer()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null || !selected.scene.IsValid())
            {
                EditorUtility.DisplayDialog("Migrate Player Rig", "Select the root GameObject of a local gameplay player in an open scene.", "OK");
                return;
            }

            if (!AssetDatabase.IsValidFolder(PrefabFolder))
            {
                AssetDatabase.CreateFolder("Assets/Prefabs", "Players");
            }

            NetworkSceneId scene = ResolveScene(selected.scene.name);
            PlayerRole role = NetworkingConfiguration.Instance?.gameFlowDefinition?.ResolveDefaultRoleForScene(scene)
                ?? PlayerRole.None;
            if (scene == NetworkSceneId.None || role == PlayerRole.None)
            {
                EditorUtility.DisplayDialog("Migrate Player Rig", "The active scene must have a single default role in GameFlowDefinition.", "OK");
                return;
            }

            Undo.RecordObject(selected, "Prepare Player Rig");
            PlayerRig rig = selected.GetComponent<PlayerRig>() ?? Undo.AddComponent<PlayerRig>(selected);
            ConfigureContract(rig, role, scene);

            string path = EditorUtility.SaveFilePanelInProject(
                "Save Local Player Rig",
                $"{role}PlayerRig",
                "prefab",
                "Save the reusable local player rig prefab.",
                PrefabFolder);
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(selected, path, InteractionMode.UserAction);
            PlayerRig prefabRig = prefab != null ? prefab.GetComponent<PlayerRig>() : null;
            if (prefabRig == null)
            {
                Debug.LogError("[PlayerRigMigration] Unity did not create a PlayerRig prefab.");
                return;
            }

            AddOrUpdateCatalogEntry(prefabRig, role, scene);
            AssetDatabase.SaveAssets();
            Selection.activeObject = prefab;
            Debug.Log($"[PlayerRigMigration] Created {path} and registered {role}/{scene}.");
        }

        [MenuItem("Tools/Players/Migrate Selected Local Player To Prefab", true)]
        private static bool CanMigrateSelectedPlayer()
        {
            return Selection.activeGameObject != null && Selection.activeGameObject.scene.IsValid();
        }

        private static NetworkSceneId ResolveScene(string sceneName)
        {
            GameFlowDefinition flow = NetworkingConfiguration.Instance?.gameFlowDefinition;
            return flow != null && flow.TryGetSceneEntryByName(sceneName, out FlowSceneEntry entry)
                ? entry.sceneId
                : NetworkSceneId.None;
        }

        private static void ConfigureContract(PlayerRig rig, PlayerRole role, NetworkSceneId scene)
        {
            SerializedObject serializedRig = new SerializedObject(rig);
            serializedRig.FindProperty("role").enumValueIndex = (int)role;
            serializedRig.FindProperty("scene").enumValueIndex = (int)scene;
            serializedRig.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddOrUpdateCatalogEntry(PlayerRig prefab, PlayerRole role, NetworkSceneId scene)
        {
            PlayerRigCatalog catalog = NetworkingConfiguration.Instance?.playerRigCatalog;
            if (catalog == null)
            {
                Debug.LogWarning("[PlayerRigMigration] PlayerRigCatalog is not assigned in NetworkingConfig.");
                return;
            }

            SerializedObject serializedCatalog = new SerializedObject(catalog);
            SerializedProperty entries = serializedCatalog.FindProperty("entries");
            for (int index = 0; index < entries.arraySize; index++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(index);
                if (entry.FindPropertyRelative("role").enumValueIndex == (int)role &&
                    entry.FindPropertyRelative("scene").enumValueIndex == (int)scene)
                {
                    entry.FindPropertyRelative("localPlayerPrefab").objectReferenceValue = prefab;
                    serializedCatalog.ApplyModifiedProperties();
                    EditorUtility.SetDirty(catalog);
                    return;
                }
            }

            int newIndex = entries.arraySize;
            entries.InsertArrayElementAtIndex(newIndex);
            SerializedProperty newEntry = entries.GetArrayElementAtIndex(newIndex);
            newEntry.FindPropertyRelative("role").enumValueIndex = (int)role;
            newEntry.FindPropertyRelative("scene").enumValueIndex = (int)scene;
            newEntry.FindPropertyRelative("localPlayerPrefab").objectReferenceValue = prefab;
            serializedCatalog.ApplyModifiedProperties();
            EditorUtility.SetDirty(catalog);
        }
    }
}
