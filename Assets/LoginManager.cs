using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Firebase;
using Firebase.Database;
using Firebase.Extensions;
using TMPro;

public class LoginManager : MonoBehaviour
{
    [Header("UI References")]
    public TMP_InputField uidInput;
    public TMP_InputField pinInput;
    public Button loginButton;
    public TextMeshProUGUI feedbackText;

    private DatabaseReference dbRef;

    void Start()
    {
        // Get root database reference (ensure Firebase is initialized elsewhere)
        dbRef = FirebaseDatabase.DefaultInstance.RootReference;
        loginButton.onClick.AddListener(AttemptLogin);
    }

    /// <summary>
    /// Attempts to log the user in by checking UID and PIN against the Realtime Database.
    /// On success, stores grade in PlayerPrefs and loads the game scene.
    /// </summary>
    private void AttemptLogin()
    {
        string uid = uidInput.text.Trim();
        string pin = pinInput.text.Trim();

        if (string.IsNullOrEmpty(uid) || string.IsNullOrEmpty(pin))
        {
            feedbackText.text = "Please enter both UID and PIN.";
            return;
        }

        feedbackText.text = "Checking credentials...";

        // Query the user's node
        dbRef.Child("users").Child(uid).GetValueAsync().ContinueWithOnMainThread(task => {
            if (task.IsFaulted)
            {
                feedbackText.text = "Database error: " + task.Exception?.Message;
                return;
            }

            DataSnapshot snapshot = task.Result;
            if (!snapshot.Exists)
            {
                feedbackText.text = "UID not found.";
                return;
            }

            // Compare PIN (assuming stored in plain text for simplicity)
            string storedPin = snapshot.Child("password").Value?.ToString() ?? string.Empty;
            if (storedPin != pin)
            {
                feedbackText.text = "Invalid PIN.";
                return;
            }

            // Retrieve grade (try schoolGrade then mathGrade)
            int grade = 0;
            if (snapshot.Child("schoolGrade").Exists)
            {
                int.TryParse(snapshot.Child("schoolGrade").Value.ToString(), out grade);
            }
            else if (snapshot.Child("mathGrade").Exists)
            {
                int.TryParse(snapshot.Child("mathGrade").Value.ToString(), out grade);
            }

            if (grade <= 0)
            {
                feedbackText.text = "No grade found for this user.";
                return;
            }

            // Save grade to PlayerPrefs for later use
            PlayerPrefs.SetInt("PlayerGrade", grade);
            PlayerPrefs.SetString("PlayerUID", uid);
            PlayerPrefs.Save();

            // Load the game scene (ensure it's added in Build Settings)
            SceneManager.LoadScene("Lobby");
        });
    }
}
