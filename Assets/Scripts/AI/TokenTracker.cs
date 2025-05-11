using UnityEngine;
using System;

public class TokenTracker : MonoBehaviour
{
    private const string TotalTokenKey = "DailyTotalTokens";
    private const string DateKey = "LastResetDate";
    
    [Header("Settings")]
    [SerializeField] int dailyTokenLimit = 10000;
    
    [Header("Debug")]
    public static bool limitReached = false;
    [SerializeField] int totalTokensUsedToday;
    [SerializeField] int promptTokensUsedToday;
    [SerializeField] int completionTokensUsedToday;
    [SerializeField] string currentDate;

    void Start() => ResetTokenCountIfNewDay();

    // 基础更新方法（兼容新旧系统）
    public void UpdateTokenUsage(int promptTokens, int completionTokens)
    {
        promptTokensUsedToday += promptTokens;
        completionTokensUsedToday += completionTokens;
        totalTokensUsedToday = promptTokensUsedToday + completionTokensUsedToday;

        SaveToPlayerPrefs();
        CheckTokenLimit();
    }

    private void SaveToPlayerPrefs()
    {
        PlayerPrefs.SetInt(TotalTokenKey, totalTokensUsedToday);
        PlayerPrefs.Save();
    }

    private void CheckTokenLimit()
    {
        if (totalTokensUsedToday > dailyTokenLimit)
        {
            Debug.LogWarning($"每日Token限额已达！已用：{totalTokensUsedToday}/{dailyTokenLimit}");
            // 这里可以触发UI警告或禁用API调用
            limitReached = true;
        }
    }

    // 增强版日期重置检查
    private void ResetTokenCountIfNewDay()
    {
        currentDate = DateTime.Now.ToString("yyyy-MM-dd");
        string lastDate = PlayerPrefs.GetString(DateKey, "");

        if (lastDate != currentDate)
        {
            PlayerPrefs.DeleteKey(TotalTokenKey);
            PlayerPrefs.SetString(DateKey, currentDate);
            
            totalTokensUsedToday = PlayerPrefs.GetInt(TotalTokenKey, 0);
            promptTokensUsedToday = 0;
            completionTokensUsedToday = 0;
            limitReached = false;
            
            Debug.Log("Token计数器已重置为新的一天");
        }
        else
        {
            totalTokensUsedToday = PlayerPrefs.GetInt(TotalTokenKey, 0);
            // 如果需要保持prompt/completion的详细记录，这里需要额外存储
        }
    }

    // 新增数据获取接口
    public (int total, int prompt, int completion) GetUsageData() 
        => (totalTokensUsedToday, promptTokensUsedToday, completionTokensUsedToday);
}