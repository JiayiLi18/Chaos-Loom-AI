using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System;
using System.Collections.Generic;

/// <summary>
/// 专注于API通信的客户端，只负责发送请求和接收原始响应
/// </summary>
public class ApiClient : MonoBehaviour
{
    [Header("API Endpoints")]
    [SerializeField] private string apiUrl_General = "http://127.0.0.1:8000/ask_general";
    [SerializeField] private string apiUrl_Texture = "http://127.0.0.1:8000/generate_texture";

    // 定义委托用于回调
    public delegate void ApiResponseCallback(string response, string error);

    #region Public API Methods
    public void SendGeneralRequest(string query, string imagePath, string sessionId, bool newSession, ApiResponseCallback callback)
    {
        if (string.IsNullOrEmpty(query))
        {
            callback?.Invoke(null, "Query is empty!");
            return;
        }
        StartCoroutine(PostGeneralRequest(query, imagePath, sessionId, newSession, callback));
    }

    public void SendTextureRequest(string textureName, string positivePrompt, string imagePath, string denoiseStrength, ApiResponseCallback callback)
    {
        if (string.IsNullOrEmpty(textureName) || string.IsNullOrEmpty(positivePrompt))
        {
            callback?.Invoke(null, "Required parameters are missing!");
            return;
        }
        StartCoroutine(PostTextureRequest(textureName, positivePrompt, imagePath, denoiseStrength, callback));
    }
    #endregion

    #region Private Request Methods
    private IEnumerator PostGeneralRequest(string query, string imagePath, string sessionId, bool newSession, ApiResponseCallback callback)
    {
        Debug.Log($"Parameters: query='{query}', imagePath='{imagePath}', sessionId='{sessionId}', newSession={newSession}");
        
        // Create form data
        List<IMultipartFormSection> formData = new List<IMultipartFormSection>();
        formData.Add(new MultipartFormDataSection("query", query));
        formData.Add(new MultipartFormDataSection("new_session", newSession.ToString().ToLower()));
        if (!string.IsNullOrEmpty(imagePath))
        {
            formData.Add(new MultipartFormDataSection("image_path", imagePath));
        }

        // Create the request with multipart form data
        UnityWebRequest www = UnityWebRequest.Post(apiUrl_General, formData);
        
        // Important: Set session_id header BEFORE sending the request
        if (!string.IsNullOrEmpty(sessionId))
        {
            www.SetRequestHeader("session_id", sessionId);
        }
        else
        {
            Debug.LogWarning("No session ID provided for request!");
        }
        
        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"Request failed: {www.error}");
            Debug.LogError($"Response Code: {www.responseCode}");
            callback?.Invoke(null, www.error);
        }
        else
        {
            callback?.Invoke(www.downloadHandler.text, null);
        }
        
        www.Dispose();
    }

    [System.Serializable]
    private class RequestData
    {
        public string query;
        public bool new_session;
        public string image_path;
    }

    private IEnumerator PostTextureRequest(string textureName, string positivePrompt, string imagePath, string denoiseStrength, ApiResponseCallback callback)
    {
        WWWForm form = new WWWForm();
        form.AddField("texture_name", textureName);
        form.AddField("positive_prompt", positivePrompt);
        form.AddField("denoise_strength", denoiseStrength);
        
        if (!string.IsNullOrEmpty(imagePath))
        {
            form.AddField("image_path", imagePath);
        }

        using (UnityWebRequest www = UnityWebRequest.Post(apiUrl_Texture, form))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                callback?.Invoke(null, www.error);
            }
            else
            {
                callback?.Invoke(www.downloadHandler.text, null);
            }
        }
    }
    #endregion
}
