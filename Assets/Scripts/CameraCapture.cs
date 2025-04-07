using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

public class CameraCapture : MonoBehaviour
{
    public Camera targetCamera;     // 要截图的相机
    public string folderPath  = "Screenshots"; // 保存文件夹名称
    public RenderTexture captureRT;// 预先创建好的 RenderTexture，分辨率即为你希望截取的尺寸

    public void Capture()
    {
        StartCoroutine(CaptureScreenshot());
    }


    public IEnumerator CaptureScreenshot()
    {
         // 等待帧渲染完成
        yield return new WaitForEndOfFrame();

        // 设置 RenderTexture 为当前激活的目标
        RenderTexture.active = captureRT;
        // 创建一个与 RenderTexture 尺寸一致的 Texture2D
        Texture2D tex = new Texture2D(captureRT.width, captureRT.height, TextureFormat.RGB24, false);
        // 从 RenderTexture 读取像素
        tex.ReadPixels(new Rect(0, 0, captureRT.width, captureRT.height), 0, 0);
        tex.Apply();

        // 重置 RenderTexture
        RenderTexture.active = null;

        // 将纹理编码为 PNG
        byte[] bytes = tex.EncodeToPNG();

        // 确保文件夹存在
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }
        // 根据时间命名文件
        string filePath = Path.Combine(folderPath, "capture_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");
        File.WriteAllBytes(filePath, bytes);
        Debug.Log("Screenshot saved to " + filePath);

        // 清理资源
        Destroy(tex);
    }

}