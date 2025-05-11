using System.Collections;
using System.IO;
using UnityEngine;

public class AreaHandler : MonoBehaviour
{
    [SerializeField] Rect centeredCaptureArea = new Rect(0, 0, 200, 200);
    public Rect calculatedCaptureArea;

    [SerializeField] bool showCaptureArea = true;
    public Texture2D screenshot;

    void Start()
    {
        // Define the area to capture (e.g., width and height of 200 pixels)
        float captureWidth = centeredCaptureArea.width;
        float captureHeight = centeredCaptureArea.height;
        calculatedCaptureArea = new Rect(
            centeredCaptureArea.x - captureWidth / 2, // X position, left
            centeredCaptureArea.y - captureHeight / 2, // Y position, top
            captureWidth, // Width
            captureHeight // Height
        );
    }

    public bool IsPointInRect(Vector2 point)
    {
        // 检查点是否在 Rect 内
        return calculatedCaptureArea.Contains(point);
    }

    public IEnumerator TakeScreenshot(string savePath, Rect captureArea)
    {
        yield return new WaitForEndOfFrame();

        // 创建 Texture2D 并读取像素
        screenshot = new Texture2D((int)captureArea.width, (int)captureArea.height, TextureFormat.RGB24, false);
        screenshot.ReadPixels(captureArea, 0, 0);
        screenshot.Apply();

        // 确保目录存在
        string directoryPath = Path.GetDirectoryName(savePath);
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        // 保存截图
        byte[] bytes = screenshot.EncodeToJPG();
        File.WriteAllBytes(savePath, bytes);

        Debug.Log($"Screenshot saved to: {savePath}");
    }

    void OnGUI()
    {
        if (showCaptureArea)
        {
            // Draw the outline of the capture area
            GUI.color = Color.green;
            GUI.DrawTexture(new Rect(calculatedCaptureArea.x, calculatedCaptureArea.y, calculatedCaptureArea.width, 1), Texture2D.whiteTexture); // Top
            GUI.DrawTexture(new Rect(calculatedCaptureArea.x, calculatedCaptureArea.y, 1, calculatedCaptureArea.height), Texture2D.whiteTexture); // Left
            GUI.DrawTexture(new Rect(calculatedCaptureArea.x, calculatedCaptureArea.y + calculatedCaptureArea.height - 1, calculatedCaptureArea.width, 1), Texture2D.whiteTexture); // Bottom
            GUI.DrawTexture(new Rect(calculatedCaptureArea.x + calculatedCaptureArea.width - 1, calculatedCaptureArea.y, 1, calculatedCaptureArea.height), Texture2D.whiteTexture); // Right
        }
    }

    void OnDrawGizmos()
    {
        if (showCaptureArea)
        {
            // Define the area to capture (e.g., width and height of 200 pixels)
            float captureWidth = centeredCaptureArea.width;
            float captureHeight = centeredCaptureArea.height;
            calculatedCaptureArea = new Rect(
                centeredCaptureArea.x - captureWidth / 2, // X position, left
                centeredCaptureArea.y - captureHeight / 2, // Y position, top
                captureWidth, // Width
                captureHeight // Height
            );
            // Draw the outline of the capture area in the editor
            Gizmos.color = Color.red;
            Gizmos.DrawLine(new Vector3(calculatedCaptureArea.x, calculatedCaptureArea.y, 0), new Vector3(calculatedCaptureArea.x + calculatedCaptureArea.width, calculatedCaptureArea.y, 0)); // Top
            Gizmos.DrawLine(new Vector3(calculatedCaptureArea.x, calculatedCaptureArea.y, 0), new Vector3(calculatedCaptureArea.x, calculatedCaptureArea.y + calculatedCaptureArea.height, 0)); // Left
            Gizmos.DrawLine(new Vector3(calculatedCaptureArea.x, calculatedCaptureArea.y + calculatedCaptureArea.height, 0), new Vector3(calculatedCaptureArea.x + calculatedCaptureArea.width, calculatedCaptureArea.y + calculatedCaptureArea.height, 0)); // Bottom
            Gizmos.DrawLine(new Vector3(calculatedCaptureArea.x + calculatedCaptureArea.width, calculatedCaptureArea.y, 0), new Vector3(calculatedCaptureArea.x + calculatedCaptureArea.width, calculatedCaptureArea.y + calculatedCaptureArea.height, 0)); // Right
        }
    }
}
