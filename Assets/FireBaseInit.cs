using Firebase;
using Firebase.Extensions;
using UnityEngine;

public class FirebaseInitializer : MonoBehaviour
{
    void Awake()
    {
        FirebaseApp.CheckAndFixDependenciesAsync()
          .ContinueWithOnMainThread(task => {
              if (task.Result == DependencyStatus.Available)
              {
                  Debug.Log("Firebase ready!");
              }
              else
              {
                  Debug.LogError($"Could not resolve all Firebase dependencies: {task.Result}");
              }
          });
    }
}
