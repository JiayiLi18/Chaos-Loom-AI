using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// 专注于API通信的客户端，支持Events和Permission API
/// </summary>
public class ApiClient : MonoBehaviour
{
    [Header("API Endpoints")]
    [SerializeField] private string apiUrl_Events = "http://127.0.0.1:8000/events";
    [SerializeField] private string apiUrl_Permission = "http://127.0.0.1:8000/permission";

    // 定义委托用于回调
    public delegate void ApiResponseCallback(string response, string error);

    private void Awake()
    {
        // 防止在场景切换或游戏退出时被销毁
        DontDestroyOnLoad(gameObject);
    }

    #region Public API Methods
    /// <summary>
    /// 发送事件批次到Events API
    /// </summary>
    /// <param name="eventBatch">事件批次数据</param>
    /// <param name="callback">回调函数</param>
    public void SendEventsRequest(EventBatch eventBatch, ApiResponseCallback callback)
    {
        if (eventBatch == null)
        {
            callback?.Invoke(null, "EventBatch is null!");
            return;
        }
        
        // 检查对象是否已被销毁
        if (this == null || !this || !gameObject)
        {
            callback?.Invoke(null, "ApiClient object has been destroyed!");
            return;
        }
        
        // 在发送前，将所有 ImageData 中的文件转换为 base64
        ConvertImageDataToBase64(eventBatch);
        
        // 使用 EventBatch.ToJson() 确保 payload 不丢失，直接发送
        var json = eventBatch.ToJson();
        StartCoroutine(PostEventsRequestRawJson(json, callback));
    }

    /// <summary>
    /// 发送权限请求到Permission API
    /// </summary>
    /// <param name="permissionRequest">权限请求数据</param>
    /// <param name="callback">回调函数</param>
    public void SendPermissionRequest(PermissionRequest permissionRequest, ApiResponseCallback callback)
    {
        if (permissionRequest == null)
        {
            callback?.Invoke(null, "PermissionRequest is null!");
            return;
        }
        StartCoroutine(PostPermissionRequest(permissionRequest, callback));
    }

    /// <summary>
    /// 发送 Plan Permission Request 到 Permission API（使用 raw JSON）
    /// </summary>
    /// <param name="jsonData">序列化后的 JSON 字符串</param>
    /// <param name="callback">回调函数</param>
    public void SendPlanPermissionRequest(string jsonData, ApiResponseCallback callback)
    {
        if (string.IsNullOrEmpty(jsonData))
        {
            callback?.Invoke(null, "JSON data is empty!");
            return;
        }
        StartCoroutine(PostPermissionRequestRawJson(jsonData, callback));
    }
    #endregion

    #region Private Request Methods

    /// <summary>
    /// 将 EventBatch 中所有 ImageData 的文件转换为 base64
    /// </summary>
    private void ConvertImageDataToBase64(EventBatch eventBatch)
    {
        if (eventBatch?.events == null) return;

        foreach (var eventData in eventBatch.events)
        {
            if (eventData?.payload == null) continue;

            // 根据事件类型处理不同的 payload
            switch (eventData.type)
            {
                case "player_speak":
                    var playerSpeakPayload = eventData.payload as PlayerSpeakPayload;
                    if (playerSpeakPayload?.image != null)
                    {
                        ConvertSingleImageDataToBase64(playerSpeakPayload.image);
                    }
                    break;

                case "agent_continue_plan":
                    var continuePlanPayload = eventData.payload as AgentContinuePlanPayload;
                    if (continuePlanPayload?.request_snapshot != null)
                    {
                        foreach (var img in continuePlanPayload.request_snapshot)
                        {
                            ConvertSingleImageDataToBase64(img);
                        }
                    }
                    break;

                case "agent_perception":
                    var perceptionPayload = eventData.payload as AgentPerceptionPayload;
                    if (perceptionPayload?.images != null)
                    {
                        foreach (var img in perceptionPayload.images)
                        {
                            ConvertSingleImageDataToBase64(img);
                        }
                    }
                    break;
            }
        }
    }

    /// <summary>
    /// 将单个 ImageData 的文件转换为 base64（如果还没有 base64 数据）
    /// </summary>
    private void ConvertSingleImageDataToBase64(ImageData imageData)
    {
        if (imageData == null) return;

        // 如果已经有 base64 数据，跳过
        if (!string.IsNullOrEmpty(imageData.base64))
        {
            return;
        }

        // 确定文件路径
        string filePath = null;
        
        if (!string.IsNullOrEmpty(imageData.file_path))
        {
            // 优先使用 file_path
            filePath = imageData.file_path;
        }
        else if (!string.IsNullOrEmpty(imageData.file_name))
        {
            // 如果只有 file_name，尝试从默认路径查找
            // 默认路径是 Application.persistentDataPath/Photos
            string defaultPhotoPath = Path.Combine(Application.persistentDataPath, "Photos");
            filePath = Path.Combine(defaultPhotoPath, imageData.file_name);
        }

        // 如果找到了文件路径，尝试读取并转换为 base64
        if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
        {
            try
            {
                byte[] fileBytes = File.ReadAllBytes(filePath);
                imageData.base64 = Convert.ToBase64String(fileBytes);
                Debug.Log($"Converted image file to base64: {filePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to convert image file to base64: {filePath}, Error: {e.Message}");
            }
        }
        else
        {
            Debug.LogWarning($"Image file not found: {filePath ?? "null"}");
        }
    }

    private IEnumerator PostEventsRequestRawJson(string jsonData, ApiResponseCallback callback)
    {
        // 记录 JSON 长度和关键信息（不记录完整 JSON，因为可能太长）
        int jsonLength = jsonData?.Length ?? 0;
        Debug.Log($"Sending Events Request - JSON length: {jsonLength} bytes");
        
        // 如果 JSON 太长，只记录前 1000 和后 1000 个字符用于调试
        if (jsonLength > 2000)
        {
            string preview = jsonData.Substring(0, Math.Min(1000, jsonLength));
            string suffix = jsonLength > 1000 ? jsonData.Substring(Math.Max(0, jsonLength - 1000)) : "";
            Debug.Log($"JSON Preview (first 1000 chars): {preview}...");
            Debug.Log($"JSON Preview (last 1000 chars): ...{suffix}");
        }
        else
        {
            Debug.Log($"Sending Events Request (raw): {jsonData}");
        }

        using (UnityWebRequest www = new UnityWebRequest(apiUrl_Events, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Events API request failed: {www.error}");
                Debug.LogError($"Response Code: {www.responseCode}");
                if (www.downloadHandler != null && !string.IsNullOrEmpty(www.downloadHandler.text))
                {
                    Debug.LogError($"Response Body: {www.downloadHandler.text}");
                }
                callback?.Invoke(null, www.error);
            }
            else
            {
                Debug.Log($"Events API Response: {www.downloadHandler.text}");
                callback?.Invoke(www.downloadHandler.text, null);
            }
        }
    }

    private IEnumerator PostPermissionRequest(PermissionRequest permissionRequest, ApiResponseCallback callback)
    {
        string jsonData = "";
        try
        {
            // 序列化PermissionRequest为JSON
            jsonData = JsonUtility.ToJson(permissionRequest, true);
            Debug.Log($"Sending Permission Request: {jsonData}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Exception serializing PermissionRequest: {e.Message}");
            callback?.Invoke(null, e.Message);
            yield break;
        }

        // 创建POST请求
        using (UnityWebRequest www = new UnityWebRequest(apiUrl_Permission, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Permission API request failed: {www.error}");
                Debug.LogError($"Response Code: {www.responseCode}");
                callback?.Invoke(null, www.error);
            }
            else
            {
                Debug.Log($"Permission API Response: {www.downloadHandler.text}");
                callback?.Invoke(www.downloadHandler.text, null);
            }
        }
    }

    private IEnumerator PostPermissionRequestRawJson(string jsonData, ApiResponseCallback callback)
    {
        Debug.Log($"Sending Permission Request (raw): {jsonData}");

        using (UnityWebRequest www = new UnityWebRequest(apiUrl_Permission, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Permission API request failed: {www.error}");
                Debug.LogError($"Response Code: {www.responseCode}");
                if (www.downloadHandler != null && !string.IsNullOrEmpty(www.downloadHandler.text))
                {
                    Debug.LogError($"Response Body: {www.downloadHandler.text}");
                }
                callback?.Invoke(null, www.error);
            }
            else
            {
                Debug.Log($"Permission API Response: {www.downloadHandler.text}");
                callback?.Invoke(www.downloadHandler.text, null);
            }
        }
    }
    #endregion
}
