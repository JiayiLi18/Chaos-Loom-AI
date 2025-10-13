using UnityEngine;
using System.IO;

/// <summary>
/// 负责AI聊天的核心功能，处理消息和命令
/// </summary>
public class RuntimeAIChat : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ChatUI chatUI;
    [SerializeField] private PhotoInventoryUI photoInventoryUI;
    [SerializeField] private AICommandProcessor commandProcessor;
    [SerializeField] private ApiClient apiClient;
    [SerializeField] private VoxelInventoryUI voxelInventoryUI;

    private bool _isInitialized = false;
    private string _currentSessionId = null;

    private void Start()
    {
        InitializeComponents();
        // Generate a new session ID when starting
        StartNewSession();
    }

    private void StartNewSession()
    {
        _currentSessionId = $"session_{System.DateTime.Now:yyyyMMdd_HHmmss}";
        Debug.Log($"Started new chat session: {_currentSessionId}");
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
        if (voxelInventoryUI != null)
        {
            voxelInventoryUI.enabled = false;
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
        if (voxelInventoryUI != null)
        {
            voxelInventoryUI.enabled = false;
        }
    }

    private void InitializeComponents()
    {
        // 直接使用 FolderPath 生成路径
        string referenceImagePath = Path.Combine(Application.persistentDataPath, PhotoInventoryUI.FolderPath);
        Debug.Log("screenshotDir: " + referenceImagePath);

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
            chatUI.photoInventoryToggle.onValueChanged.AddListener(OnPhotoToggleChanged);
            chatUI.newSessionButton.onClick.AddListener(() => SendChatMessage("", true));
            OnPhotoToggleChanged(false);
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
            }
        }

        if (commandProcessor == null)
        {
            commandProcessor = FindAnyObjectByType<AICommandProcessor>();
            if (commandProcessor == null)
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

        if (voxelInventoryUI == null)
        {
            voxelInventoryUI = FindAnyObjectByType<VoxelInventoryUI>();
            if (voxelInventoryUI == null)
            {
                Debug.LogWarning("找不到VoxelInventoryUI组件，voxel展示功能将不可用");
            }
        }

        _isInitialized = true;
    }

    private void OnSendMessage()
    {
        // 在发送消息前先检查并记录状态
        bool hasPhotoSelected = photoInventoryUI != null && photoInventoryUI.CurrentTexture != null;
        bool isToggleOn = chatUI.photoInventoryToggle.isOn;
        
        string message = chatUI.currentMyMessage;
        SendChatMessage(message, false);
    }

    private void OnPhotoToggleChanged(bool isOn)
    {
        chatUI.currentPhoto.gameObject.SetActive(isOn);
        if (isOn)
        {
            chatUI.currentPhoto.texture = photoInventoryUI.CurrentTexture;
        }
    }

    private void OnPhotoSelected()
    {
        if (photoInventoryUI != null && chatUI != null)
        {
            chatUI.currentPhoto.texture = photoInventoryUI.CurrentTexture;
            // 确保在选择照片时打开toggle
            chatUI.photoInventoryToggle.isOn = true;
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
            StartNewSession();
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
            string currentPhotoPath = "";
            bool wasToggleOn = chatUI.photoInventoryToggle.isOn;  // 保存toggle状态

            if (wasToggleOn && photoInventoryUI != null)
            {
                currentPhotoPath = photoInventoryUI.CurrentPhotoPath;
                Debug.Log($"Using photo path: {currentPhotoPath}");
            }

            // 先发送请求
            // TODO: 发送请求
            
            // 发送用户消息（这会更新UI）
            chatUI.OnSendMessage();
            
            // 如果之前是开着的，现在再关闭
            if (wasToggleOn)
            {
                chatUI.photoInventoryToggle.isOn = false;
                chatUI.currentPhoto.gameObject.SetActive(false);
            }
        }
    }

    private void HandleApiResponse(string response, string error)
    {
        if (!string.IsNullOrEmpty(error))
        {
            Debug.LogError($"API request failed: {error}");
            chatUI.OnReceiveMessage($"Sorry, request failed: {error}", true);
            return;
        }

        try
        {
            // 解析响应
            ParsedMessage parsedMessage = MessageParser.ParseMessage(response);

            if (!parsedMessage.IsValid)
            {
                Debug.LogWarning("Received an invalid response");
                chatUI.OnReceiveMessage("Sorry, received an invalid response", true);
                return;
            }

            // 显示回答
            chatUI.OnReceiveMessage(parsedMessage.answer);

            // 处理命令
            // TODO: 处理命令
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error processing response: {e.Message}");
            chatUI.OnReceiveMessage("Sorry, there was an error processing the response", true);
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