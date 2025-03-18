using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System;
using UnityEngine;
using UnityEngine.Networking;

public class ChatGPTHandler : MonoBehaviour
{
    private string apiKey;
    private string orgKey;
    private string apiUrl = "https://api.openai.com/v1/chat/completions";
    [SerializeField] string gptModel = "gpt-4o-mini";
    [SerializeField] int maxTokens = 100;

    // Variables to store token counts
    [SerializeField] int inputTokenCount = 0; // Total input tokens
    [SerializeField] int outputTokenCount = 0; // Total output tokens
    [SerializeField] int totalTokenCount = 0; // Total tokens (input + output)
    [SerializeField] TokenTracker tokenTracker;
    
    [SerializeField] private ColorManager colorManager;
    // Add new field for system message
    [TextArea(5, 20)] // 最小5行，最大20行
    [SerializeField] private string customSystemMessage = "You are a color expert. When analyzing images, focus on suggesting color variations and improvements. Format your response as follows:\n1. Current colors: [list main colors]\n2. Suggested changes: [list specific color suggestions]\n3. Reasoning: [explain why]";

    private void Start()
    {
        LoadAuthKeys();
    }

    private void LoadAuthKeys()
    {
        string path = Path.Combine(Application.dataPath, "../.openai/auth.json");
        if (File.Exists(path))
        {
            string json = File.ReadAllText(path);
            AuthKeys authKeys = JsonUtility.FromJson<AuthKeys>(json);
            apiKey = authKeys.api_key;
            orgKey = authKeys.organization;
        }
        else
        {
            Debug.LogError("Auth file not found!");
        }
    }

    public void AskChatGPT(string question, System.Action<string> onAnswerReceived)
    {
        int estimatedTokens = EstimateTokenUsage(question);
        if (tokenTracker.CanMakeApiCall(estimatedTokens))
        {
            StartCoroutine(SendChatGPTRequest(question, null, onAnswerReceived));
        }
        else
        {
            Debug.Log("Daily token limit reached. Try again tomorrow.");
        }
    }

    // Text-with-image version
    public void AskChatGPT(string question, Texture2D image, System.Action<string> onAnswerReceived)
    {
        StartCoroutine(SendChatGPTRequest(question, image, onAnswerReceived));
    }

    private IEnumerator SendChatGPTRequest(string question, Texture2D image, System.Action<string> onAnswerReceived)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            Debug.LogError("Question is empty or null.");
            onAnswerReceived("Please enter a valid question.");
            yield break;
        }

        string jsonData = null;
        if (image != null)
        {
            yield return PrepareImageRequest(question, image, (preparedJson) => jsonData = preparedJson);
        }
        else
        {
            yield return PrepareTextRequest(question, (preparedJson) => jsonData = preparedJson);
        }

        yield return SendRequest(jsonData, (response) => HandleResponse(response, image != null, onAnswerReceived));
    }

    private IEnumerator PrepareTextRequest(string question, System.Action<string> onPrepared)
    {
        var message = new RequestMessage
        {
            role = "user",
            content = new List<Content>
            {
                new Content { type = "text", text = question }
            }
        };

        var payload = new ChatGPTRequest
        {
            model = gptModel,
            messages = new List<RequestMessage> { message },
            max_tokens = maxTokens
        };

        onPrepared(JsonUtility.ToJson(payload));
        yield return null;
    }

    private IEnumerator PrepareImageRequest(string question, Texture2D image, System.Action<string> onPrepared)
    {
        string imageBase64 = ConvertTextureToBase64(image);
        
        var messages = new List<RequestMessage>
        {
            new RequestMessage
            {
                role = "system",
                content = new List<Content>
                {
                    new Content { type = "text", text = customSystemMessage }
                }
            },
            new RequestMessage
            {
                role = "user",
                content = new List<Content>
                {
                    new Content { type = "text", text = question },
                    new Content
                    {
                        type = "image_url",
                        image_url = new ImageUrl { url = $"data:image/jpg;base64,{imageBase64}" }
                    }
                }
            }
        };

        var payload = new ChatGPTRequest
        {
            model = gptModel,
            messages = messages,
            max_tokens = maxTokens
        };

        string jsonData = JsonUtility.ToJson(payload);
        onPrepared(CleanJson(jsonData));
        yield return null;
    }

    private IEnumerator SendRequest(string jsonData, System.Action<string> onResponse)
    {
        byte[] postData = Encoding.UTF8.GetBytes(jsonData);
        Debug.Log("Payload: " + jsonData);

        using (UnityWebRequest request = new UnityWebRequest(apiUrl, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(postData);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", $"Bearer {apiKey}");

            yield return request.SendWebRequest();
            onResponse(request.downloadHandler.text);
        }
    }

    private void HandleResponse(string responseText, bool isImageRequest, System.Action<string> onAnswerReceived)
    {
        if (string.IsNullOrEmpty(responseText))
        {
            Debug.LogError("Empty response received");
            onAnswerReceived("Error: Empty response");
            return;
        }

        try
        {
            ChatGPTResponse response = JsonUtility.FromJson<ChatGPTResponse>(responseText);
            string answer = null;

            if (response.choices != null && response.choices.Length > 0)
            {
                answer = response.choices[0].message.content.Trim();
                
                if (isImageRequest)
                {
                    if (colorManager != null)
                    {
                        colorManager.ParseAndStoreColors(answer);
                    }
                }
            }

            // 更新token计数
            UpdateTokenCounts(response.usage);
            
            onAnswerReceived(answer);
        }
        catch (Exception e)
        {
            Debug.LogError($"Error parsing response: {e.Message}\nResponse: {responseText}");
            onAnswerReceived("Error processing response");
        }
    }

    private void UpdateTokenCounts(Usage usage)
    {
        inputTokenCount = usage.prompt_tokens;
        outputTokenCount = usage.completion_tokens;
        totalTokenCount = inputTokenCount + outputTokenCount;
        tokenTracker.UpdateTokenUsage(totalTokenCount);
    }

    [System.Serializable]
    private class AuthKeys
    {
        public string api_key;
        public string organization;
    }

    [System.Serializable]
    private class ChatGPTResponse
    {
        public Choice[] choices;
        public Usage usage;
    }

    [System.Serializable]
    private class Choice
    {
        public ResponseMessage message; // Use ResponseMessage for receiving
    }

    [System.Serializable]
    private class ResponseMessage
    {
        public string role;
        public string content; // String for the assistant's text reply
    }

    [System.Serializable]
    private class Usage
    {
        public int prompt_tokens;
        public int completion_tokens;
        public int total_tokens;
    }

    // ===== REQUEST CLASSES =====
    [System.Serializable]
    private class ChatGPTRequest
    {
        public string model;
        public List<RequestMessage> messages; // Change from Message[] to List<Message>
        public int max_tokens;
    }

    [System.Serializable]
    private class RequestMessage
    {
        public string role;
        public List<Content> content;
    }

    [System.Serializable]
    public class Content
    {
        public string type;
        public string text;
        public ImageUrl image_url;
    }

    [System.Serializable]
    public class ImageUrl
    {
        public string url;
    }

    public string ConvertTextureToBase64(Texture2D texture)
    {
        byte[] imageBytes = texture.EncodeToPNG();
        return Convert.ToBase64String(imageBytes);
    }

    private string CleanJson(string json)
    {
        // Remove the "text" field from "image_url" objects
        string pattern = "\"text\":\"\",\"image_url\":";
        string cleanedJson = json.Replace(pattern, " \"image_url\":");

        return cleanedJson;
    }

    private int EstimateTokenUsage(string prompt)
    {
        // Rough estimate: 1 token ≈ 4 characters
        return Mathf.CeilToInt(prompt.Length / 4f);
    }
}
