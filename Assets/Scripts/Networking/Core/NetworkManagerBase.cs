using UnityEngine;

namespace SatelliteGameJam.Networking.Core
{
    /// <summary>
    /// Base class for all singleton network managers that persist across scenes.
    /// Automatically handles singleton pattern and DontDestroyOnLoad.
    /// </summary>
    public abstract class NetworkManagerBase<T> : MonoBehaviour where T : NetworkManagerBase<T>
    {
        private static T instance;
        public static T Instance
        {
            get
            {
                if (instance == null && !applicationIsQuitting)
                {
                    instance = FindFirstObjectByType<T>();
                }
                return instance;
            }
        }

        private static bool applicationIsQuitting = false;

        protected virtual void Awake()
        {
            if (instance != null && instance != this)
            {
                Debug.LogWarning($"[{typeof(T).Name}] Duplicate instance detected. Destroying.");
                Destroy(gameObject);
                return;
            }

            instance = this as T;
            DontDestroyOnLoad(gameObject);
            
            OnAwakeAfterSingleton();
        }

        protected virtual void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
            
            OnDestroyBeforeNull();
        }

        private void OnApplicationQuit()
        {
            applicationIsQuitting = true;
        }

        /// <summary>
        /// Override this instead of Awake() for initialization logic.
        /// Called after singleton setup is complete.
        /// </summary>
        protected virtual void OnAwakeAfterSingleton() { }

        /// <summary>
        /// Override this instead of OnDestroy() for cleanup logic.
        /// Called before instance is nulled.
        /// </summary>
        protected virtual void OnDestroyBeforeNull() { }
    }
}