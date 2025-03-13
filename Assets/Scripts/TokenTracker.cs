using UnityEngine;
using System;

public class TokenTracker : MonoBehaviour
{
    private const string TokenKey = "DailyTokenUsage";
    private const string DateKey = "LastResetDate";
    [SerializeField] const int DailyTokenLimit = 10000; // Set your daily token limit here
    
    [SerializeField] int tokensUsedToday;
    [SerializeField] string currentDate;

    void Start()
    {
        // Check if the token count needs to be reset (e.g., if it's a new day)
        ResetTokenCountIfNewDay();
    }

    // Call this method after each API call to update the token count
    public void UpdateTokenUsage(int tokensUsed)
    {
        tokensUsedToday = PlayerPrefs.GetInt(TokenKey, 0);
        tokensUsedToday += tokensUsed;

        if (tokensUsedToday > DailyTokenLimit)
        {
            Debug.LogWarning("Daily token limit reached. No more API calls allowed today.");
            // Optionally, disable further API calls or notify the user
            return;
        }

        PlayerPrefs.SetInt(TokenKey, tokensUsedToday);
        PlayerPrefs.Save();
    }

    // Check if the token count should be reset (e.g., at the start of a new day)
    private void ResetTokenCountIfNewDay()
    {
        string lastResetDate = PlayerPrefs.GetString(DateKey, "");
        currentDate = DateTime.Now.ToString("yyyy-MM-dd");

        if (lastResetDate != currentDate)
        {
            PlayerPrefs.SetInt(TokenKey, 0); // Reset token count
            PlayerPrefs.SetString(DateKey, currentDate); // Update the reset date
            tokensUsedToday = 0;
            PlayerPrefs.Save();
        }
        else{
             tokensUsedToday = PlayerPrefs.GetInt(TokenKey, 0);
        }
    }

    // Call this method before making an API call to check if the limit is reached
    public bool CanMakeApiCall(int estimatedTokens)
    {
        return (tokensUsedToday + estimatedTokens) <= DailyTokenLimit;
    }
}
