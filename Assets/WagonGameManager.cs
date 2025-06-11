using System.Collections.Generic;
using UnityEngine;
using Firebase;
using Firebase.Database;
using Firebase.Extensions;
using UnityEngine.UI;
using TMPro;

public class WagonGameManager : MonoBehaviour
{
    [Header("Game Settings")]
    public int maxNumberRange;
    public float miniGameDuration;
    public int numQuestions;
    public int requiredScore;

    [Header("UI Elements")]
    public TextMeshProUGUI questionText;
    public TMP_InputField inputPrevious;
    public TMP_InputField inputNext;
    public TextMeshProUGUI feedbackText;
    public Button submitButton;
    public Button nextButton;
    public Button restartButton;
    public TextMeshProUGUI scoreText;

    private DatabaseReference dbRef;
    private string playerUID;
    private int playerGrade;

    private int currentNumber;
    private int currentScore;
    private int questionCount;
    private bool gameOver;

    void Start()
    {
        dbRef = FirebaseDatabase.DefaultInstance.RootReference;
        playerUID = PlayerPrefs.GetString("PlayerUID", "");
        playerGrade = PlayerPrefs.GetInt("PlayerGrade", 0);

        if (string.IsNullOrEmpty(playerUID) || playerGrade <= 0)
        {
            Debug.LogError("Player UID or grade not found in PlayerPrefs.");
            return;
        }

        FetchGameConfig();
    }

    private void FetchGameConfig()
{
    dbRef.Child("tests").GetValueAsync().ContinueWithOnMainThread(task => {
        if (task.IsFaulted)
        {
            Debug.LogError("Error fetching tests: " + task.Exception);
            return;
        }

        DataSnapshot snapshot = task.Result;
        bool configFound = false;

        // Add debug logging
        Debug.Log($"Fetching game config for player grade: {playerGrade}");

        foreach (DataSnapshot testSnapshot in snapshot.Children)
        {
            // Check if this test is for the player's grade
            if (testSnapshot.Child("grade").Exists && 
                int.TryParse(testSnapshot.Child("grade").Value.ToString(), out int grade) && 
                grade == playerGrade)
            {
                Debug.Log($"Found test for grade {playerGrade}: {testSnapshot.Key}");
                
                // Navigate to the find_previous_next_number mini-game config
                DataSnapshot miniGameConfigsSnapshot = testSnapshot.Child("miniGameConfigs");
                if (miniGameConfigsSnapshot.Exists)
                {
                    DataSnapshot gameConfig = miniGameConfigsSnapshot.Child("find_previous_next_number");
                    
                    if (gameConfig.Exists)
                    {
                        // Extract the configuration values
                        if (gameConfig.Child("maxNumberRange").Exists)
                            maxNumberRange = int.Parse(gameConfig.Child("maxNumberRange").Value.ToString());
                        
                        if (gameConfig.Child("miniGameDuration").Exists)
                            miniGameDuration = float.Parse(gameConfig.Child("miniGameDuration").Value.ToString());
                        
                        if (gameConfig.Child("numQuestions").Exists)
                            numQuestions = int.Parse(gameConfig.Child("numQuestions").Value.ToString());
                        
                        // Convert percentage to required score
                        if (gameConfig.Child("requiredCorrectAnswersMinimumPercent").Exists)
                        {
                            int percent = int.Parse(gameConfig.Child("requiredCorrectAnswersMinimumPercent").Value.ToString());
                            requiredScore = Mathf.CeilToInt(numQuestions * percent / 100f);
                        }
                        
                        Debug.Log($"Loaded config: maxNumberRange={maxNumberRange}, miniGameDuration={miniGameDuration}, numQuestions={numQuestions}, requiredScore={requiredScore}");
                        
                        configFound = true;
                        StartGame();
                        break;
                    }
                }
            }
        }

        if (!configFound)
        {
            Debug.LogError($"No test config found for grade {playerGrade}. Using defaults.");
            // Set default values as fallback
            maxNumberRange = 10;
            miniGameDuration = 30;
            numQuestions = 5;
            requiredScore = 3;
            StartGame();
        }
    });
}

    public void StartGame()
    {
        currentScore = 0;
        questionCount = 0;
        gameOver = false;

        inputPrevious.gameObject.SetActive(true);
        inputNext.gameObject.SetActive(true);
        submitButton.gameObject.SetActive(true);
        restartButton.gameObject.SetActive(false);
        nextButton.gameObject.SetActive(false);
        feedbackText.text = "";

        GenerateQuestion();
    }

    public void GenerateQuestion()
    {
        currentNumber = Random.Range(2, maxNumberRange);
        questionText.text = $"What comes before and after {currentNumber}?";

        inputPrevious.text = "";
        inputNext.text = "";
        inputPrevious.interactable = true;
        inputNext.interactable = true;
        submitButton.interactable = true;
        submitButton.gameObject.SetActive(true);
        nextButton.gameObject.SetActive(false);
    }

    public void CheckAnswer()
    {
        bool isPrevValid = int.TryParse(inputPrevious.text, out int prevGuess);
        bool isNextValid = int.TryParse(inputNext.text, out int nextGuess);

        if (!isPrevValid || !isNextValid)
        {
            feedbackText.text = "Please enter valid numbers.";
            return;
        }

        if (prevGuess == currentNumber - 1 && nextGuess == currentNumber + 1)
        {
            feedbackText.text = "Correct!";
            currentScore++;
        }
        else
        {
            feedbackText.text = $"Incorrect. The correct answers are {currentNumber - 1} and {currentNumber + 1}.";
        }

        questionCount++;
        UpdateScoreUI();

        submitButton.interactable = false;
        submitButton.gameObject.SetActive(false);
        inputPrevious.interactable = false;
        inputNext.interactable = false;
        nextButton.gameObject.SetActive(true);

        if (questionCount >= numQuestions)
        {
            gameOver = true;
            nextButton.GetComponentInChildren<TextMeshProUGUI>().text = "Finish";
        }
    }

    public void NextQuestion()
    {
        if (gameOver)
        {
            EndMiniGame();
        }
        else
        {
            GenerateQuestion();
        }
    }

    private void EndMiniGame()
    {
        questionText.text = "Game Over!";
        feedbackText.text = $"You finished your journey, you traveled {currentScore}/{numQuestions} stations.";

        inputPrevious.gameObject.SetActive(false);
        inputNext.gameObject.SetActive(false);
        submitButton.gameObject.SetActive(false);
        nextButton.gameObject.SetActive(false);
        restartButton.gameObject.SetActive(true);

        SaveResultsToDatabase();
    }

    public void RestartGame()
    {
        nextButton.GetComponentInChildren<TextMeshProUGUI>().text = "";
        StartGame();
    }

    private void SaveResultsToDatabase()
    {
        string resultPath = $"users/{playerUID}/results";
        var resultData = new Dictionary<string, object>()
        {
            {"AnsweredQuestions", currentScore},
            {"score" , currentScore * 100 / numQuestions}, // Percentage score
            {"duration", miniGameDuration},
            {"numQuestions", numQuestions},
            {"passed", currentScore >= requiredScore},
            {"completed At", System.DateTime.UtcNow.ToString("o")}, // ISO 8601 format
        };

        dbRef.Child(resultPath).Push().SetValueAsync(resultData).ContinueWithOnMainThread(task => {
            if (task.IsFaulted)
            {
                Debug.LogError("Error saving results: " + task.Exception);
            }
            else
            {
                Debug.Log("Results saved successfully.");
            }
        });
    }

    private void UpdateScoreUI()
    {
        if (scoreText != null)
            scoreText.text = $"Score: {currentScore}/{requiredScore}";
    }
}
