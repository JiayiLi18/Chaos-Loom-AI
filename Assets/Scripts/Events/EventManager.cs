using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 事件管理器 - 负责收集和管理游戏事件，并发送到Events API
/// </summary>
public class EventManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ApiClient apiClient;
    [SerializeField] private GameStateManager gameStateManager;
    
    [Header("Settings")]
    [SerializeField] private float batchSendInterval = 10f; // 批次发送间隔（秒）
    [SerializeField] private int maxEventsPerBatch = 10; // 每批次最大事件数
    [SerializeField] private string[] immediateSendEventType = {"player_speak", "agent_continue_plan", "agent_perception"}; //立即发送事件类型
    [SerializeField] private bool autoSend = true; // 是否自动发送

    private EventBatch currentBatch;
    private string currentSessionId;
    private float lastSendTime;
    private bool isInitialized = false;

    // 事件回调
    public delegate void EventSentCallback(bool success, string response, string error);
    public static event EventSentCallback OnEventBatchSent;

    private void Start()
    {
        InitializeEventManager();
    }

    private void Update()
    {
        if (autoSend && isInitialized && currentBatch != null && currentBatch.events.Count > 0)
        {

            // 遍历检查是否存在立即发送事件类型
            bool isImmediateSendEventType = false;
            foreach (var eventData in currentBatch.events)
            {
                if (IsImmediateSendEventType(eventData.type))
                {
                    isImmediateSendEventType = true;
                    break;
                }
            }

            // 检查是否需要发送批次
            bool shouldSend = Time.time - lastSendTime >= batchSendInterval || 
                             currentBatch.events.Count >= maxEventsPerBatch;
            
            if (shouldSend || isImmediateSendEventType)
            {
                SendCurrentBatch();
            }
        }
    }

    private void InitializeEventManager()
    {
        // 获取ApiClient引用
        if (apiClient == null)
        {
            apiClient = FindAnyObjectByType<ApiClient>();
            if (apiClient == null)
            {
                Debug.LogError("EventManager: 找不到ApiClient组件！");
                enabled = false;
                return;
            }
        }

        // 获取GameStateManager引用
        if (gameStateManager == null)
        {
            gameStateManager = FindAnyObjectByType<GameStateManager>();
            if (gameStateManager == null)
            {
                Debug.LogError("EventManager: 找不到GameStateManager组件！");
                enabled = false;
                return;
            }
        }

        // 生成会话ID
        currentSessionId = $"session_{System.DateTime.Now:yyyyMMdd_HHmmss}";
        
        // 创建新的事件批次
        CreateNewBatch();
        
        isInitialized = true;
        Debug.Log($"EventManager initialized with session ID: {currentSessionId}");
    }

    /// <summary>
    /// 创建新的事件批次
    /// </summary>
    public void CreateNewBatch()
    {
        currentBatch = new EventBatch(currentSessionId);
        lastSendTime = Time.time;
    }

    /// <summary>
    /// 添加玩家说话事件
    /// </summary>
    public void AddPlayerSpeakEvent(string text, string imagePath = null, string imageBase64 = null, string imageUrl = null)
    {
        if (!isInitialized) return;

        ImageData imageData = null;
        if (!string.IsNullOrEmpty(imagePath) || !string.IsNullOrEmpty(imageBase64) || !string.IsNullOrEmpty(imageUrl))
        {
            imageData = new ImageData(imageBase64, imageUrl, imagePath);
        }

        var payload = new PlayerSpeakPayload(text, imageData);
        var eventData = new EventData(TimestampUtils.GenerateTimestamp(), "player_speak", payload);
        
        currentBatch.AddEvent(eventData);
        Debug.Log($"Added player_speak event: {text}");
    }

    /// <summary>
    /// 添加玩家建造事件
    /// </summary>
    public void AddPlayerBuildEvent(string voxelId, string voxelName, Vector3 position)
    {
        if (!isInitialized) return;

        var voxelInstance = new VoxelInstanceData(voxelId, voxelName, TimestampUtils.ToVector3Data(position));
        var payload = new PlayerBuildPayload(voxelInstance);
        var eventData = new EventData(TimestampUtils.GenerateTimestamp(), "player_build", payload);
        
        currentBatch.AddEvent(eventData);
        Debug.Log($"Added player_build event: {voxelName} at {position}");
    }

    /// <summary>
    /// 添加体素类型创建事件
    /// </summary>
    public void AddVoxelTypeCreatedEvent(string id, string name, string description, string texture = "", string[] faceTextures = null)
    {
        if (!isInitialized) return;

        var voxelType = new VoxelTypeData(id, name, description, texture, faceTextures);
        var payload = new VoxelTypeCreatedPayload(voxelType);
        var eventData = new EventData(TimestampUtils.GenerateTimestamp(), "voxel_type_created", payload);
        
        currentBatch.AddEvent(eventData);
        Debug.Log($"Added voxel_type_created event: {name} (ID: {id})");
    }

    /// <summary>
    /// 添加体素类型更新事件
    /// </summary>
    public void AddVoxelTypeUpdatedEvent(string voxelId, string newId, string newName, string newDescription, string newTexture = "", string[] newFaceTextures = null)
    {
        if (!isInitialized) return;

        var newVoxelType = new VoxelTypeData(newId, newName, newDescription, newTexture, newFaceTextures);
        var payload = new VoxelTypeUpdatedPayload(voxelId, newVoxelType);
        var eventData = new EventData(TimestampUtils.GenerateTimestamp(), "voxel_type_updated", payload);
        
        currentBatch.AddEvent(eventData);
        Debug.Log($"Added voxel_type_updated event: {voxelId} -> {newName}");
    }

    /// <summary>
    /// 添加AI继续计划事件
    /// </summary>
    public void AddAgentContinuePlanEvent(string currentSummary, string possibleNextSteps, bool requestSnapshot = false)
    {
        if (!isInitialized) return;

        var payload = new AgentContinuePlanPayload(currentSummary, possibleNextSteps, requestSnapshot);
        var eventData = new EventData(TimestampUtils.GenerateTimestamp(), "agent_continue_plan", payload);
        
        currentBatch.AddEvent(eventData);
        Debug.Log($"Added agent_continue_plan event: {currentSummary}");
    }

    /// <summary>
    /// 添加AI感知事件
    /// </summary>
    public void AddAgentPerceptionEvent(List<ImageData> images, string nearbyVoxels, SixDirectionData sixDirection)
    {
        if (!isInitialized) return;

        var payload = new AgentPerceptionPayload(images);
        var eventData = new EventData(TimestampUtils.GenerateTimestamp(), "agent_perception", payload);
        
        currentBatch.AddEvent(eventData);
        Debug.Log($"Added agent_perception event with {images?.Count ?? 0} images");
    }


    /// <summary>
    /// 立即发送当前批次
    /// </summary>
    public void SendCurrentBatch()
    {
        if (!isInitialized || currentBatch == null || currentBatch.events.Count == 0)
        {
            Debug.LogWarning("EventManager: 没有事件需要发送");
            return;
        }

        // 从 GameStateManager 获取最新的游戏状态
        if (gameStateManager != null)
        {
            var currentGameState = gameStateManager.GetCurrentGameState();
            currentBatch.SetGameState(currentGameState);
            Debug.Log($"EventManager: 已更新批次中的游戏状态快照");
        }
        else
        {
            Debug.LogWarning("EventManager: GameStateManager 未找到，跳过游戏状态更新");
        }

        // 预校验，避免 422
        if (!currentBatch.Validate(out var validationError))
        {
            Debug.LogError($"EventManager: 当前批次校验失败 -> {validationError}");
            return;
        }

        Debug.Log($"Sending event batch with {currentBatch.events.Count} events");
        
        // 使用统一的公共接口，内部会自动处理 JSON 序列化
        apiClient.SendEventsRequest(currentBatch, OnEventBatchSentCallback);
        
        // 重置批次
        CreateNewBatch();
    }

    /// <summary>
    /// 强制发送当前批次（忽略自动发送设置）
    /// </summary>
    public void ForceSendBatch()
    {
        autoSend = false; // 临时禁用自动发送
        SendCurrentBatch();
        autoSend = true;  // 重新启用自动发送
    }

    /// <summary>
    /// 清空当前批次
    /// </summary>
    public void ClearCurrentBatch()
    {
        if (currentBatch != null)
        {
            currentBatch.events.Clear();
            currentBatch.SetGameState(null);
        }
        Debug.Log("Current event batch cleared");
    }

    /// <summary>
    /// 获取当前批次的事件数量
    /// </summary>
    public int GetCurrentBatchEventCount()
    {
        return currentBatch?.events.Count ?? 0;
    }

    /// <summary>
    /// 设置自动发送
    /// </summary>
    public void SetAutoSend(bool enabled)
    {
        autoSend = enabled;
        Debug.Log($"EventManager auto-send {(enabled ? "enabled" : "disabled")}");
    }

    /// <summary>
    /// 设置批次发送间隔
    /// </summary>
    public void SetBatchSendInterval(float interval)
    {
        batchSendInterval = Mathf.Max(1f, interval);
        Debug.Log($"EventManager batch send interval set to {batchSendInterval}s");
    }

    /// <summary>
    /// 设置每批次最大事件数
    /// </summary>
    public void SetMaxEventsPerBatch(int maxEvents)
    {
        maxEventsPerBatch = Mathf.Max(1, maxEvents);
        Debug.Log($"EventManager max events per batch set to {maxEventsPerBatch}");
    }

    /// <summary>
    /// 检查是否存在立即发送事件类型
    /// </summary>
    public bool IsImmediateSendEventType(string eventType)
    {
        return System.Array.IndexOf(immediateSendEventType, eventType) >= 0;
    }

    /// <summary>
    /// 事件批次发送回调
    /// </summary>
    private void OnEventBatchSentCallback(string response, string error)
    {
        bool success = string.IsNullOrEmpty(error);
        
        if (success)
        {
            Debug.Log($"Event batch sent successfully: {response}");
        }
        else
        {
            Debug.LogError($"Event batch send failed: {error}");
        }

        // 触发事件回调
        OnEventBatchSent?.Invoke(success, response, error);
    }

    private void OnDestroy()
    {
        // 在销毁前发送剩余的事件
        if (isInitialized && currentBatch != null && currentBatch.events.Count > 0)
        {
            Debug.Log("EventManager: Sending remaining events before destruction");
            autoSend = false; // 禁用自动发送
            SendCurrentBatch();
        }
    }
}
