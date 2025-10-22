using UnityEngine;
using Voxels;
using UnityEngine.EventSystems;

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
            voxelEditingUI.OnGlobalConfirmRequested -= OnGlobalConfirmRequested;
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
        
        _isInitialized = true;
        Debug.Log("[RuntimeVoxelCreator] Initialized successfully");
    }
    
    /// <summary>
    /// 订阅VoxelEditingUI的事件
    /// </summary>
    private void SubscribeToEvents()
    {
        if (voxelEditingUI != null)
        {
            // 先取消订阅避免重复
            voxelEditingUI.OnGlobalConfirmRequested -= OnGlobalConfirmRequested;
            voxelEditingUI.OnDeleteRequested -= OnDeleteRequested;
            
            // 重新订阅
            voxelEditingUI.OnGlobalConfirmRequested += OnGlobalConfirmRequested;
            voxelEditingUI.OnDeleteRequested += OnDeleteRequested;
            
            Debug.Log("[RuntimeVoxelCreator] Subscribed to VoxelEditingUI events");
        }
    }

    
    
    /// <summary>
    /// 处理UI发出的Add按钮点击事件
    /// </summary>
    private void OnAddButtonClicked()
    {
        Debug.Log("[RuntimeVoxelCreator] Add button clicked - entering creation mode");
        // 可以在这里添加创建模式的额外逻辑
    }
    
    /// <summary>
    /// 处理UI发出的Edit按钮点击事件
    /// </summary>
    private void OnEditButtonClicked()
    {
        Debug.Log("[RuntimeVoxelCreator] Edit button clicked - entering edit mode");
        // 可以在这里添加编辑模式的额外逻辑
    }
    

    // Mapping no longer needed: UI order matches VoxelDefinition order
    
    /// <summary>
    /// VoxelEditingUI全局确认事件处理
    /// </summary>
    private void OnGlobalConfirmRequested(string name, string description, Texture2D[] faceTextures, ushort voxelId)
    {
        if (string.IsNullOrEmpty(name) || faceTextures == null || faceTextures.Length != 6)
        {
            Debug.LogError("[RuntimeVoxelCreator] Invalid parameters for voxel creation/modification");
            return;
        }
        
        Debug.Log($"[RuntimeVoxelCreator] Received voxel params: name='{name}', description='{description}', faceTextures count={faceTextures.Length}, voxelId={voxelId}");
        
        if (voxelId == 0)
        {
            // voxelId为0表示创建新体素
            ProcessVoxelCreation(name, description, faceTextures);
        }
        else
        {
            // voxelId不为0表示修改现有体素
            ProcessVoxelModification(voxelId, name, description, faceTextures);
        }
    }
    
    /// <summary>
    /// 处理体素创建
    /// </summary>
    private void ProcessVoxelCreation(string name, string description, Texture2D[] faceTextures)
    {
        try
        {
            voxelSystem.CreateVoxelTypeWithFaces(name, description, faceTextures);
            Debug.Log($"[RuntimeVoxelCreator] ✅ Successfully created voxel '{name}'");
            OnVoxelCreated(name);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[RuntimeVoxelCreator] Failed to create voxel '{name}': {ex.Message}");
        }
    }
    
    
    /// <summary>
    /// 处理体素修改
    /// </summary>
    private void ProcessVoxelModification(ushort voxelId, string name, string description, Texture2D[] faceTextures)
    {
        try
        {
            voxelSystem.ModifyVoxelTypeWithFaces(voxelId, name, description, faceTextures);
            Debug.Log($"[RuntimeVoxelCreator] ✅ Successfully modified voxel '{name}' (ID: {voxelId})");
            OnVoxelModified(name);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[RuntimeVoxelCreator] Failed to modify voxel '{name}': {ex.Message}");
        }
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
        
        Debug.Log($"[RuntimeVoxelCreator] Delete requested for voxel ID: {voxelId}");
        
        try
        {
            voxelSystem.DeleteVoxelType(voxelId);
            Debug.Log($"[RuntimeVoxelCreator] ✅ Successfully deleted voxel ID {voxelId}");
            OnVoxelDeleted(voxelId);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[RuntimeVoxelCreator] Failed to delete voxel ID {voxelId}: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 体素删除成功后的回调
    /// </summary>
    private void OnVoxelDeleted(ushort voxelId)
    {
        // 可以在这里添加删除成功后的处理逻辑
        // 比如刷新UI、显示通知等
        Debug.Log($"[RuntimeVoxelCreator] Voxel ID {voxelId} deleted successfully!");
    }
    
    
    #region Test Methods
    
    /// <summary>
    /// 简单的测试方法 - 创建测试体素
    /// 在Inspector中可以通过按钮调用
    /// </summary>
    
    #endregion
}
