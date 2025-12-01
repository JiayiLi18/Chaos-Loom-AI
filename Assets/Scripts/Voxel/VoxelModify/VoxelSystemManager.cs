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
    public void CreateVoxelTypeWithFaces(string name, string description, Texture2D[] faceTextures, string initiator = null)
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

            // 发布体素类型创建事件（仅当initiator为"player"时触发）
            if (initiator == "player")
            {
                var createdPayload = new VoxelTypeCreatedPayload(
                    new VoxelTypeData(
                        id: newId.ToString(),
                        name: newDef.displayName ?? name,
                        description: newDef.description,
                        faceTextures: ExtractFaceTextures(newDef)
                    ),
                    initiator
                );
                EventBus.Publish(createdPayload);
            }
        }
    }

    /// <summary>
    /// 修改现有的体素类型（支持6个面不同纹理）
    /// </summary>
    public void ModifyVoxelTypeWithFaces(ushort typeId, string name = null, string description = null, Texture2D[] faceTextures = null, string initiator = null)
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

        // Debug: 记录修改前的状态
        Debug.Log($"[VoxelSystemManager] ===== Modifying voxel type {typeId} =====");
        Debug.Log($"[VoxelSystemManager] Current face textures before modification:");
        for (int i = 0; i < 6; i++)
        {
            var face = def.faceTextures[i];
            //Debug.Log($"  Face {i}: {(face != null && face.texture != null ? face.texture.name : "null")}");
        }

        // 记录修改前的数据
        var oldVoxelType = new VoxelTypeData(
            typeId.ToString(),
            def.displayName ?? def.name,
            def.description,
            ExtractFaceTextures(def)
        );

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
            // 确保面纹理数组已初始化
            if (def.faceTextures == null || def.faceTextures.Length != 6)
            {
                def.faceTextures = new VoxelDefinition.FaceTexture[6];
                for (int j = 0; j < 6; j++)
                {
                    def.faceTextures[j] = new VoxelDefinition.FaceTexture();
                }
            }
            
            // 更新每个面的纹理（只更新传入的非null纹理）
            for (int i = 0; i < 6; i++)
            {
                var incoming = faceTextures[i];
                if (incoming != null && !string.IsNullOrEmpty(incoming.name))
                {
                    def.faceTextures[i].texture = incoming;
                    def.faceTextures[i].sliceIndex = TextureLibrary.SafeRegister(incoming);
                    Debug.Log($"[VoxelSystemManager] Updated face {i} texture: {incoming.name}");
                }
                else
                {
                    // 传入为 null 或名字为空，视为不修改，保留原有纹理
                    Debug.Log($"[VoxelSystemManager] Face {i} skipped (null/empty), keeping existing texture: {def.faceTextures[i]?.texture?.name ?? "none"}");
                }
            }
            
            // 更新纹理索引
            def.UpdateTextureIfNeeded();
        }
        
        // Debug: 记录修改后的状态
        Debug.Log($"[VoxelSystemManager] Face textures after modification:");
        for (int i = 0; i < 6; i++)
        {
            var face = def.faceTextures[i];
            Debug.Log($"  Face {i}: {(face != null && face.texture != null ? face.texture.name : "null")}");
        }

        // 同步更新到数据库（修改后）
        if (voxelJsonDB != null)
        {
            voxelJsonDB.SyncVoxelToDatabase(typeId, def);
        }

        voxelJsonDB.SaveDatabase();
        onVoxelModified?.Invoke(def);
        Debug.Log($"[VoxelSystemManager] Modified voxel type {typeId} with faces");

        // 发布体素类型更新事件（仅当initiator为"player"时触发）
        if (initiator == "player")
        {
            var newVoxelType = new VoxelTypeData(
                typeId.ToString(),
                def.displayName ?? def.name,
                def.description,
                ExtractFaceTextures(def)
            );
            var updatedPayload = new VoxelTypeUpdatedPayload(
                typeId.ToString(),
                oldVoxelType,
                newVoxelType
            );
            EventBus.Publish(updatedPayload);
        }
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
            // 为删除事件准备旧数据（如果能获取到）
            VoxelDefinition defBeforeDelete = VoxelRegistry.GetDefinition(typeId);
            VoxelTypeData oldVoxelType = null;
            if (defBeforeDelete != null)
            {
                oldVoxelType = new VoxelTypeData(
                    id: typeId.ToString(),
                    name: defBeforeDelete.displayName ?? defBeforeDelete.name,
                    description: defBeforeDelete.description,
                    faceTextures: ExtractFaceTextures(defBeforeDelete)
                );
            }

            onVoxelDeleted?.Invoke(typeId);
            Debug.Log($"[VoxelSystemManager] Deleted voxel type with ID {typeId}");

            // 发布体素类型更新事件，表示删除（new_voxel_type = null）
            var deletedPayload = new VoxelTypeUpdatedPayload(
                voxelId: typeId.ToString(),
                oldVoxelType: oldVoxelType,
                newVoxelType: null
            );
            EventBus.Publish(deletedPayload);
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

    /// <summary>
    /// 从VoxelDefinition中提取面纹理信息
    /// </summary>
    private string[] ExtractFaceTextures(VoxelDefinition def)
    {
        string[] faceTextures = new string[6];
        
        if (def.faceTextures != null && def.faceTextures.Length >= 6)
        {
            for (int i = 0; i < 6; i++)
            {
                if (def.faceTextures[i] != null && def.faceTextures[i].texture != null)
                {
                    // 使用纹理名称作为标识
                    faceTextures[i] = def.faceTextures[i].texture.name;
                    //Debug.Log($"[VoxelSystemManager] Extracted face {i} texture: {faceTextures[i]}");
                }
                else
                {
                    // 如果面纹理为空，设置为空字符串
                    faceTextures[i] = "";
                    //Debug.Log($"[VoxelSystemManager] Face {i} has no texture (null)");
                }
            }
        }
        else
        {
            // 如果没有面纹理，所有面都设置为空字符串
            for (int i = 0; i < 6; i++)
            {
                faceTextures[i] = "";
            }
            Debug.LogWarning($"[VoxelSystemManager] VoxelDefinition has invalid faceTextures array (null or length < 6)");
        }
        
        Debug.Log($"[VoxelSystemManager] Extracted face textures: [{string.Join(", ", faceTextures)}]");
        return faceTextures;
    }
} 