using UnityEngine;
using System.Text.RegularExpressions;

[System.Serializable]
public struct ParsedMessage
{
    public string cleanAnswer;
    public int promptTokens;
    public int completionTokens;
    public int totalTokens;
    
    public bool IsValid => !string.IsNullOrEmpty(cleanAnswer);
}

public static class MessageParser 
{
    public static ParsedMessage ParseMessage(string jsonString)
    {
        try 
        {
            // 反序列化基础结构
            var baseData = JsonUtility.FromJson<BaseChatData>(jsonString);
            
            return new ParsedMessage 
            {
                cleanAnswer = CleanAnswerText(baseData.answer),
                promptTokens = baseData.token_usage.prompt_tokens,
                completionTokens = baseData.token_usage.completion_tokens,
                totalTokens = baseData.token_usage.total_tokens
            };
        }
        catch (System.Exception e)
        {
            Debug.LogError($"解析失败: {e.Message}");
            return new ParsedMessage(); // 返回无效数据
        }
    }

    private static string CleanAnswerText(string rawText)
    {
        // 分步清理流程
        string step1 = DecodeEscapes(rawText);
        string step2 = RemoveMarkdown(step1);
        return FinalTrim(step2);
    }

    private static string DecodeEscapes(string input)
    {
        return input
            .Replace("\\n", "\n")
            .Replace("\\\"", "\"")
            .Replace("\\\\", "\\");
    }

    private static string RemoveMarkdown(string input)
    {
        return Regex.Replace(input, @"```[\s\S]*?```|`", "");
    }

    private static string FinalTrim(string input)
    {
        return input.Trim('\n', ' ', '\t');
    }

    // 临时中间类
    [System.Serializable]
    private class BaseChatData 
    {
        public string answer;
        public TokenUsageData token_usage;
    }

    [System.Serializable]
    private class TokenUsageData 
    {
        public int prompt_tokens;
        public int completion_tokens;
        public int total_tokens;
    }
}