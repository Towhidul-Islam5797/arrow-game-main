using UnityEngine;
using System;

#if MT_FIREBASE
using Firebase.Extensions;
using Firebase.Crashlytics;
using Firebase.AppCheck;
#endif


namespace MultiTechStudio.EscapeGame
{
    /// <summary>
    /// Handles optional Firebase initialisation and analytics hooks.
    /// </summary>
    public class FirebaseManager : MonoBehaviour
    {
        public static System.Action OnRemoteConfigValuesReceived;

        void Start()
        {
            Initialize();
        }

        public void SendLevelData()
        {
#if MT_FIREBASE
            if (LevelManager.Instance != null)
            {
                Crashlytics.SetCustomKey("level", LevelManager.Instance.currentLevelIndex.ToString());
                if (LevelManager.Instance.uiManager != null)
                {
                    Crashlytics.SetCustomKey("coins", LevelManager.Instance.uiManager.coins.ToString());
                }
            }
#endif
        }

        void Initialize()
        {
#if MT_FIREBASE
            Firebase.FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(task =>
            {
                var dependencyStatus = task.Result;
                if (dependencyStatus == Firebase.DependencyStatus.Available)
                {
                    _ = Firebase.FirebaseApp.DefaultInstance;
                }
                else
                {
                    Debug.LogError($"Could not resolve all Firebase dependencies: {dependencyStatus}");
                }
            });
#endif
        }
    }
}