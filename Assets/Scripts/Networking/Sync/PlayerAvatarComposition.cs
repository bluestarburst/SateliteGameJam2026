using UnityEngine;

namespace SatelliteGameJam.Networking.Sync
{
    /// <summary>
    /// Keeps a clean separation between network/player core and role visuals.
    /// </summary>
    public class PlayerAvatarComposition : MonoBehaviour
    {
        [SerializeField] private Transform visualAnchor;
        [SerializeField] private GameObject activeVisual;

        public Transform VisualAnchor
        {
            get
            {
                if (visualAnchor == null)
                {
                    EnsureVisualAnchor();
                }

                return visualAnchor;
            }
        }

        public void ApplyVisual(GameObject visualPrefab)
        {
            if (visualPrefab == null)
            {
                return;
            }

            if (activeVisual != null)
            {
                Destroy(activeVisual);
            }

            activeVisual = Instantiate(visualPrefab, VisualAnchor);
            activeVisual.name = visualPrefab.name;
            activeVisual.transform.localPosition = Vector3.zero;
            activeVisual.transform.localRotation = Quaternion.identity;
            activeVisual.transform.localScale = Vector3.one;
        }

        private void EnsureVisualAnchor()
        {
            Transform existing = transform.Find("VisualAnchor");
            if (existing != null)
            {
                visualAnchor = existing;
                return;
            }

            var anchorGo = new GameObject("VisualAnchor");
            anchorGo.transform.SetParent(transform, false);
            visualAnchor = anchorGo.transform;
        }
    }
}
