using UnityEngine;
using Voxels;
using UnityEngine.EventSystems;
using System;
using System.IO;

/// <summary>
/// 运行时体素创建器，负责处理体素创建的业务逻辑
/// 参考RuntimeAIChat的模式，通过引用UI组件获取数据
/// 
/// 核心职责：
/// 1. 从VoxelEditingUI获取颜色数据
/// 2. 生成纹理并创建体素
/// 3. 处理创建成功/失败的回调
/// 
/// 与其他组件的关系：
/// - 引用VoxelEditingUI：获取用户输入的颜色数据
/// - 使用VoxelSystemManager：创建体素定义
/// </summary>
public class RuntimeVoxelCreator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private VoxelEditingUI voxelEditingUI;
    [SerializeField] private VoxelSystemManager voxelSystem;
    
    private bool _isInitialized = false;
    private SimpleColorTextureCache _colorTextureCache;
    
    // 静态实例，方便其他组件访问
    public static RuntimeVoxelCreator Instance { get; private set; }
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void OnDestroy()
    {
        // 清理事件订阅
        if (voxelEditingUI != null)
        {
            voxelEditingUI.OnPlayerGlobalConfirmRequested -= OnPlayerGlobalConfirmRequested;
            voxelEditingUI.OnDeleteRequested -= OnDeleteRequested;
        }
    }
    
    private void Start()
    {
        InitializeComponents();
    }
    
    private void OnEnable()
    {
        if (!_isInitialized)
        {
            InitializeComponents();
        }
        else
        {
            // 如果已经初始化，只需要重新订阅事件
            SubscribeToEvents();
        }
        // 注意：不再直接管理VoxelInventoryUI，由Tool层管理
    }
    
    private void OnDisable()
    {
        // 注意：不再直接管理VoxelInventoryUI，由Tool层管理
    }
    
    private void InitializeComponents()
    {
        if (voxelEditingUI == null)
        {
            voxelEditingUI = FindAnyObjectByType<VoxelEditingUI>();
            if (voxelEditingUI == null)
            {
                Debug.LogError("[RuntimeVoxelCreator] VoxelEditingUI not found!");
                enabled = false;
                return;
            }
        }
        
        // 订阅事件
        SubscribeToEvents();
        
        // 检查VoxelSystemManager组件
        if (voxelSystem == null)
        {
            voxelSystem = FindAnyObjectByType<VoxelSystemManager>();
            if (voxelSystem == null)
            {
                Debug.LogError("[RuntimeVoxelCreator] VoxelSystemManager not found!");
                enabled = false;
                return;
            }
        }
        
        // 初始化颜色纹理缓存
        if (_colorTextureCache == null)
        {
            _colorTextureCache = new SimpleColorTextureCache();
        }
        
        _isInitialized = true;
        //Debug.Log("[RuntimeVoxelCreator] Initialized successfully");
    }
    
    /// <summary>
    /// 订阅VoxelEditingUI的事件
    /// </summary>
    private void SubscribeToEvents()
    {
        if (voxelEditingUI != null)
        {
            // 先取消订阅避免重复
            voxelEditingUI.OnPlayerGlobalConfirmRequested -= OnPlayerGlobalConfirmRequested;
            voxelEditingUI.OnDeleteRequested -= OnDeleteRequested;
            
            // 重新订阅
            voxelEditingUI.OnPlayerGlobalConfirmRequested += OnPlayerGlobalConfirmRequested;
            voxelEditingUI.OnDeleteRequested += OnDeleteRequested;
            
            //Debug.Log("[RuntimeVoxelCreator] Subscribed to VoxelEditingUI events");
        }
    }

    // Mapping no longer needed: UI order matches VoxelDefinition order
    
    /// <summary>
    /// VoxelEditingUI全局确认事件处理
    /// </summary>
    private void OnPlayerGlobalConfirmRequested(string name, string description, Texture2D[] faceTextures, ushort voxelId)
    {
        if (string.IsNullOrEmpty(name) || faceTextures == null || faceTextures.Length != 6)
        {
            Debug.LogError("[RuntimeVoxelCreator] Invalid parameters for voxel creation/modification");
            return;
        }
        
        //Debug.Log($"[RuntimeVoxelCreator] Received voxel params: name='{name}', description='{description}', faceTextures count={faceTextures.Length}, voxelId={voxelId}");
        
        if (voxelId == 0)
        {
            // voxelId为0表示创建新体素
            ProcessVoxelCreation(name, description, faceTextures, initiator: "player");
        }
        else
        {
            // voxelId不为0表示修改现有体素
            ProcessVoxelModification(voxelId, name, description, faceTextures, initiator: "player");
        }
    }
    
    /// <summary>
    /// 处理体素创建
    /// </summary>
    private void ProcessVoxelCreation(string name, string description, Texture2D[] faceTextures, string initiator = null)
    {
        try
        {
            voxelSystem.CreateVoxelTypeWithFaces(name, description, faceTextures, initiator);
            Debug.Log($"[RuntimeVoxelCreator] ✅ Successfully created voxel '{name}'");

            //OnVoxelCreated(name);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[RuntimeVoxelCreator] Failed to create voxel '{name}': {ex.Message}");
        }
    }
    
    
    /// <summary>
    /// 处理体素修改
    /// </summary>
    private void ProcessVoxelModification(ushort voxelId, string name, string description, Texture2D[] faceTextures, string initiator = null)
    {
        // 仅当真的有变化时才提交修改
        if (!HasVoxelChanges(voxelId, name, description, faceTextures))
        {
            Debug.Log("[RuntimeVoxelCreator] No changes detected, skip modification.");
            return;
        }

        try
        {
            voxelSystem.ModifyVoxelTypeWithFaces(voxelId, name, description, faceTextures, initiator);
            Debug.Log($"[RuntimeVoxelCreator] ✅ Successfully modified voxel '{name}' (ID: {voxelId})");

            //OnVoxelModified(name);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[RuntimeVoxelCreator] Failed to modify voxel '{name}': {ex.Message}");
        }
    }

    /// <summary>
    /// 比较提交的数据与当前定义，判断是否存在任何变化
    /// - 名称或描述不同视为变化
    /// - 面贴图：仅当传入非空且名称不同于当前时视为变化
    /// </summary>
    private bool HasVoxelChanges(ushort voxelId, string name, string description, Texture2D[] faceTextures)
    {
        var def = VoxelRegistry.GetDefinition(voxelId);
        if (def == null) return true; // 无法比对时，默认认为有变化

        string currentName = def.displayName ?? def.name ?? string.Empty;
        string newName = name ?? string.Empty;
        if (!string.Equals(currentName, newName, StringComparison.Ordinal)) return true;

        string currentDesc = def.description ?? string.Empty;
        string newDesc = description ?? string.Empty;
        if (!string.Equals(currentDesc, newDesc, StringComparison.Ordinal)) return true;

        if (faceTextures != null && faceTextures.Length == 6)
        {
            for (int i = 0; i < 6; i++)
            {
                var incoming = faceTextures[i];
                if (incoming == null || string.IsNullOrEmpty(incoming.name))
                {
                    // 视为“不修改该面”
                    continue;
                }

                string currentFaceName = string.Empty;
                if (def.faceTextures != null && def.faceTextures.Length > i && def.faceTextures[i] != null && def.faceTextures[i].texture != null)
                {
                    currentFaceName = def.faceTextures[i].texture.name ?? string.Empty;
                }

                if (!string.Equals(incoming.name, currentFaceName, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false; // 无任何变化
    }
    
    
    /// <summary>
    /// 体素创建成功后的回调
    /// </summary>
    private void OnVoxelCreated(string voxelName)
    {
        // 可以在这里添加创建成功后的处理逻辑
        // 比如刷新UI、显示通知等
        Debug.Log($"[RuntimeVoxelCreator] Voxel '{voxelName}' created successfully!");
    }
    
    /// <summary>
    /// 体素修改成功后的回调（从VoxelDefinition）
    /// </summary>
    private void OnVoxelModified(string voxelName)
    {
        // 可以在这里添加修改成功后的处理逻辑
        // 比如刷新UI、显示通知等
        //还可以写入event发给ai agent，告诉他体素修改了
        Debug.Log($"[RuntimeVoxelCreator] Voxel '{voxelName}' modified successfully!");
    }
    
    /// <summary>
    /// 删除按钮事件处理
    /// </summary>
    private void OnDeleteRequested(ushort voxelId)
    {
        if (voxelId == 0)
        {
            Debug.LogWarning("[RuntimeVoxelCreator] Cannot delete voxel with ID 0");
            return;
        }
        
        try
        {
            voxelSystem.DeleteVoxelType(voxelId);
            Debug.Log($"[RuntimeVoxelCreator] ✅ Successfully deleted voxel ID {voxelId}");
            //OnVoxelDeleted(voxelId);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[RuntimeVoxelCreator] Failed to delete voxel ID {voxelId}: {ex.Message}");
        }
    }
    
    
    #region Public Methods for Command Executors
    
    /// <summary>
    /// 公共方法：创建体素类型（供命令执行器使用，initiator为"agent"）
    /// 接收Texture2D数组
    /// </summary>
    public void CreateVoxelTypeForAgent(string name, string description, Texture2D[] faceTextures)
    {
        ProcessVoxelCreation(name, description, faceTextures, initiator: "agent");
    }
    
    /// <summary>
    /// 公共方法：创建体素类型（供命令执行器使用，initiator为"agent"）
    /// 接收纹理路径字符串数组，自动处理颜色纹理生成
    /// </summary>
    public void CreateVoxelTypeForAgent(string name, string description, string[] faceTexturePaths)
    {
        Texture2D[] faceTextures = ConvertFaceTexturePaths(faceTexturePaths);
        ProcessVoxelCreation(name, description, faceTextures, initiator: "agent");
    }
    
    /// <summary>
    /// 公共方法：修改体素类型（供命令执行器使用，initiator为"agent"）
    /// 接收Texture2D数组
    /// </summary>
    public void ModifyVoxelTypeForAgent(ushort voxelId, string name, string description, Texture2D[] faceTextures)
    {
        ProcessVoxelModification(voxelId, name, description, faceTextures, initiator: "agent");
    }
    
    /// <summary>
    /// 公共方法：修改体素类型（供命令执行器使用，initiator为"agent"）
    /// 接收纹理路径字符串数组，自动处理颜色纹理生成
    /// </summary>
    public void ModifyVoxelTypeForAgent(ushort voxelId, string name, string description, string[] faceTexturePaths)
    {
        Texture2D[] faceTextures = ConvertFaceTexturePaths(faceTexturePaths);
        ProcessVoxelModification(voxelId, name, description, faceTextures, initiator: "agent");
    }
    
    /// <summary>
    /// 将纹理路径字符串数组转换为Texture2D数组
    /// 统一处理RGB颜色格式的纹理生成
    /// </summary>
    private Texture2D[] ConvertFaceTexturePaths(string[] faceTexturePaths)
    {
        if (faceTexturePaths == null || faceTexturePaths.Length != 6)
        {
            Debug.LogWarning("[RuntimeVoxelCreator] Invalid face texture paths array, using default");
            return CreateDefaultTextures();
        }
        
        Texture2D[] textures = new Texture2D[6];
        
        for (int i = 0; i < 6; i++)
        {
            if (!string.IsNullOrEmpty(faceTexturePaths[i]))
            {
                // 检查是否是RGB颜色格式的文件名（如"255+127+80.png"）
                string colorName = ExtractColorNameFromPath(faceTexturePaths[i]);
                if (colorName != null)
                {
                    // 使用颜色纹理缓存生成或获取纹理
                    textures[i] = _colorTextureCache.GetOrCreateColorTexture(colorName);
                    if (textures[i] == null)
                    {
                        Debug.LogWarning($"[RuntimeVoxelCreator] Failed to generate color texture from '{colorName}', using default");
                        textures[i] = CreateDefaultTexture(Color.white);
                    }
                    else if (string.IsNullOrEmpty(textures[i].name))
                    {
                        // 确保纹理名称正确（防御性编程）
                        textures[i].name = colorName;
                    }
                }
                else
                {
                    // 尝试从Resources加载
                    textures[i] = Resources.Load<Texture2D>(faceTexturePaths[i]);
                    
                    // 如果Resources加载失败，尝试从Resources/VoxelTextures加载（去掉扩展名）
                    if (textures[i] == null)
                    {
                        string resourcePath = $"VoxelTextures/{Path.GetFileNameWithoutExtension(faceTexturePaths[i])}";
                        textures[i] = Resources.Load<Texture2D>(resourcePath);
                    }
                    
                    // 如果Resources加载失败，尝试从文件系统加载
                    if (textures[i] == null && File.Exists(faceTexturePaths[i]))
                    {
                        byte[] data = File.ReadAllBytes(faceTexturePaths[i]);
                        textures[i] = new Texture2D(2, 2);
                        textures[i].LoadImage(data);
                    }
                    
                    // 如果所有方法都失败，使用默认纹理
                    if (textures[i] == null)
                    {
                        textures[i] = CreateDefaultTexture(Color.white);
                    }
                }
            }
            else
            {
                textures[i] = CreateDefaultTexture(Color.white);
            }
        }
        
        return textures;
    }
    
    /// <summary>
    /// 从文件路径中提取RGB颜色名称（如果路径是颜色格式）
    /// 例如："255+127+80.png" -> "255+127+80"
    /// </summary>
    private string ExtractColorNameFromPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return null;
        
        // 获取文件名（去掉路径和扩展名）
        string fileName = Path.GetFileNameWithoutExtension(path);
        
        // 检查是否是RGB颜色格式（数字+数字+数字）
        string[] parts = fileName.Split('+');
        if (parts.Length == 3)
        {
            // 验证每个部分都是数字
            if (int.TryParse(parts[0], out int r) &&
                int.TryParse(parts[1], out int g) &&
                int.TryParse(parts[2], out int b))
            {
                // 验证RGB值范围（0-255）
                if (r >= 0 && r <= 255 && g >= 0 && g <= 255 && b >= 0 && b <= 255)
                {
                    return fileName; // 返回颜色名称（如"255+127+80"）
                }
            }
        }
        
        return null; // 不是颜色格式
    }
    
    /// <summary>
    /// 创建默认纹理数组
    /// </summary>
    private Texture2D[] CreateDefaultTextures()
    {
        Texture2D[] textures = new Texture2D[6];
        for (int i = 0; i < 6; i++)
        {
            textures[i] = CreateDefaultTexture(Color.white);
        }
        return textures;
    }
    
    /// <summary>
    /// 创建默认纯色纹理
    /// </summary>
    private Texture2D CreateDefaultTexture(Color color)
    {
        Texture2D texture = new Texture2D(64, 64);
        Color[] pixels = new Color[64 * 64];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = color;
        }
        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }
    
    #endregion
}
