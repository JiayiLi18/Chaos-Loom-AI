using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System;
using System.Collections.Generic;

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
    #endregion

    #region Test Helpers
    /// <summary>
    /// 测试：使用真实的 GameStateManager 数据构造 EventBatch 并发送到 Events API
    /// 在 Unity Inspector 中右键组件，选择 "Test Send Real Game State" 触发
    /// </summary>
    [ContextMenu("Test Send Real Game State")]
    public void TestSendRealGameState()
    {
        try
        {
            // 查找 GameStateManager
            var gameStateManager = FindFirstObjectByType<GameStateManager>();
            if (gameStateManager == null)
            {
                Debug.LogError("[ApiClient] Could not find GameStateManager component!");
                return;
            }

            // 触发一次自动监控更新，获取最新的游戏状态
            gameStateManager.TriggerManualUpdate();

            // 创建测试事件
            var testSessionId = $"session_{DateTime.Now:yyyyMMdd_HHmmss}";
            var ts = TimestampUtils.GenerateTimestamp();
            
            var testEvent = new EventData(
                ts,
                "player_speak",
                new PlayerSpeakPayload("Hello from TestSendRealGameState", null)
            );

            // 创建 EventBatch 并使用真实的游戏状态
            var eventBatch = new EventBatch(testSessionId);
            eventBatch.AddEvent(testEvent);
            
            // 获取当前游戏状态并设置到批次中
            var currentGameState = gameStateManager.GetCurrentGameState();
            eventBatch.SetGameState(currentGameState);

            Debug.Log($"[ApiClient] Sending real game state batch session= {testSessionId}");
            Debug.Log($"[ApiClient] Game State: Agent={currentGameState.agent_position.x},{currentGameState.agent_position.y},{currentGameState.agent_position.z}");
            Debug.Log($"[ApiClient] Six Direction: Up={currentGameState.six_direction.up.name} (id:{currentGameState.six_direction.up.id}, dist:{currentGameState.six_direction.up.distance})");

            // 使用 EventBatch.ToJson() 发送
            SendEventsRequest(eventBatch, (response, error) =>
            {
                if (!string.IsNullOrEmpty(error))
                {
                    Debug.LogError($"[ApiClient] Real game state batch send failed: {error}");
                }
                else
                {
                    Debug.Log($"[ApiClient] Real game state batch sent successfully: {response}");
                }
            });
        }
        catch (Exception e)
        {
            Debug.LogError($"[ApiClient] Exception in TestSendRealGameState: {e.Message}");
        }
    }

    /// <summary>
    /// 旧版测试：构造一个硬编码的测试 EventBatch（保留作为备用）
    /// </summary>
    [ContextMenu("Test Send Dummy Batch (Legacy)")]
    public void TestSendDummyBatchLegacy()
    {
        try
        {
            // 创建测试批次
            var testSessionId = $"session_{DateTime.Now:yyyyMMdd_HHmmss}";
            var ts = TimestampUtils.GenerateTimestamp();

            // 直接构造最小合法 JSON（确保 payload 保留）
            var json = "{" +
                $"\"session_id\":\"{testSessionId}\"," +
                "\"events\":[{" +
                    $"\"timestamp\":\"{ts}\"," +
                    "\"type\":\"player_speak\"," +
                    "\"payload\":{\"text\":\"Hello from TestSendDummyBatchLegacy\",\"image\":null}" +
                "}]," +
                "\"game_state\":{" +
                    $"\"timestamp\":\"{ts}\"," +
                    "\"agent_position\":{\"x\":0,\"y\":0,\"z\":0}," +
                    "\"player_position_rel\":{\"x\":0,\"y\":0,\"z\":0}," +
                    "\"six_direction\":{" +
                        "\"up\":{\"name\":\"empty\",\"id\":\"0\",\"distance\":10}," +
                        "\"down\":{\"name\":\"empty\",\"id\":\"0\",\"distance\":10}," +
                        "\"front\":{\"name\":\"empty\",\"id\":\"0\",\"distance\":10}," +
                        "\"back\":{\"name\":\"empty\",\"id\":\"0\",\"distance\":10}," +
                        "\"left\":{\"name\":\"empty\",\"id\":\"0\",\"distance\":10}," +
                        "\"right\":{\"name\":\"empty\",\"id\":\"0\",\"distance\":10}" +
                    "}," +
                    "\"nearby_voxels\":\"\"," +
                    "\"pending_plans\":[]," +
                    "\"last_commands\":[]" +
                "}" +
            "}";

            Debug.Log($"[ApiClient] Sending legacy dummy batch session= {testSessionId}\n{json}");

            // 直接发送 JSON 到 Events API（测试用）
            StartCoroutine(PostEventsRequestRawJson(json, (response, error) =>
            {
                if (!string.IsNullOrEmpty(error))
                {
                    Debug.LogError($"[ApiClient] Legacy dummy batch send failed: {error}");
                }
                else
                {
                    Debug.Log($"[ApiClient] Legacy dummy batch sent successfully: {response}");
                }
            }));
        }
        catch (Exception e)
        {
            Debug.LogError($"[ApiClient] Exception in TestSendDummyBatchLegacy: {e.Message}");
        }
    }
    #endregion

    #region Private Request Methods

    private IEnumerator PostEventsRequestRawJson(string jsonData, ApiResponseCallback callback)
    {
        Debug.Log($"Sending Events Request (raw): {jsonData}");

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
    #endregion
}
