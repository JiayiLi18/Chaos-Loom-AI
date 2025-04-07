using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.IO;
using Unity.VisualScripting;
using TMPro;
using NUnit.Framework;

public class ApiClient : MonoBehaviour
{

    public string apiUrl = "http://localhost:8000/ask_text";
    [SerializeField] string query, imagePath = @"C:\Aalto\S4\Graduation\AI-Agent\Assets\Screenshots\Screenshot.jpg";
    [SerializeField] ChatManager chatManagerReference;
    [SerializeField] TokenTracker tokenTrackerReference;

    public void OnButtionClicked()
    {
        if(TokenTracker.limitReached)
        {
            Debug.LogWarning("Token limit reached, cannot send request.");
            return;
        }
        query = chatManagerReference.OnSendMessage();
        if(string.IsNullOrEmpty(query))
        {
            Debug.LogWarning("Query is empty!");
            return;
        }
        SendRequest(query, imagePath);
    }

    // 调用此方法发送请求
    public void SendRequest(string query, string imagePath = null)
    {
        StartCoroutine(PostRequest(query, imagePath));//for example: "path/to/your/image.jpg"
    }

    private IEnumerator PostRequest(string query, string imagePath = null)
    {
        // 创建表单数据
        WWWForm form = new WWWForm();
        form.AddField("query", query);

        if (imagePath != null)
        {
            form.AddField("image_path", imagePath);
        }

        // 创建请求
        UnityWebRequest www = UnityWebRequest.Post(apiUrl, form);
        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Error: " + www.error);
        }
        else
        {
            // 处理响应
            string responseText = www.downloadHandler.text;
            Debug.Log("Response: " + responseText);
            // 在此处解析 JSON 响应并更新游戏逻辑
            OnNetworkMessageReceived(responseText);
        }
    }

    void OnNetworkMessageReceived(string receivedJson)
    {
        // 解析消息
        ParsedMessage message = MessageParser.ParseMessage(receivedJson);

        // 有效性检查
        if (!message.IsValid)
        {
            Debug.LogWarning("收到无效消息");
            return;
        }
        
        // 更新消息到UI
        chatManagerReference.OnReceiveMessage(message.cleanAnswer);
        // 更新 Token 显示
        chatManagerReference.OnReceiveMessage(UpdateTokenDisplay(
            message.promptTokens, 
            message.completionTokens, 
            message.totalTokens
        ));

        // 更新 Token 计数器
        tokenTrackerReference.UpdateTokenUsage(message);
    }

    private string UpdateTokenDisplay(int prompt, int completion, int total)
    {
        
        string tokenDisplayText =  string.Format(
            "Token Usage:\n" +
            "Prompt: {0}\n" +
            "Completion: {1}\n" +
            "Total: {2}", 
            prompt, completion, total
        );

        return tokenDisplayText;
    }
}
