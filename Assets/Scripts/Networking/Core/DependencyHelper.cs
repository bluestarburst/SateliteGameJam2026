using System;
using System.Collections;
using UnityEngine;

namespace SatelliteGameJam.Networking.Core
{
    /// <summary>
    /// Helper for retrying operations with dependencies that may not be ready at Awake.
    /// Replaces the manual Invoke(nameof(RegisterHandlers), 0.5f) pattern.
    /// </summary>
    public static class DependencyHelper
    {
        /// <summary>
        /// Retries an action until it succeeds or max attempts is reached.
        /// </summary>
        /// <param name="behaviour">The MonoBehaviour to run the coroutine on</param>
        /// <param name="action">The action to attempt</param>
        /// <param name="dependencyName">Name of the dependency for logging</param>
        /// <param name="retryInterval">Seconds between retries</param>
        /// <param name="maxAttempts">Max retry attempts (default 10)</param>
        public static void RetryUntilSuccess(
            MonoBehaviour behaviour,
            Func<bool> action,
            string dependencyName = "dependency",
            float retryInterval = 0.1f,
            int maxAttempts = 10)
        {
            behaviour.StartCoroutine(RetryCoroutine(action, dependencyName, retryInterval, maxAttempts));
        }

        private static IEnumerator RetryCoroutine(
            Func<bool> action,
            string dependencyName,
            float retryInterval,
            int maxAttempts)
        {
            int attempts = 0;
            
            while (attempts < maxAttempts)
            {
                if (action())
                {
                    yield break; // Success!
                }

                attempts++;
                if (attempts >= maxAttempts)
                {
                    Debug.LogError($"[DependencyHelper] Failed to initialize {dependencyName} after {maxAttempts} attempts!");
                    yield break;
                }

                yield return new WaitForSeconds(retryInterval);
            }
        }
    }
}