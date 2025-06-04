using UnityEngine;
using Voxels;
using System.IO;
using UnityEngine.Events;

/// <summary>
/// 统一管理体素系统的核心管理器，包括创建、修改、删除体素类型等功能
/// </summary>
public class VoxelSystemManager : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private VoxelJsonDB voxelJsonDB;

    [Header("Events")]
    public UnityEvent<VoxelDefinition> onVoxelCreated = new UnityEvent<VoxelDefinition>();
    public UnityEvent<ushort> onVoxelDeleted = new UnityEvent<ushort>();
    public UnityEvent<VoxelDefinition> onVoxelModified = new UnityEvent<VoxelDefinition>();

    private string texSavePath;
    private bool _isInitialized = false;

    private void Start()
    {
        InitializeComponents();
    }

    private void InitializeComponents()
    {
        if (voxelJsonDB == null)
            voxelJsonDB = FindAnyObjectByType<VoxelJsonDB>();

        // 设置贴图保存路径为和TextureLibrary一样的Resources路径
        texSavePath = TextureLibrary.TextureSavePath;
            
        _isInitialized = true;
    }

    /// <summary>
    /// 创建新的体素类型
    /// </summary>
    public void CreateVoxelType(string name, string description, Texture2D texture)
    {
        if (!_isInitialized || string.IsNullOrEmpty(name) || texture == null)
        {
            Debug.LogError("[VoxelSystemManager] Cannot create voxel type: Invalid parameters or not initialized");
            return;
        }

        // 生成唯一的文件名：原名字_时间戳
        string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string uniqueTexName = $"{name}_{timestamp}";
        texture.name = uniqueTexName;

        // 确保目录存在
        if (!Directory.Exists(texSavePath))
        {
            Directory.CreateDirectory(texSavePath);
        }

        // 保存贴图
        byte[] pngData = texture.EncodeToPNG();
        string texPath = Path.Combine(texSavePath, uniqueTexName + ".png");
        File.WriteAllBytes(texPath, pngData);
        
#if UNITY_EDITOR
        UnityEditor.AssetDatabase.Refresh();
#endif

        // 注册新的体素
        ushort newId = voxelJsonDB.AddVoxel(name, Color.white, texture, description, false);
        if (newId > 0)
        {
            var newDef = VoxelRegistry.GetDefinition(newId);
            onVoxelCreated?.Invoke(newDef);
            Debug.Log($"[VoxelSystemManager] Created voxel type '{name}' with ID {newId}");
        }
    }

    /// <summary>
    /// 修改现有的体素类型
    /// </summary>
    public void ModifyVoxelType(ushort typeId, string name = null, string description = null, Texture2D newTexture = null)
    {
        if (!_isInitialized)
        {
            Debug.LogError("[VoxelSystemManager] Not initialized!");
            return;
        }

        var def = VoxelRegistry.GetDefinition(typeId);
        if (def == null)
        {
            Debug.LogError($"[VoxelSystemManager] VoxelDefinition with ID {typeId} not found!");
            return;
        }

        // 更新名称和描述
        if (!string.IsNullOrEmpty(name))
        {
            def.name = name;
            def.displayName = name;
        }

        if (!string.IsNullOrEmpty(description))
        {
            def.description = description;
        }

        // 更新贴图
        if (newTexture != null)
        {
            string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string uniqueTexName = $"{def.name}_{timestamp}";
            newTexture.name = uniqueTexName;

            if (!Directory.Exists(texSavePath))
            {
                Directory.CreateDirectory(texSavePath);
            }

            byte[] pngData = newTexture.EncodeToPNG();
            string texPath = Path.Combine(texSavePath, uniqueTexName + ".png");
            File.WriteAllBytes(texPath, pngData);

            def.texture = newTexture;
            def.UpdateTextureIfNeeded();
        }

        voxelJsonDB.SaveDatabase();
        onVoxelModified?.Invoke(def);
        Debug.Log($"[VoxelSystemManager] Modified voxel type {typeId}");
    }

    /// <summary>
    /// 删除体素类型
    /// </summary>
    public void DeleteVoxelType(ushort typeId)
    {
        if (!_isInitialized)
        {
            Debug.LogError("[VoxelSystemManager] Not initialized!");
            return;
        }

        if (voxelJsonDB.DeleteVoxel(typeId))
        {
            onVoxelDeleted?.Invoke(typeId);
            Debug.Log($"[VoxelSystemManager] Deleted voxel type with ID {typeId}");
        }
    }

    /// <summary>
    /// 从RenderTexture创建Texture2D
    /// </summary>
    public Texture2D CreateTextureFromRenderTexture(RenderTexture renderTexture)
    {
        if (renderTexture == null)
        {
            Debug.LogError("[VoxelSystemManager] RenderTexture is null!");
            return null;
        }

        Texture2D texture2D = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGBA32, false);
        texture2D.filterMode = FilterMode.Point;
        texture2D.wrapMode = TextureWrapMode.Repeat;
        
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = renderTexture;
        
        texture2D.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
        texture2D.Apply();

        RenderTexture.active = previous;
        return texture2D;
    }
} 