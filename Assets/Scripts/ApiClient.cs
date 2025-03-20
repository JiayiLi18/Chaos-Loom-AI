using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.IO;

public class ApiClient : MonoBehaviour
{
    public string apiUrl = "http://localhost:8000/ask_text"; // 替换为您的 FastAPI 接口地址

    // 调用此方法发送请求
    public void SendRequest(string query)
    {
        StartCoroutine(PostRequest(query));
    }

    private IEnumerator PostRequest(string query, Texture2D image = null)
    {
        // 创建表单数据
        WWWForm form = new WWWForm();
        form.AddField("query", query);

        if (image != null)
        {
            byte[] imageBytes = image.EncodeToPNG();
            form.AddBinaryData("image", imageBytes, "image.png", "image/png");
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
        }
    }
}
