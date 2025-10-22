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
    /// 创建新的体素类型（支持6个面不同纹理）
    /// </summary>
    public void CreateVoxelTypeWithFaces(string name, string description, Texture2D[] faceTextures)
    {
        if (!_isInitialized || string.IsNullOrEmpty(name) || faceTextures == null || faceTextures.Length != 6)
        {
            Debug.LogError("[VoxelSystemManager] Cannot create voxel type with faces: Invalid parameters or not initialized");
            return;
        }

        // 注册新的体素（使用第一个面作为主纹理）
        ushort newId = voxelJsonDB.AddVoxelWithFaces(name, Color.white, faceTextures, description, false);
        if (newId > 0)
        {
            var newDef = VoxelRegistry.GetDefinition(newId);
            onVoxelCreated?.Invoke(newDef);
            Debug.Log($"[VoxelSystemManager] Created voxel type '{name}' with faces, ID {newId}");
        }
    }

    /// <summary>
    /// 修改现有的体素类型（支持6个面不同纹理）
    /// </summary>
    public void ModifyVoxelTypeWithFaces(ushort typeId, string name = null, string description = null, Texture2D[] faceTextures = null)
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

        // 更新面纹理
        if (faceTextures != null && faceTextures.Length == 6)
        {
            // 更新每个面的纹理
            for (int i = 0; i < 6; i++)
            {
                if (faceTextures[i] != null)
                {
                    // 确保面纹理数组已初始化
                    if (def.faceTextures == null || def.faceTextures.Length != 6)
                    {
                        def.faceTextures = new VoxelDefinition.FaceTexture[6];
                        for (int j = 0; j < 6; j++)
                        {
                            def.faceTextures[j] = new VoxelDefinition.FaceTexture();
                        }
                    }
                    
                    def.faceTextures[i].texture = faceTextures[i];
                    def.faceTextures[i].sliceIndex = TextureLibrary.SafeRegister(faceTextures[i]);
                }
            }
            
            // 更新纹理索引
            def.UpdateTextureIfNeeded();
        }

        voxelJsonDB.SaveDatabase();
        onVoxelModified?.Invoke(def);
        Debug.Log($"[VoxelSystemManager] Modified voxel type {typeId} with faces");
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