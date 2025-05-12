using System;
using System.IO;
using UnityEngine;

/// <summary>
/// 体素纹理管理器，负责管理体素纹理sheet。
/// 
/// 核心职责：
/// 1. 加载单一的texture sheet (512x512像素)，每个体素使用16x16像素区域
/// 2. 提供texture sheet的访问接口
/// 3. 在初始化完成时触发 OnLibraryInitialized 事件
/// </summary>
[DefaultExecutionOrder(-1000)]
public sealed class TextureLibrary : MonoBehaviour
{
    public static TextureLibrary I { get; private set; }
    public Texture2D TextureSheet { get; private set; }

    [SerializeField] private string textureSheetPath = "VoxelTextures/texture_sheet.png"; // Resources路径

    /// <summary>
    /// Event triggered when TextureLibrary is fully initialized
    /// </summary>
    public static event Action OnLibraryInitialized;

    /// <summary>
    /// Returns true if the TextureLibrary singleton is initialized and ready to use
    /// </summary>
    public static bool IsInitialized => I != null && I.TextureSheet != null;

    void Awake()
    {
        if (I != null)
        {
            Debug.LogWarning("Multiple TextureLibrary instances detected. Destroying duplicate.");
            Destroy(gameObject);
            return;
        }

        I = this;
        DontDestroyOnLoad(gameObject);

        // 延迟执行资源加载，防止在 domain reload 阶段被执行
#if UNITY_EDITOR
        UnityEditor.EditorApplication.delayCall += () =>
        {
            LoadTextureSheet();

            // 触发初始化完成事件
            OnLibraryInitialized?.Invoke();
            Debug.Log("TextureLibrary initialized successfully");
        };
#else
        LoadTextureSheet();
        
        // 触发初始化完成事件
        OnLibraryInitialized?.Invoke();
        Debug.Log("TextureLibrary initialized successfully");
#endif
    }

    void OnDestroy()
    {
        if (I == this)
        {
            I = null;
        }
    }

    /// <summary>
    /// 加载texture sheet
    /// </summary>
    private void LoadTextureSheet()
    {
        string fullPath = Path.Combine(Application.dataPath, "Resources", textureSheetPath);

        try
        {
            // 1. 从文件系统读取字节数据
            byte[] fileData = File.ReadAllBytes(fullPath);

            // 2. 创建一个新的纹理，格式为RGBA32
            TextureSheet = new Texture2D(512, 512, TextureFormat.RGBA32, false);

            // 3. 加载图片数据到纹理
            if (!TextureSheet.LoadImage(fileData))
            {
                Debug.LogError($"Failed to load texture data from: {fullPath}");
                return;
            }

            // 4. 确保纹理设置正确
            TextureSheet.filterMode = FilterMode.Point;
            TextureSheet.wrapMode = TextureWrapMode.Repeat;

            // 5. 更新VoxelResources中的材质
            Voxels.VoxelResources.UpdateMaterialsWithTextureSheet(TextureSheet);

            Debug.Log($"TextureLibrary: Loaded texture sheet from {fullPath} with dimensions {TextureSheet.width}x{TextureSheet.height}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error loading texture from path {fullPath}: {ex.Message}");

        }
    }

    /// <summary>
    /// 设置使用外部提供的texture sheet路径
    /// </summary>
    public void SetTextureSheetPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return;

        textureSheetPath = path;
        LoadTextureSheet();
    }
}