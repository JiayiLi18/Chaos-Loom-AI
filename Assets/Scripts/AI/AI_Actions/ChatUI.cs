using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ChatUI : MonoBehaviour
{
    // 预制体和组件引用
    [SerializeField] GameObject chatUI;
    [Header("Prefabs")]
    public GameObject myMessagePrefab;    // 自己消息预制体
    public GameObject otherMessagePrefab; // 他人消息预制体
    [SerializeField] private GameObject messagePhotoPrefab; // 消息中的照片预制体
    public string currentMyMessage; // 当前自己消息
    [SerializeField] Transform contentParent;      // 消息父物体（ScrollView的Content）
    [SerializeField] TMP_Text currentChatMessage;
    [Header("UI Elements")]
    [SerializeField] TMP_InputField inputField;     // 输入框
    [SerializeField] public Button sendButton;
    [SerializeField] public Button newSessionButton; // New session button
    [SerializeField] public Toggle photoInventoryToggle; //用于选择是否使用截图, 从runtimeAIChat中获取
    [SerializeField] public RawImage currentPhoto; //显示当前截图preview
    [SerializeField] ScrollRect scrollRect;
    private GameObject _waitingMessage; // 用于存储等待消息的引用
    [SerializeField] Color systemMessageColor;
    private bool _isInitialized = false;

    private void Awake()
    {
        InitializeComponents();
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
                    // 防止回车键产生换行
                    inputField.DeactivateInputField();
                }
            });
        }
        else
        {
            Debug.LogError("Input Field is not assigned in ChatUI!");
        }

        _isInitialized = true;
    }

    void OnEnable()
    {
        if(!_isInitialized)
        {
            InitializeComponents();
        }
        if (chatUI != null)
        {
            chatUI.SetActive(true);
        }
    }

    private void OnDisable()
    {
        if (chatUI != null)
        {
            chatUI.SetActive(false);
        }
    }
    // 发送消息方法
    public void OnSendMessage()
    {
        string message = inputField.text;
        currentMyMessage = message;
        if (!string.IsNullOrEmpty(message))
        {
            // 获取当前选中的照片
            Texture2D currentPhotoTexture = photoInventoryToggle.isOn ? currentPhoto.texture as Texture2D : null;
            
            // 生成自己消息
            CreateMessage(message, true, currentPhotoTexture);
            CreateWaitingMessage();
            // 清空输入框
            inputField.text = "";
        }
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

    public void OnReceiveMessage(string message, bool isSystemMessage = false)
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
        if (photo != null && isMine && messagePhotoPrefab != null)
        {
            GameObject photoObj = Instantiate(messagePhotoPrefab, contentParent);
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
}