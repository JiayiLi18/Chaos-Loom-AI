using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.IO;
using Unity.VisualScripting;

public class ApiClient : MonoBehaviour
{
    public string apiUrl = "http://localhost:8000/ask_text"; 
    [SerializeField] string query, imagePath;

    public void OnButtionClicked()
    {
        SendRequest(query,imagePath);
    }

    // 调用此方法发送请求
    public void SendRequest(string query, string imagePath = null)
    {
        StartCoroutine(PostRequest(query,imagePath));//for example: "path/to/your/image.jpg"
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
        }
    }
}
