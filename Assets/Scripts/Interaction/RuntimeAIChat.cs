using UnityEngine;
using System.IO;

/// <summary>
/// 中央管理器 - 管理所有子脚本，负责消息流程和 session ID
/// </summary>
public class RuntimeAIChat : MonoBehaviour
{
    // 前向声明委托，避免循环依赖
    public delegate void ApiResponseCallback(string response, string error);
    
    // 使用 ApiClient 的委托类型
    private ApiClient.ApiResponseCallback _apiClientCallback;
    
    [Header("References")]
    [SerializeField] private ChatUI chatUI;
    [SerializeField] private PhotoInventoryUI photoInventoryUI;
    [SerializeField] private CommandManager commandManager;
    [SerializeField] private ApiClient apiClient;
    [SerializeField] private EventManager eventManager;
    private GameStateManager gameStateManager;

    private bool _isInitialized = false;
    public static string currentSessionId = null;

    private void Start()
    {
        if(!_isInitialized)
        {
            InitializeComponents();
        }
    }

    private void OnEnable()
    {
        if (!_isInitialized)
        {
            InitializeComponents();
        }
        
        
        if (chatUI != null)
        {
            chatUI.enabled = true;
        }
        if (photoInventoryUI != null)
        {
            photoInventoryUI.enabled = true;
        }
    }

    private void OnDisable()
    {
        if (chatUI != null)
        {
            chatUI.enabled = false;
        }
        if (photoInventoryUI != null)
        {
            photoInventoryUI.enabled = false;
        }
    }

    private void InitializeComponents()
    {
        // 直接使用 FolderPath 生成路径
        string referenceImagePath = Path.Combine(Application.persistentDataPath, PhotoInventoryUI.FolderPath);
        //Debug.Log("screenshotDir: " + referenceImagePath);

        // 确保截图目录存在
        if (!Directory.Exists(referenceImagePath))
        {
            Directory.CreateDirectory(referenceImagePath);
        }

        // 检查必要组件
        if (chatUI == null)
        {
            chatUI = FindAnyObjectByType<ChatUI>();
            if (chatUI == null)
            {
                Debug.LogError("找不到ChatUI组件，聊天功能将不可用");
                enabled = false;
                return;
            }
            chatUI.sendButton.onClick.AddListener(OnSendMessage);
            chatUI.newSessionButton.onClick.AddListener(() => SendChatMessage("", true));
            //TODO: 新建会话需要新建sessionID,且重置chatUI等等。
            //TODO：还有其他的prefab引用等。
        }

        if (photoInventoryUI == null)
        {
            photoInventoryUI = FindAnyObjectByType<PhotoInventoryUI>();
            if (photoInventoryUI == null)
            {
                Debug.LogWarning("找不到PhotoInventoryUI组件，照片功能将不可用");
            }
            else
            {
                photoInventoryUI.OnPhotoSelected += OnPhotoSelected;
                //TODO: onOhotoSelected改成true/false,即直接在这确认是否选择图片不需要toggle
            }
        }

        if (commandManager == null)
        {
            commandManager = FindAnyObjectByType<CommandManager>();
            if (commandManager == null)
            {
                Debug.LogWarning("找不到AICommandProcessor组件，命令处理功能将不可用");
            }
        }

        if (apiClient == null)
        {
            apiClient = FindAnyObjectByType<ApiClient>();
            if (apiClient == null)
            {
                Debug.LogError("找不到ApiClient组件，API通信功能将不可用");
                enabled = false;
                return;
            }
        }

        if (eventManager == null)
        {
            eventManager = FindAnyObjectByType<EventManager>();
            if (eventManager == null)
            {
                Debug.LogWarning("找不到EventManager组件，事件功能将不可用");
            }
        }

        // 获取 GameStateManager 引用
        if (gameStateManager == null)
        {
            gameStateManager = FindAnyObjectByType<GameStateManager>();
            if (gameStateManager == null)
            {
                Debug.LogWarning("找不到GameStateManager组件，pending plans 和 last commands 记录功能将不可用");
            }
        }

        CreateNewSession();

        _isInitialized = true;
    }

    private void CreateNewSession()
    {
        currentSessionId = $"session_{System.DateTime.Now:yyyyMMdd_HHmmss}";
        Debug.Log($"Started new chat session: {currentSessionId}");
    }

    private void OnSendMessage()
    {
        string message = chatUI.currentMyMessage;
        SendChatMessage(message, false);
    }

    private void OnPhotoSelected()
    {
        // PhotoInventoryUI 会自动管理选中的照片
        // 只需要记录日志即可
        if (photoInventoryUI != null && photoInventoryUI.CurrentTexture != null)
        {
            Debug.Log($"Photo selected: {photoInventoryUI.CurrentPhotoPath}");
        }
    }

    public void SendChatMessage(string message, bool isNewSession = false)
    {
        if (!_isInitialized)
        {
            Debug.LogWarning("System not initialized");
            return;
        }

        // If it's a new session request or we don't have a session ID
        if (isNewSession)
        {
            CreateNewSession();
            if (string.IsNullOrEmpty(message))
            {
                // If no message provided with new session, just clear the chat
                chatUI.ClearChat();
                return;
            }
        }

        // Only proceed with API call if there's a message
        if (!string.IsNullOrEmpty(message))
        {
            // 发送用户消息（这会更新UI，并触发EventBus）
            chatUI.OnSendMessage();
        }
    }

    /// <summary>
    /// 接收 EventManager 的请求，发送事件批次
    /// </summary>
    public void SendEventBatch(EventBatch eventBatch, ApiResponseCallback callback)
    {
        if (apiClient == null)
        {
            Debug.LogError("RuntimeAIChat: ApiClient 不可用");
            callback?.Invoke(null, "ApiClient not available");
            return;
        }

        //Debug.Log($"RuntimeAIChat: Sending event batch via ApiClient");
        
        // 创建一个新的回调，先处理响应，再调用原始回调
        ApiClient.ApiResponseCallback apiCallback = (response, error) =>
        {
            if (!string.IsNullOrEmpty(error))
            {
                Debug.LogError($"API request failed: {error}");
                callback?.Invoke(response, error);
                return;
            }

            // 统一处理响应：尝试解析为 PlanBatch 或 CommandBatch
            HandleApiResponse(response);
            
            // 调用原始回调
            callback?.Invoke(response, error);
        };
        
        apiClient.SendEventsRequest(eventBatch, apiCallback);
    }

    /// <summary>
    /// 统一处理API响应 - 尝试解析为PlanBatch或CommandBatch
    /// </summary>
    private void HandleApiResponse(string response)
    {
        if (string.IsNullOrEmpty(response))
        {
            return;
        }

        // 优先尝试解析为 PlanBatch（因为它有更独特的字段，更容易准确识别）
        // PlanBatch 通常是从 events API 返回的，应该优先处理
        PlanBatch planBatch = MessageParser.ParsePlanBatch(response);
        if (!string.IsNullOrEmpty(planBatch.goal_id) && !string.IsNullOrEmpty(planBatch.session_id))
        {
            //Debug.Log($"Parsed as PlanBatch: goal_id={planBatch.goal_id}, plan count={planBatch.plan?.Length ?? 0}");
            HandlePlanBatchResponse(planBatch);
            return;
        }

        // 如果不是 PlanBatch，尝试解析为 CommandBatch（命令执行是最终目标）
        CommandBatch commandBatch = MessageParser.ParseCommandBatch(response);
        if (!string.IsNullOrEmpty(commandBatch.goal_id) && !string.IsNullOrEmpty(commandBatch.session_id))
        {
            //Debug.Log($"Parsed as CommandBatch: goal_id={commandBatch.goal_id}, command count={commandBatch.commands?.Count ?? 0}");
            HandleCommandBatchResponse(commandBatch);
            return;
        }

        // 如果都不是，显示为纯文本消息
        Debug.LogWarning($"Response is neither CommandBatch nor PlanBatch, treating as text");
        HandleTextResponse(response);
    }

    /// <summary>
    /// 处理 Plan Batch 响应
    /// </summary>
    private void HandlePlanBatchResponse(PlanBatch planBatch)
    {
        Debug.Log($"Handling Plan Batch: {planBatch.goal_label}");
        
        // 直接调用 GameStateManager 记录 pending plans
        if (gameStateManager != null && planBatch.plan != null && planBatch.plan.Length > 0)
        {
            gameStateManager.AddPendingPlansFromBatch(planBatch);
        }
        
        // 检查 plan 是否为空
        if (planBatch.plan == null || planBatch.plan.Length == 0)
        {
            // 如果 plan 为空，只显示 talk_to_player 的文本消息
            if (!string.IsNullOrEmpty(planBatch.talk_to_player) && chatUI != null)
            {
                Debug.Log($"Plan is empty, displaying only talk_to_player text: {planBatch.talk_to_player}");
                chatUI.OnReceiveMessageText(planBatch.talk_to_player);
            }
            return;
        }
        
        // 如果有 plan，显示完整的 PlanUI
        if (chatUI != null)
        {
            chatUI.DisplayPlanBatch(planBatch);
        }
        
        // 可以将 plan batch 传递给 AICommandProcessor 进行处理
        if (commandManager != null)
        {
            // TODO: 实现 Plan Batch 的处理逻辑
            Debug.Log($"Plan Batch received: {planBatch.plan.Length} actions to execute");
        }
    }

    /// <summary>
    /// 接收 Plan UI 的请求，发送批准的计划
    /// </summary>
    public void SendPlanPermission(PlanPermissionRequest planPermissionRequest, ApiResponseCallback callback)
    {
        if (apiClient == null)
        {
            Debug.LogError("RuntimeAIChat: ApiClient 不可用");
            callback?.Invoke(null, "ApiClient not available");
            return;
        }

        Debug.Log($"RuntimeAIChat: Sending plan permission via ApiClient");
        
        // 存储goal label以便后续使用
        if (!string.IsNullOrEmpty(planPermissionRequest.goal_id) && !string.IsNullOrEmpty(planPermissionRequest.goal_label))
        {
            GoalLabelStorage.StoreGoalLabel(planPermissionRequest.goal_id, planPermissionRequest.goal_label);
        }
        
        // 创建一个新的回调，处理响应
        ApiClient.ApiResponseCallback apiCallback = (response, error) =>
        {
            if (!string.IsNullOrEmpty(error))
            {
                Debug.LogError($"API request failed: {error}");
                callback?.Invoke(response, error);
                return;
            }
            
            //Debug.Log($"Plan permission sent successfully: {response}");
            
            // 统一处理响应：尝试解析为 PlanBatch 或 CommandBatch
            HandleApiResponse(response);
            
            // 调用原始回调
            callback?.Invoke(response, error);
        };
        
        // 使用 PlanPermissionRequest.ToJson() 确保正确序列化，直接发送
        var json = planPermissionRequest.ToJson();
        apiClient.SendPlanPermissionRequest(json, apiCallback);
    }

    /// <summary>
    /// 处理 Command Batch 响应
    /// </summary>
    private void HandleCommandBatchResponse(CommandBatch commandBatch)
    {
        //Debug.Log($"Handling Command Batch: goal_id={commandBatch.goal_id}, command count={commandBatch.commands?.Count ?? 0}");
        
        // 检查命令是否为空
        if (commandBatch.commands == null || commandBatch.commands.Count == 0)
        {
            Debug.LogWarning("Command Batch is empty, nothing to execute");
            return;
        }
        
        // 直接调用 GameStateManager 记录 last commands
        if (gameStateManager != null)
        {
            gameStateManager.AddLastCommandsFromBatch(commandBatch);
        }
        
        // 传递给 CommandManager 处理
        if (commandManager != null)
        {
            commandManager.ProcessCommandBatch(commandBatch);
        }
        else
        {
            Debug.LogError("RuntimeAIChat: CommandManager not found, cannot process commands");
        }
    }
    
    /// <summary>
    /// 处理纯文本响应（非 Plan Batch）
    /// </summary>
    private void HandleTextResponse(string response)
    {
        if (!string.IsNullOrEmpty(response))
        {
            //Debug.Log($"Received text response: {response}");
            if (chatUI != null)
            {
                chatUI.OnReceiveMessageText(response);
            }
        }
    }

    private void HandleErrorApiResponse(string response, string error)
    {
        if (!string.IsNullOrEmpty(error))
        {
            Debug.LogError($"API request failed: {error}");
            if (chatUI != null)
            {
                chatUI.OnReceiveMessageText($"Sorry, request failed: {error}", true);
            }
            return;
        }

        // 如果没有错误但有响应，显示为文本
        if (!string.IsNullOrEmpty(response))
        {
            HandleTextResponse(response);
        }
    }

    private void OnDestroy()
    {
        if (photoInventoryUI != null)
        {
            // 取消订阅事件
            photoInventoryUI.OnPhotoSelected -= OnPhotoSelected;
        }
    }
}