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
    private VoxelInventoryUI voxelInventoryUI;
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
        // 注意：不再直接管理VoxelInventoryUI，由Tool层管理
    }
    
    private void OnDisable()
    {
        // 注意：不再直接管理VoxelInventoryUI，由Tool层管理
    }
    
    private void InitializeComponents()
    {
        // 检查VoxelInventoryUI组件并订阅事件
        if (voxelInventoryUI == null)
        {
            voxelInventoryUI = FindAnyObjectByType<VoxelInventoryUI>();
        }

        if (voxelInventoryUI == null)
        {
            Debug.LogError("[RuntimeVoxelCreator] VoxelInventoryUI not found!");
            return;
        }
        
        // 监听确认按钮（comfirmBtn确认创建voxel type）的点击事件
        SetupUICallbacks();
        
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
    /// 设置UI回调，监听确认按钮点击
    /// </summary>
    private void SetupUICallbacks()
    {
        if (voxelInventoryUI.voxelEditingUI == null) return;
        
        // 直接访问public的comfirmBtn
        if (voxelInventoryUI.voxelEditingUI.comfirmBtn != null)
        {
            voxelInventoryUI.voxelEditingUI.comfirmBtn.onClick.AddListener(OnConfirmButtonClicked);
        }
        else
        {
            Debug.LogWarning("[RuntimeVoxelCreator] comfirmBtn is null in VoxelEditingUI");
        }
    }
    
    /// <summary>
    /// 确认按钮点击回调
    /// </summary>
    private void OnConfirmButtonClicked()
    {
        CreateVoxelFromUI();
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
    
    /// <summary>
    /// 处理体素创建/修改（从VoxelEditingUI获取数据）
    /// 自动判断是创建新体素还是修改现有体素
    /// </summary>
    public void CreateVoxelFromUI()
    {
        if (!_isInitialized)
        {
            Debug.LogError("[RuntimeVoxelCreator] Not initialized!");
            return;
        }
        
        if (voxelInventoryUI.voxelEditingUI == null)
        {
            Debug.LogError("[RuntimeVoxelCreator] VoxelEditingUI reference is null!");
            return;
        }
        
        // 从UI获取用户输入的数据
        string name = voxelInventoryUI.voxelEditingUI.GetVoxelName();
        string description = voxelInventoryUI.voxelEditingUI.GetVoxelDescription();
        Color32[] faceColors = voxelInventoryUI.voxelEditingUI.GetCurrentFaceColors();
        bool isEditingMode = voxelInventoryUI.voxelEditingUI.IsEditingMode();
        ushort editingVoxelId = voxelInventoryUI.voxelEditingUI.GetEditingVoxelId();
        
        // 验证数据
        if (string.IsNullOrEmpty(name))
        {
            Debug.LogWarning("[RuntimeVoxelCreator] Voxel name cannot be empty!");
            return;
        }
        
        if (faceColors == null || faceColors.Length != 6)
        {
            Debug.LogError("[RuntimeVoxelCreator] Invalid face colors array!");
            return;
        }
        
        try
        {
            // 生成纹理
            Texture2D[] faceTextures = GenerateFaceTextures(faceColors);
            
            if (isEditingMode)
            {
                // 修改现有的体素
                ProcessVoxelModification(editingVoxelId, name, description, faceTextures);
            }
            else
            {
                // 创建新的体素
                ProcessVoxelCreation(name, description, faceTextures);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[RuntimeVoxelCreator] Error processing voxel: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 处理体素创建
    /// </summary>
    private void ProcessVoxelCreation(string name, string description, Texture2D[] faceTextures)
    {
        voxelSystem.CreateVoxelTypeWithFaces(name, description, faceTextures);
        Debug.Log($"[RuntimeVoxelCreator] ✅ Successfully created voxel '{name}'");
        
        // 创建成功后可以添加额外的处理逻辑
        OnVoxelCreated(name);
    }
    
    /// <summary>
    /// 处理体素修改
    /// </summary>
    private void ProcessVoxelModification(ushort voxelId, string name, string description, Texture2D[] faceTextures)
    {
        voxelSystem.ModifyVoxelTypeWithFaces(voxelId, name, description, faceTextures);
        Debug.Log($"[RuntimeVoxelCreator] ✅ Successfully modified voxel ID {voxelId} -> '{name}'");
        
        // 修改成功后可以添加额外的处理逻辑
        OnVoxelModified(voxelId, name);
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
    /// 体素修改成功后的回调
    /// </summary>
    private void OnVoxelModified(ushort voxelId, string voxelName)
    {
        // 可以在这里添加修改成功后的处理逻辑
        // 比如刷新UI、显示通知等
        Debug.Log($"[RuntimeVoxelCreator] Voxel ID {voxelId} modified to '{voxelName}' successfully!");
    }
    
    /// <summary>
    /// 生成6个面的纹理（使用ColorTextureGenerator）
    /// </summary>
    private Texture2D[] GenerateFaceTextures(Color32[] faceColors)
    {
        // 检测所有面的颜色是否相同
        bool allColorsSame = AreAllColorsSame(faceColors);
        
        if (allColorsSame)
        {
            // 所有颜色相同，使用统一颜色模式
            Debug.Log($"[RuntimeVoxelCreator] Using unified color mode for color {faceColors[0]}");
            return Voxels.ColorTextureGenerator.GenerateVoxelTextures(null, faceColors[0]);
        }
        else
        {
            // 颜色不同，使用6面不同颜色模式
            Debug.Log($"[RuntimeVoxelCreator] Using 6-face different color mode");
            return Voxels.ColorTextureGenerator.GenerateVoxelTextures(faceColors, Color.white);
        }
    }
    
    /// <summary>
    /// 检测所有面的颜色是否相同
    /// </summary>
    private bool AreAllColorsSame(Color32[] faceColors)
    {
        if (faceColors == null || faceColors.Length < 6)
            return true;
            
        Color32 firstColor = faceColors[0];
        for (int i = 1; i < 6; i++)
        {
            if (!ColorsEqual(firstColor, faceColors[i]))
            {
                return false;
            }
        }
        return true;
    }
    
    /// <summary>
    /// 比较两个颜色是否相等（忽略alpha）
    /// </summary>
    private bool ColorsEqual(Color32 color1, Color32 color2)
    {
        return color1.r == color2.r && color1.g == color2.g && color1.b == color2.b;
    }
    
    #region Test Methods
    
    /// <summary>
    /// 简单的测试方法 - 创建测试体素
    /// 在Inspector中可以通过按钮调用
    /// </summary>
    [ContextMenu("Test Create Simple Voxel")]
    public void TestCreateSimpleVoxel()
    {
        if (!_isInitialized)
        {
            Debug.LogError("[RuntimeVoxelCreator] Not initialized! Please wait for Start() to complete.");
            return;
        }
        
        if (voxelSystem == null)
        {
            Debug.LogError("[RuntimeVoxelCreator] VoxelSystemManager not found!");
            return;
        }
        
        try
        {
            // 创建测试数据
            string testName = "TestVoxel_" + System.DateTime.Now.ToString("HHmmss");
            string testDescription = "这是一个测试体素，通过RuntimeVoxelCreator创建";
            
            // 创建6种不同的测试颜色
            // 体素面顺序: [+X, -X, +Y, -Y, +Z, -Z] = [右面, 左面, 上面, 下面, 前面, 后面]
            Color32[] testColors = new Color32[]
            {
                new Color32(255, 100, 0, 255),    // 红色 - +X (右面)
                new Color32(0, 255, 100, 255),    // 绿色 - -X (左面)
                new Color32(0, 100, 255, 255),    // 蓝色 - +Y (上面)
                new Color32(255, 255, 100, 255),  // 黄色 - -Y (下面)
                new Color32(255, 100, 255, 255),  // 紫色 - +Z (前面)
                new Color32(100, 255, 255, 255)   // 青色 - -Z (后面)
            };
            
            // 生成纹理
            Texture2D[] faceTextures = GenerateFaceTextures(testColors);
            
            // 创建体素
            voxelSystem.CreateVoxelTypeWithFaces(testName, testDescription, faceTextures);
            
            Debug.Log($"[RuntimeVoxelCreator] ✅ 测试成功！创建了测试体素 '{testName}'");
            Debug.Log($"[RuntimeVoxelCreator] 体素描述: {testDescription}");
            Debug.Log($"[RuntimeVoxelCreator] 使用了6种不同颜色的面");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[RuntimeVoxelCreator] 测试失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 测试创建统一颜色的体素
    /// </summary>
    [ContextMenu("Test Create Unified Color Voxel")]
    public void TestCreateUnifiedColorVoxel()
    {
        if (!_isInitialized)
        {
            Debug.LogError("[RuntimeVoxelCreator] Not initialized! Please wait for Start() to complete.");
            return;
        }
        
        if (voxelSystem == null)
        {
            Debug.LogError("[RuntimeVoxelCreator] VoxelSystemManager not found!");
            return;
        }
        
        try
        {
            // 创建测试数据
            string testName = "UnifiedVoxel_" + System.DateTime.Now.ToString("HHmmss");
            string testDescription = "这是一个统一颜色的测试体素";
            
            // 创建6个相同的颜色（统一颜色模式）
            Color32 unifiedColor = new Color32(128, 200, 255, 255); // 浅蓝色
            Color32[] testColors = new Color32[]
            {
                unifiedColor, unifiedColor, unifiedColor,
                unifiedColor, unifiedColor, unifiedColor
            };
            
            // 生成纹理
            Texture2D[] faceTextures = GenerateFaceTextures(testColors);
            
            // 创建体素
            voxelSystem.CreateVoxelTypeWithFaces(testName, testDescription, faceTextures);
            
            Debug.Log($"[RuntimeVoxelCreator] ✅ 测试成功！创建了统一颜色体素 '{testName}'");
            Debug.Log($"[RuntimeVoxelCreator] 体素描述: {testDescription}");
            Debug.Log($"[RuntimeVoxelCreator] 统一颜色: {unifiedColor}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[RuntimeVoxelCreator] 测试失败: {ex.Message}");
        }
    }
    
    
    /// <summary>
    /// 测试纹理生成功能
    /// </summary>
    [ContextMenu("Test Texture Generation")]
    public void TestTextureGeneration()
    {
        try
        {
            Debug.Log("[RuntimeVoxelCreator] 开始测试纹理生成...");
            
            // 测试不同颜色的面
            Color32[] differentColors = new Color32[]
            {
                Color.red, Color.green, Color.blue, 
                Color.yellow, Color.magenta, Color.cyan
            };
            
            // 测试统一颜色
            Color32[] sameColors = new Color32[]
            {
                Color.white, Color.white, Color.white,
                Color.white, Color.white, Color.white
            };
            
            // 生成不同颜色的纹理
            Texture2D[] textures1 = GenerateFaceTextures(differentColors);
            Debug.Log($"[RuntimeVoxelCreator] ✅ 生成了 {textures1.Length} 个不同颜色的面纹理");
            
            // 生成统一颜色的纹理
            Texture2D[] textures2 = GenerateFaceTextures(sameColors);
            Debug.Log($"[RuntimeVoxelCreator] ✅ 生成了 {textures2.Length} 个统一颜色的面纹理");
            
            // 验证纹理尺寸
            if (textures1.Length > 0)
            {
                Debug.Log($"[RuntimeVoxelCreator] 纹理尺寸: {textures1[0].width}x{textures1[0].height}");
            }
            
            Debug.Log("[RuntimeVoxelCreator] ✅ 纹理生成测试完成！");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[RuntimeVoxelCreator] 纹理生成测试失败: {ex.Message}");
        }
    }
    
    #endregion
}
