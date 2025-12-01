using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// 通用意图消息（可选）：便于外部用字符串 + 现成 payload 进行发布
public struct EventIntent
{
    public string type;   // 如 "player_build" / "voxel_type_created" / ...
    public object payload; // 直接放 PlayerBuildPayload / VoxelTypeCreatedPayload 等
}

/// <summary>
/// 事件管理器 - 负责收集和管理游戏事件，并发送到Events API
/// </summary>
public class EventManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameStateManager gameStateManager;
    [SerializeField] private AgentCam agentCam;
    private RuntimeAIChat runtimeAIChat;

    [Header("Settings")]
    [SerializeField] private float batchSendInterval = 10f; // 批次发送间隔（秒）
    [SerializeField] private int maxEventsPerBatch = 10; // 每批次最大事件数
    [SerializeField] private int minEventsForPerception = 1; // 触发 agent_perception 的最小事件数
    [SerializeField] private string[] immediateSendEventType = { "player_speak", "agent_continue_plan", "agent_perception" }; //立即发送事件类型
    [SerializeField] private bool autoSend = true; // 是否自动发送

    [Header("Inspector Debug")]
    [SerializeField, TextArea(10, 20)] private string lastBatchJson = "";

    private EventBatch currentBatch;
    private string currentSessionId;
    private float lastSendTime;
    private bool isInitialized = false;
    private bool isPerceptionInProgress = false; // 防止重复触发 agent_perception

    // 事件回调
    public delegate void EventSentCallback(bool success, string response, string error);
    public delegate void ApiResponseCallback(string response, string error);
    public static event EventSentCallback OnEventBatchSent;

    //----------------------------------

    private void OnEnable()
    {
        // 订阅各类强类型 Payload（直接沿用 EventBatch 中的载荷结构）
        EventBus.Subscribe<PlayerSpeakPayload>(OnPlayerSpeakPayload);
        EventBus.Subscribe<PlayerBuildPayload>(OnPlayerBuildPayload);
        EventBus.Subscribe<VoxelTypeCreatedPayload>(OnVoxelTypeCreatedPayload);
        EventBus.Subscribe<VoxelTypeUpdatedPayload>(OnVoxelTypeUpdatedPayload);
        EventBus.Subscribe<AgentContinuePlanPayload>(OnAgentContinuePlanPayload);
        EventBus.Subscribe<AgentPerceptionPayload>(OnAgentPerceptionPayload);
        // 兼容：订阅通用意图
        EventBus.Subscribe<EventIntent>(OnEventIntent);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<PlayerSpeakPayload>(OnPlayerSpeakPayload);
        EventBus.Unsubscribe<PlayerBuildPayload>(OnPlayerBuildPayload);
        EventBus.Unsubscribe<VoxelTypeCreatedPayload>(OnVoxelTypeCreatedPayload);
        EventBus.Unsubscribe<VoxelTypeUpdatedPayload>(OnVoxelTypeUpdatedPayload);
        EventBus.Unsubscribe<AgentContinuePlanPayload>(OnAgentContinuePlanPayload);
        EventBus.Unsubscribe<AgentPerceptionPayload>(OnAgentPerceptionPayload);
        EventBus.Unsubscribe<EventIntent>(OnEventIntent);
    }

    // 统一入队函数
    private void EnqueueEvent(string type, object payload)
    {
        if (!isInitialized || currentBatch == null) return;
        var ts = TimestampUtils.GenerateTimestamp();
        currentBatch.AddEvent(new EventData(ts, type, payload));
        lastBatchJson = currentBatch.ToJson();
    }

    // 各类 Bus 回调（统一转为内部 Add* 逻辑）
    private void OnPlayerSpeakPayload(PlayerSpeakPayload payload)
    {
        EnqueueEvent("player_speak", payload);
        Debug.Log($"[EventManager] Captured bus -> player_speak");
    }

    private void OnPlayerBuildPayload(PlayerBuildPayload payload)
    {
        EnqueueEvent("player_build", payload);
        Debug.Log($"[EventManager] Captured bus -> player_build");
    }

    private void OnVoxelTypeCreatedPayload(VoxelTypeCreatedPayload payload)
    {
        EnqueueEvent("voxel_type_created", payload);
        Debug.Log($"[EventManager] Captured bus -> voxel_type_created");
    }

    private void OnVoxelTypeUpdatedPayload(VoxelTypeUpdatedPayload payload)
    {
        EnqueueEvent("voxel_type_updated", payload);
        Debug.Log($"[EventManager] Captured bus -> voxel_type_updated");
    }

    private void OnAgentContinuePlanPayload(AgentContinuePlanPayload payload)
    {
        EnqueueEvent("agent_continue_plan", payload);
        Debug.Log($"[EventManager] Captured bus -> agent_continue_plan");
    }

    private void OnAgentPerceptionPayload(AgentPerceptionPayload payload)
    {
        EnqueueEvent("agent_perception", payload);
        Debug.Log($"[EventManager] Captured bus -> agent_perception");
    }

//----------------------------------

    // 通用意图 -> 统一入队
    private void OnEventIntent(EventIntent intent)
    {
        EnqueueEvent(intent.type, intent.payload);
        Debug.Log($"[EventManager] Captured intent -> type={intent.type}");
    }

    

    private void Awake()
    {
        // 清空旧的 EventBus 订阅，避免旧数据残留
        EventBus.Clear();
        
        // 防止在场景切换或游戏退出时被销毁
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        InitializeEventManager();
    }

    private void Update()
    {
        // 更新 session ID（从 RuntimeAIChat 获取）
        if (RuntimeAIChat.currentSessionId != null && currentBatch != null)
        {
            if (currentBatch.session_id != RuntimeAIChat.currentSessionId)
            {
                currentBatch.session_id = RuntimeAIChat.currentSessionId;
            }
        }
        
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

            // 如果存在立即发送事件类型（包括 agent_perception），直接发送
            if (isImmediateSendEventType)
            {
                SendCurrentBatch();
                return;
            }

            // 检查是否需要触发 agent_perception（仅在非立即发送事件的情况下）
            bool shouldTriggerPerception = false;
            
            // 条件1：超过间隔且达到最小事件数
            if (Time.time - lastSendTime >= batchSendInterval && 
                currentBatch.events.Count >= minEventsForPerception)
            {
                shouldTriggerPerception = true;
            }
            
            // 条件2：达到最大事件数
            if (currentBatch.events.Count >= maxEventsPerBatch)
            {
                shouldTriggerPerception = true;
            }

            // 触发 agent_perception（如果不在进行中）
            if (shouldTriggerPerception && !isPerceptionInProgress)
            {
                TriggerAgentPerception();
            }
        }
    }

    private void InitializeEventManager()
    {
        // 获取RuntimeAIChat引用（不管理session ID，只接收）
        if (runtimeAIChat == null)
        {
            runtimeAIChat = FindAnyObjectByType<RuntimeAIChat>();
            if (runtimeAIChat == null)
            {
                Debug.LogError("EventManager: 找不到RuntimeAIChat组件！");
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

        // 获取AgentCam引用
        if (agentCam == null)
        {
            agentCam = FindAnyObjectByType<AgentCam>();
            if (agentCam == null)
            {
                Debug.LogWarning("EventManager: 找不到AgentCam组件！自动触发 agent_perception 功能将不可用");
            }
        }

        // 获取当前session ID（从RuntimeAIChat）
        currentSessionId = RuntimeAIChat.currentSessionId;

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
        lastBatchJson = currentBatch.ToJson();
    }

    

    /// <summary>
    /// 立即发送当前批次（通过 RuntimeAIChat）
    /// </summary>
    public void SendCurrentBatch()
    {
        if (!isInitialized)
        {
            Debug.Log("EventManager: Not initialized, skipping send");
            return;
        }

        // 在真正发送前，请求相关生产者（如 RuntimeVoxelBuilding）立刻刷新待发送的建造事件
        EventBus.Publish(new FlushPlayerBuildRequest());

        // 刷新后如果仍然没有事件，则返回
        if (currentBatch == null || currentBatch.events.Count == 0)
        {
            Debug.Log("EventManager: No events to send");
            return;
        }

        // 从 GameStateManager 获取最新的游戏状态（按需实时采集一次）
        if (gameStateManager != null)
        {
            var currentGameState = gameStateManager.GetFreshGameStateSnapshot();
            currentBatch.SetGameState(currentGameState);
            // 注意：GameState 中的 last_commands.params_data 已经是 JSON 字符串格式
            // 最终的序列化会由 EventBatch.BuildGameStateJson 处理
            Debug.Log($"EventManager: Updated game state - Plans: {currentGameState.pending_plans?.Count ?? 0}, Commands: {currentGameState.last_commands?.Count ?? 0}");
        }
        else
        {
            Debug.LogWarning("EventManager: GameStateManager not found, skipping game state update");
        }

        // 预校验，避免 422
        if (!currentBatch.Validate(out var validationError))
        {
            Debug.LogError($"EventManager: validation failed -> {validationError}");
            return;
        }

        //Debug.Log($"EventManager: Requesting RuntimeAIChat to send batch with {currentBatch.events.Count} events");

        // 在发送前记录一份 JSON 供 Inspector 查看
        lastBatchJson = currentBatch.ToJson();

        // 请求 RuntimeAIChat 发送批次
        if (runtimeAIChat != null)
        {
            runtimeAIChat.SendEventBatch(currentBatch, OnEventBatchSentCallback);
        }
        else
        {
            Debug.LogError("EventManager: RuntimeAIChat is not available, skipping send");
            return;
        }

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
            lastBatchJson = currentBatch.ToJson();
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
    }

    /// <summary>
    /// 设置批次发送间隔
    /// </summary>
    public void SetBatchSendInterval(float interval)
    {
        batchSendInterval = Mathf.Max(1f, interval);
    }

    /// <summary>
    /// 设置每批次最大事件数
    /// </summary>
    public void SetMaxEventsPerBatch(int maxEvents)
    {
        maxEventsPerBatch = Mathf.Max(1, maxEvents);
    }

    /// <summary>
    /// 设置触发 agent_perception 的最小事件数
    /// </summary>
    public void SetMinEventsForPerception(int minEvents)
    {
        minEventsForPerception = Mathf.Max(1, minEvents);
    }

    /// <summary>
    /// 检查是否存在立即发送事件类型
    /// </summary>
    public bool IsImmediateSendEventType(string eventType)
    {
        return System.Array.IndexOf(immediateSendEventType, eventType) >= 0;
    }

    /// <summary>
    /// 触发 agent_perception 行动（自动拍摄四方位照片）
    /// </summary>
    private void TriggerAgentPerception()
    {
        if (agentCam == null)
        {
            Debug.LogWarning("EventManager: AgentCam is not available, cannot trigger agent_perception");
            // 如果 AgentCam 不可用，直接发送当前批次
            SendCurrentBatch();
            return;
        }

        if (isPerceptionInProgress)
        {
            Debug.LogWarning("EventManager: agent_perception is in progress, skipping");
            return;
        }

        isPerceptionInProgress = true;
        // 更新 lastSendTime 以避免在拍照期间重复触发
        lastSendTime = Time.time;
        Debug.Log($"[EventManager] Triggering automatic agent_perception, current batch has {currentBatch.events.Count} events");

        // 调用 AgentCam 拍摄四方位照片
        agentCam.TakeFourDirectionPhotos((photoFileNames) =>
        {
            if (photoFileNames == null || photoFileNames.Count == 0)
            {
                Debug.LogError("[EventManager] agent_perception 拍照失败，直接发送当前批次");
                isPerceptionInProgress = false;
                // 拍照失败时，直接发送当前批次（不包含 agent_perception）
                SendCurrentBatch();
                return;
            }

            // 转换为 ImageData 列表
            List<ImageData> imageDataList = new List<ImageData>();
            foreach (string fileName in photoFileNames)
            {
                if (!string.IsNullOrEmpty(fileName))
                {
                    imageDataList.Add(new ImageData(fileName: fileName));
                }
            }

            // 发布 agent_perception 事件（这会自动入队并触发立即发送）
            EventBus.Publish(new AgentPerceptionPayload(imageDataList));

            // 重置标记
            isPerceptionInProgress = false;
        });
    }

    /// <summary>
    /// 事件批次发送回调
    /// </summary>
    private void OnEventBatchSentCallback(string response, string error)
    {
        bool success = string.IsNullOrEmpty(error);

        if (!success)
        {
            Debug.LogError($"Event batch send failed: {error}");
        }

        // 触发事件回调
        OnEventBatchSent?.Invoke(success, response, error);
    }

    private void OnDestroy()
    {
        // 不再在销毁时发送批次，避免在组件和系统正在卸载时访问可能已经失效的对象
        if (isInitialized && currentBatch != null && currentBatch.events.Count > 0)
        {
            Debug.LogWarning($"EventManager: Skipping send on destroy - {currentBatch.events.Count} events will be lost. Please use ForceSendBatch() before shutdown if needed.");
        }
    }

}
