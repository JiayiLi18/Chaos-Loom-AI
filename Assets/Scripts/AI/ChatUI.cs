using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class ChatUI : MonoBehaviour
{
    // 预制体和组件引用
    [SerializeField] GameObject logPanel;  //指logPanel
    [Header("Text Prefabs")]
    public GameObject myMessagePrefab;    // 自己消息预制体
    public GameObject myMessageImagePrefab; // 消息中的照片预制体
    public GameObject otherMessageImagePrefab; // 他人消息中的照片预制体
    public GameObject otherMessagePrefab; // 他人消息预制体
    public string currentMyMessage; // 当前自己消息
    [SerializeField] Transform contentParent;      // 消息父物体（ScrollView的Content）
    [SerializeField] TMP_Text currentChatMessage; //debug检查收到的信息用
    [Header("UI Elements")]
    [SerializeField] TMP_InputField inputField;     // 输入框
    [SerializeField] public Button sendButton;
    [SerializeField] public Button newSessionButton; // New session button
    [SerializeField] ScrollRect scrollRect;
    [Header("References")]
    [SerializeField] private PhotoInventoryUI photoInventoryUI;
    [SerializeField] private GameObject planUIPrefab; // PlanUI 的预制体
    [SerializeField] private GameObject commandUIPrefab; // CommandUI 的预制体
    private List<PlanUI> _activePlanUIs; // 所有活跃的 PlanUI 实例列表
    private List<CommandUI> _activeCommandUIs; // 所有活跃的 CommandUI 实例列表
    private GameObject _waitingMessage; // 用于存储等待消息的引用
    [Header("Other Settings")]
    [SerializeField] Color systemMessageColor;
    [SerializeField] private PlayerInput playerInput;
    private bool _isInitialized = false;
    
    // Input System 相关
    private InputAction submitAction;
    private Coroutine _reactivateCoroutine; // 用于管理重新激活协程，防止重复启动

    private void Awake()
    {
        InitializeComponents();
        InitializeInputSystem();
    }
    
    private void InitializeInputSystem()
    {
        // 获取 PlayerInput 组件（可以是同一个 GameObject 上的，或者通过 FindAnyObjectByType 查找）
        if (playerInput == null)
        {
            // 如果当前 GameObject 没有 PlayerInput，尝试查找场景中的 PlayerInput
            playerInput = FindAnyObjectByType<PlayerInput>();
        }
        
        if (playerInput != null && playerInput.actions != null)
        {
            // 获取 Submit 动作引用
            submitAction = playerInput.actions["Submit"];
        }
        else
        {
            Debug.LogWarning("ChatUI: PlayerInput component not found, Submit action will not work");
        }
    }

    private void InitializeComponents()
    {
        if (sendButton != null)
        {
            sendButton.onClick.RemoveAllListeners();
            sendButton.onClick.AddListener(OnSendMessage);
        }

        if (inputField != null)
        {
            // 移除所有已有的监听器，防止重复
            inputField.onSubmit.RemoveAllListeners();
            // 添加回车键监听
            inputField.onSubmit.AddListener((text) => {
                if (!string.IsNullOrEmpty(text))
                {
                    OnSendMessage();
                    // 注意：不再在这里调用 DeactivateInputField()，由 OnSendMessage() 统一处理
                }
            });
        }
        else
        {
            Debug.LogError("Input Field is not assigned in ChatUI!");
        }

        _activePlanUIs = new List<PlanUI>();
        _activeCommandUIs = new List<CommandUI>();
        
        _isInitialized = true;
    }

    void OnEnable()
    {
        if(!_isInitialized)
        {
            InitializeComponents();
        }
        if (logPanel != null)
        {
            logPanel.SetActive(true);
        }
        
        // 确保输入框处于正确状态
        if (inputField != null)
        {
            inputField.interactable = true;
        }
        
        // 启用并订阅 Submit 动作
        if (submitAction != null)
        {
            submitAction.Enable();
            submitAction.performed += OnSubmitPerformed;
        }
    }

    private void OnDisable()
    {
        if (logPanel != null)
        {
            logPanel.SetActive(false);
        }
        
        // 停止重新激活协程（如果正在运行）
        if (_reactivateCoroutine != null)
        {
            StopCoroutine(_reactivateCoroutine);
            _reactivateCoroutine = null;
        }
        
        // 取消订阅并禁用 Submit 动作
        if (submitAction != null)
        {
            submitAction.performed -= OnSubmitPerformed;
            submitAction.Disable();
        }
    }
    
    private void OnSubmitPerformed(InputAction.CallbackContext context)
    {
        // 只有在输入框被聚焦且有文本时才发送
        if (inputField != null && inputField.isFocused && !string.IsNullOrEmpty(inputField.text))
        {
            OnSendMessage();
        }
    }

    // 发送消息方法，必定发送的是自己的消息
    public void OnSendMessage()
    {
        string message = inputField != null ? inputField.text.Trim() : "";
        currentMyMessage = message;
        
        // 检查消息是否为空（空白字符也算空）
        if (string.IsNullOrEmpty(message))
        {
            Debug.Log("ChatUI: Ignoring empty message");
            return;
        }
        
        // 获取当前选中的照片（如果有）
        Texture2D currentPhotoTexture = null;
        string imageFileName = null;
        
        if (photoInventoryUI != null && photoInventoryUI.CurrentTexture != null)
        {
            currentPhotoTexture = photoInventoryUI.CurrentTexture as Texture2D;
            imageFileName = System.IO.Path.GetFileName(photoInventoryUI.CurrentPhotoPath);
        }
        
        // 生成自己消息
        CreateMessage(message, true, currentPhotoTexture);
        CreateWaitingMessage();
        
        // 触发 EventBus - EventManager 会自动捕获
        ImageData imageData = null;
        if (imageFileName != null)
        {
            // 构建 ImageData 对象
            string fullPath = photoInventoryUI.CurrentPhotoPath;
            imageData = new ImageData(
                fileName: imageFileName,
                filePath: fullPath
            );
        }
        EventBus.Publish(new PlayerSpeakPayload(message, imageData));
        
        // 清空输入框并重新激活（防止输入框变白）
        if (inputField != null)
        {
            // 先失活输入框，确保状态重置
            inputField.DeactivateInputField();
            inputField.text = "";
            
            // 停止之前的协程（如果存在），防止重复启动
            if (_reactivateCoroutine != null)
            {
                StopCoroutine(_reactivateCoroutine);
            }
            
            // 重新激活输入框，确保可以继续编辑
            _reactivateCoroutine = StartCoroutine(ReactivateInputField());
        }
    }
    
    /// <summary>
    /// 重新激活输入框的协程
    /// </summary>
    private System.Collections.IEnumerator ReactivateInputField()
    {
        // 等待一帧，确保 DeactivateInputField() 完成
        yield return null;
        
        // 再等待一帧，确保UI完全更新
        yield return null;
        
        if (inputField != null)
        {
            // 确保输入框可以交互
            inputField.interactable = true;
            
            // 重新激活输入框（这会触发占位符显示）
            inputField.ActivateInputField();
            
            // 确保输入框获得焦点（如果可能）
            if (!inputField.isFocused)
            {
                inputField.Select();
            }
        }
        
        _reactivateCoroutine = null;
    }

    // 创建等待消息
    public void CreateWaitingMessage()
    {
        // 如果已经存在等待消息，先删除它
        if (_waitingMessage != null)
        {
            Destroy(_waitingMessage);
        }

        // 使用已有的CreateMessage方法创建等待消息
        _waitingMessage = CreateMessage("Waiting for response...", false);
    }

    public void OnReceiveMessageText(string message, bool isSystemMessage = false)
    {
        if (!string.IsNullOrEmpty(message))
        {
            if (currentChatMessage != null)
            {
                currentChatMessage.text = message;
            }

            // 如果存在等待消息，删除它
            if (_waitingMessage != null)
            {
                Destroy(_waitingMessage);
                _waitingMessage = null;
            }

            // 生成消息
            CreateMessage(message, false, null, isSystemMessage);
        }
    }

    GameObject CreateMessage(string text, bool isMine, Texture2D photo = null, bool isSystemMessage = false)
    {
        // 如果有照片且是自己的消息，添加照片
        if (photo != null && isMine && myMessageImagePrefab != null)
        {
            GameObject photoObj = Instantiate(myMessageImagePrefab, contentParent);
            RawImage photoImage = photoObj.GetComponentInChildren<RawImage>();
            if (photoImage != null)
            {
                photoImage.texture = photo;
            }
        }
        // 选择预制体
        GameObject prefab = isMine ? myMessagePrefab : otherMessagePrefab;

        // 实例化消息
        GameObject newMessage = Instantiate(prefab, contentParent);

        // 设置文本内容和颜色
        TMP_Text messageText = newMessage.GetComponentInChildren<TMP_Text>();
        if (messageText)
        {
            messageText.text = text;
            if (isSystemMessage)
            {
                messageText.color = systemMessageColor;
            }
        }

        StartCoroutine(ScrollToBottom());
        
        return newMessage;
    }

    /// <summary>
    /// Show agent images message (four photos), in the order of front/back/left/right. The prefab needs to contain 4 RawImage components.
    /// </summary>
    /// <param name="filePaths">Full paths of four photos</param>
    /// <param name="caption">Text above the images</param>
    public void ShowAgentImagesMessage(System.Collections.Generic.List<string> filePaths, string caption = "Hey i just looked around, let's see if i can do anything here...")
    {
        if (otherMessageImagePrefab == null || contentParent == null)
        {
            CreateMessage(caption, false);
            return;
        }

        // 先实例化四图容器
        GameObject photoObj = Instantiate(otherMessageImagePrefab, contentParent);
        var rawImages = photoObj.GetComponentsInChildren<RawImage>(true);
        var captionText = photoObj.GetComponentInChildren<TMPro.TMP_Text>(true);

        // 加载并填充多张图片
        int assignCount = 0;
        if (filePaths != null)
        {
            for (int i = 0; i < filePaths.Count && i < rawImages.Length; i++)
            {
                var tex = LoadTextureFromFile(filePaths[i]);
                if (tex != null)
                {
                    rawImages[i].texture = tex;
                    assignCount++;
                }
            }
        }
        
        // 在同一 prefab 内设置文字
        if (captionText != null)
        {
            captionText.text = caption;
        }

        StartCoroutine(ScrollToBottom());
    }

    private Texture2D LoadTextureFromFile(string path)
    {
        if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
        {
            return null;
        }
        try
        {
            byte[] data = System.IO.File.ReadAllBytes(path);
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
            if (tex.LoadImage(data))
            {
                return tex;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"ChatUI: Failed to load image from {path}: {e.Message}");
        }
        return null;
    }

    System.Collections.IEnumerator ScrollToBottom()
    {
        // 等待一帧确保布局更新完成
        yield return new WaitForEndOfFrame();

        // 滚动到底部
        scrollRect.normalizedPosition = new Vector2(0, 0);
    }

    /// <summary>
    /// Clears all messages from the chat
    /// </summary>
    public void ClearChat()
    {
        if (contentParent != null)
        {
            foreach (Transform child in contentParent)
            {
                Destroy(child.gameObject);
            }
        }
        
        if (inputField != null)
        {
            inputField.text = string.Empty;
        }
    }

    /// <summary>
    /// 显示 Plan Batch（新的 PlanUI 实例会在 contentParent 下创建）
    /// </summary>
    public void DisplayPlanBatch(PlanBatch planBatch)
    {
        if (planUIPrefab == null)
        {
            Debug.LogWarning("ChatUI: planUIPrefab not assigned, cannot display plan batch");
            return;
        }

        if (contentParent == null)
        {
            Debug.LogError("ChatUI: contentParent is not assigned");
            return;
        }

        // 删除等待消息（如果存在）
        if (_waitingMessage != null)
        {
            Destroy(_waitingMessage);
            _waitingMessage = null;
        }

        // 实例化 PlanUI（不清除旧的，允许多个叠加显示）
        GameObject planUIInstance = Instantiate(planUIPrefab, contentParent);
        PlanUI planUI = planUIInstance.GetComponent<PlanUI>();

        if (planUI != null)
        {
            _activePlanUIs.Add(planUI);
            planUI.DisplayPlanBatch(planBatch);
        }
        else
        {
            Debug.LogError("ChatUI: PlanUI component not found on prefab");
            Destroy(planUIInstance);
        }
    }

    /// <summary>
    /// 显示 Command Batch（新的 CommandUI 实例会在 contentParent 下创建）
    /// </summary>
    public void DisplayCommandBatch(CommandBatch commandBatch)
    {
        if (commandUIPrefab == null)
        {
            Debug.LogWarning("ChatUI: commandUIPrefab not assigned, cannot display command batch");
            return;
        }
        
        if (contentParent == null)
        {
            Debug.LogError("ChatUI: contentParent is not assigned");
            return;
        }
        
        // 删除等待消息（如果存在）
        if (_waitingMessage != null)
        {
            Destroy(_waitingMessage);
            _waitingMessage = null;
        }
        
        // 实例化 CommandUI（不清除旧的，允许多个叠加显示）
        GameObject commandUIInstance = Instantiate(commandUIPrefab, contentParent);
        CommandUI commandUI = commandUIInstance.GetComponent<CommandUI>();
        
        if (commandUI != null)
        {
            _activeCommandUIs.Add(commandUI);
            commandUI.DisplayCommandBatch(commandBatch);
        }
        else
        {
            Debug.LogError("ChatUI: CommandUI component not found on prefab");
            Destroy(commandUIInstance);
        }
        
        StartCoroutine(ScrollToBottom());
    }
    
    /// <summary>
    /// 获取最新活跃的 PlanUI 实例
    /// </summary>
    private PlanUI GetLatestPlanUI()
    {
        if (_activePlanUIs != null && _activePlanUIs.Count > 0)
        {
            return _activePlanUIs[_activePlanUIs.Count - 1];
        }
        return null;
    }

    /// <summary>
    /// 获取确认的 Plan Items（从最新的 PlanUI）
    /// </summary>
    public PlanItem[] GetConfirmedPlans()
    {
        PlanUI latestPlanUI = GetLatestPlanUI();
        if (latestPlanUI != null)
        {
            return latestPlanUI.GetConfirmedPlans();
        }
        return new PlanItem[0];
    }

    /// <summary>
    /// 检查 Plan Item 是否被确认（从最新的 PlanUI）
    /// </summary>
    public bool IsPlanConfirmed(string planId)
    {
        PlanUI latestPlanUI = GetLatestPlanUI();
        if (latestPlanUI != null)
        {
            return latestPlanUI.IsPlanConfirmed(planId);
        }
        return false;
    }

    /// <summary>
    /// 清除所有 Plan UI 实例
    /// </summary>
    public void ClearPlanUI()
    {
        if (_activePlanUIs != null && _activePlanUIs.Count > 0)
        {
            foreach (var planUI in _activePlanUIs)
            {
                if (planUI != null)
                {
                    planUI.Clear();
                    Destroy(planUI.gameObject);
                }
            }
            _activePlanUIs.Clear();
        }
    }
}