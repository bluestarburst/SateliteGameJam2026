using System;
using System.Collections.Generic;
using SatelliteGameJam.Networking.Messages;
using UnityEngine;

namespace SatelliteGameJam.Players
{
    [Serializable]
    public sealed class PlayerRigEntry
    {
        public PlayerRole role = PlayerRole.None;
        public NetworkSceneId scene = NetworkSceneId.None;
        public PlayerRig localPlayerPrefab;
    }

    [CreateAssetMenu(fileName = "PlayerRigCatalog", menuName = "Players/Player Rig Catalog")]
    public sealed class PlayerRigCatalog : ScriptableObject
    {
        [SerializeField] private List<PlayerRigEntry> entries = new();

        public PlayerRig Resolve(PlayerRole role, NetworkSceneId scene)
        {
            PlayerRigEntry exact = entries.Find(entry => entry.role == role && entry.scene == scene);
            if (exact != null && exact.localPlayerPrefab != null)
            {
                return exact.localPlayerPrefab;
            }

            PlayerRigEntry roleDefault = entries.Find(entry =>
                entry.role == role && entry.scene == NetworkSceneId.None);
            return roleDefault?.localPlayerPrefab;
        }
    }
}
