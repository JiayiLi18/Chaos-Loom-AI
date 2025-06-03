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
    [SerializeField] private string referenceImagePath;

    private bool _isPhotoSelected = false;

    private bool _isInitialized = false;

    private void Start()
    {
        InitializeComponents();
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
        referenceImagePath = Path.Combine(Application.persistentDataPath, PhotoInventoryUI.FolderPath);
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
                // 订阅照片选择事件
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

        _isInitialized = true;
    }

    private void OnSendMessage()
    {
        string message = chatUI.inputField.text;
        SendChatMessage(message);
    }

    private void OnPhotoToggleChanged(bool isOn)
    {
        _isPhotoSelected = isOn;
        chatUI.currentPhoto.gameObject.SetActive(isOn);
        if (isOn)
        {
            referenceImagePath = PhotoInventoryUI._currentSelectedPhotoPath;
            chatUI.currentPhoto.texture = photoInventoryUI.CurrentTexture;
        }
    }

    private void OnPhotoSelected()
    {
        Debug.Log("Photo selected event received");
        referenceImagePath = PhotoInventoryUI._currentSelectedPhotoPath;
        chatUI.currentPhoto.texture = photoInventoryUI.CurrentTexture;
    }

    public void SendChatMessage(string message)
    {
        if (!_isInitialized || string.IsNullOrEmpty(message))
        {
            Debug.LogWarning("System not initialized or message is empty");
            return;
        }
        referenceImagePath = PhotoInventoryUI._currentSelectedPhotoPath;

        // 再发送用户消息
        chatUI.OnSendMessage();

        if (_isPhotoSelected && chatUI.photoInventoryToggle.isOn)
        {
            apiClient.SendGeneralRequest(message, referenceImagePath, HandleApiResponse);
        }
        else
        {
            apiClient.SendGeneralRequest(message, "", HandleApiResponse);
        }
    }

    private void HandleApiResponse(string response, string error)
    {
        if (!string.IsNullOrEmpty(error))
        {
            Debug.LogError($"API request failed: {error}");
            chatUI.OnReceiveMessage($"Sorry, request failed: {error}");
            return;
        }

        try
        {
            // 解析响应
            ParsedMessage parsedMessage = MessageParser.ParseMessage(response);

            if (!parsedMessage.IsValid)
            {
                Debug.LogWarning("Received an invalid response");
                chatUI.OnReceiveMessage("Sorry, received an invalid response");
                return;
            }

            // 显示回答
            chatUI.OnReceiveMessage(parsedMessage.answer);

            // 处理命令
            if (commandProcessor != null)
            {
                commandProcessor.ProcessResponse(parsedMessage);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error processing response: {e.Message}");
            chatUI.OnReceiveMessage("Sorry, there was an error processing the response");
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