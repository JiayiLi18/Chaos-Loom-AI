using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ChatUI : MonoBehaviour
{
    // 预制体和组件引用
    [SerializeField] GameObject chatUI;
    public GameObject myMessagePrefab;    // 自己消息预制体
    public GameObject otherMessagePrefab; // 他人消息预制体
    [SerializeField] Transform contentParent;      // 消息父物体（ScrollView的Content）
    [SerializeField] TMP_Text currentChatMessage;
    public TMP_InputField inputField;     // 输入框
    [SerializeField] public Button sendButton;
    [SerializeField] public Toggle photoInventoryToggle; //用于选择是否使用截图, 从runtimeAIChat中获取
    [SerializeField] public RawImage currentPhoto; //显示当前截图preview
    [SerializeField] ScrollRect scrollRect;
    private bool _isInitialized = false;
    private GameObject _waitingMessage; // 用于存储等待消息的引用

    private void InitializeComponents()
    {
        if (sendButton != null)
        {
            sendButton.onClick.AddListener(OnSendMessage);
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
        if (!string.IsNullOrEmpty(message))
        {
            // 生成自己消息
            CreateMessage(message, true);
            // 清空输入框
            inputField.text = "";
            CreateWaitingMessage();
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

    GameObject CreateMessage(string text, bool isMine)
    {
        // 选择预制体
        GameObject prefab = isMine ? myMessagePrefab : otherMessagePrefab;

        // 实例化消息
        GameObject newMessage = Instantiate(prefab, contentParent);

        // 设置文本内容
        TMP_Text messageText = newMessage.GetComponentInChildren<TMP_Text>();
        if (messageText) messageText.text = text;

        StartCoroutine(ScrollToBottom());
        
        return newMessage;
    }

    // 接收消息方法（可从网络模块调用）
    public void OnReceiveMessage(string message)
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

            // 生成他人消息
            CreateMessage(message, false);
        }
    }

    System.Collections.IEnumerator ScrollToBottom()
    {
        // 等待一帧确保布局更新完成
        yield return new WaitForEndOfFrame();

        // 滚动到底部
        scrollRect.normalizedPosition = new Vector2(0, 0);
    }
}