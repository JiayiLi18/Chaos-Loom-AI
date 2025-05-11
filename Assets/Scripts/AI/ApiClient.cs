using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.IO;
using Unity.VisualScripting;
using TMPro;
using NUnit.Framework;

public class ApiClient : MonoBehaviour
{
    public string apiUrl_General = "http://127.0.0.1:8000/ask_general";
    public string apiUrl_Texture = "http://127.0.0.1:8000/generate_texture";
    [SerializeField] string query, tex_name, pprompt, denoise;
    [SerializeField] string screenshotPath = @"C:\Aalto\S4\Graduation\AI-Agent\Assets\Screenshots\Screenshot.jpg";
    [SerializeField] string textureReferencePath = "";
    [SerializeField] AICommandProcessor commandProcessorReference;//handle the execution of commands
    [SerializeField] ChatManager chatManagerReference;//handle the display of messages
    [SerializeField] TokenTracker tokenTrackerReference;//handle the counting and display of tokens

    #region Public_API
    public void OnGeneralRequest()
    {
        if (TokenTracker.limitReached)
        {
            Debug.LogWarning("Token limit reached, cannot send request.");
            return;
        }
        query = chatManagerReference.OnSendMessage();
        if (string.IsNullOrEmpty(query))
        {
            Debug.LogWarning("Query is empty!");
            return;
        }
        SendRequest_General(query, screenshotPath);
    }

    public void OnGenerateTextureRequest()
    {
        SendTextureRequest(tex_name, pprompt, textureReferencePath);
    }

    #endregion

    #region General_Process
    // 调用此方法发送请求
    void SendRequest_General(string query, string imagePath = null)
    {
        StartCoroutine(PostRequest_General(query, imagePath));//for example: "path/to/your/image.jpg"
    }

    private IEnumerator PostRequest_General(string query, string imagePath = null)
    {
        // 创建表单数据
        WWWForm form = new WWWForm();
        form.AddField("query", query);

        if (imagePath != null)
        {
            form.AddField("image_path", imagePath);
        }

        // 创建请求
        UnityWebRequest www = UnityWebRequest.Post(apiUrl_General, form);
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
            OnNetworkMessageReceived_General(responseText);
        }
    }

    void OnNetworkMessageReceived_General(string receivedJson)
    {
        // 解析消息
        ParsedMessage message = MessageParser.ParseMessage(receivedJson);

        // 有效性检查
        if (!message.IsValid)
        {
            Debug.LogWarning("收到无效消息");
            return;
        }

        // 处理消息+显示文本+执行操作
        commandProcessorReference.ProcessResponse(message.cleanAnswer);
        // 更新 Token 显示
        chatManagerReference.OnReceiveMessage(UpdateTokenDisplay(
            message.promptTokens,
            message.completionTokens,
            message.totalTokens
        ));

        // 更新 Token 计数器
        tokenTrackerReference.UpdateTokenUsage(message.promptTokens, message.completionTokens);
    }

    private string UpdateTokenDisplay(int prompt, int completion, int total)
    {

        string tokenDisplayText = string.Format(
            "Token Usage:\n" +
            "Prompt: {0}\n" +
            "Completion: {1}\n" +
            "Total: {2}",
            prompt, completion, total
        );

        return tokenDisplayText;
    }
    #endregion

    #region Texture_Process
    void SendTextureRequest(string tex_name, string pprompt, string imagePath = null, string denoise = "1.0")
    {
        StartCoroutine(PostRequest_Texture(tex_name, pprompt, imagePath, denoise));
    }

    private IEnumerator PostRequest_Texture(string tex_name, string pprompt, string imagePath = null, string denoise = "1.0")
    {
        // 创建表单数据
        WWWForm form = new WWWForm();
        form.AddField("image_path", imagePath);
        form.AddField("texture_name", tex_name);
        form.AddField("positive_prompt", pprompt);
        form.AddField("denoise_strength", denoise);

        // 创建请求
        UnityWebRequest www = UnityWebRequest.Post(apiUrl_Texture, form);
        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Error: " + www.error);
        }
        else
        {
            // 处理响应
            string responseText = www.downloadHandler.text;
            Debug.Log("Texture Response: " + responseText);
            OnNetworkMessageReceived_Texture(responseText);
        }
    }

    private void OnNetworkMessageReceived_Texture(string receivedString)
    {

    }

    #endregion
}
