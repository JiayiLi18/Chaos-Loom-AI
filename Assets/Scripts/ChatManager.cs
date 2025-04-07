using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ChatManager : MonoBehaviour
{
    // 预制体和组件引用
    public GameObject myMessagePrefab;    // 自己消息预制体
    public GameObject otherMessagePrefab; // 他人消息预制体
    public Transform contentParent;      // 消息父物体（ScrollView的Content）
    [SerializeField] TMP_Text currentChatMessage;
    public TMP_InputField inputField;     // 输入框
    public ScrollRect scrollRect;         // 滚动视图组件（用于自动滚动）

    // 发送消息方法
    public string OnSendMessage()
    {
        string message = inputField.text;
        if (!string.IsNullOrEmpty(message))
        {
            // 生成自己消息
            CreateMessage(message, true);
            
            // 清空输入框
            inputField.text = "";
            
            // 自动滚动到底部
            StartCoroutine(ScrollToBottom());
        }

        return message; // 返回消息内容
    }

    // 接收消息方法（可从网络模块调用）
    public void OnReceiveMessage(string message)
    {
        if (!string.IsNullOrEmpty(message))
        {
            if(currentChatMessage != null)
            {
                currentChatMessage.text = message;
            }
            // 生成他人消息
            CreateMessage(message, false);
            StartCoroutine(ScrollToBottom());
        }
    }

    void CreateMessage(string text, bool isMine)
    {
        // 选择预制体
        GameObject prefab = isMine ? myMessagePrefab : otherMessagePrefab;
        
        // 实例化消息
        GameObject newMessage = Instantiate(prefab, contentParent);
        
        // 设置文本内容
        TMP_Text messageText = newMessage.GetComponentInChildren<TMP_Text>();
        if(messageText) messageText.text = text;
        
        // 强制刷新布局（重要！）
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentParent.GetComponent<RectTransform>());
    }

    System.Collections.IEnumerator ScrollToBottom()
    {
        // 等待一帧确保布局更新完成
        yield return new WaitForEndOfFrame();
        
        // 滚动到底部
        scrollRect.normalizedPosition = new Vector2(0, 0);
    }
}