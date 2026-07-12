using System;
using System.Collections.Generic;
using SatelliteGameJam.Networking.Messages;
using UnityEngine;

namespace SatelliteGameJam.Networking.Core
{
    [Serializable]
    public class RoleVisualEntry
    {
        public PlayerRole role = PlayerRole.None;
        public NetworkSceneId scene = NetworkSceneId.None;
        public GameObject visualPrefab;
    }

    [CreateAssetMenu(fileName = "RoleVisualProfile", menuName = "Networking/Role Visual Profile", order = 3)]
    public class RoleVisualProfile : ScriptableObject
    {
        [SerializeField] private List<RoleVisualEntry> entries = new List<RoleVisualEntry>();

        public GameObject Resolve(PlayerRole role, NetworkSceneId scene)
        {
            RoleVisualEntry exact = entries.Find(e => e.role == role && e.scene == scene);
            if (exact != null && exact.visualPrefab != null)
            {
                return exact.visualPrefab;
            }

            RoleVisualEntry roleDefault = entries.Find(e => e.role == role && e.scene == NetworkSceneId.None);
            return roleDefault != null ? roleDefault.visualPrefab : null;
        }
    }
}
