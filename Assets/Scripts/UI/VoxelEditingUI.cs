using UnityEngine;
using UnityEngine.UI;
using Voxels;
using TMPro;

namespace Voxels
{
    /// <summary>
    /// 体素编辑UI管理器，负责协调各个编辑功能
    /// 使用统一的SurfaceSpec系统管理表面状态
    /// </summary>
    public class VoxelEditingUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject voxelEditingPanel;
    [SerializeField] public Button comfirmBtn; //comfirm and create voxel type
    [SerializeField] public Button resetBtn; //cancel current operation and reset to default
    [SerializeField] public Button addButton; //add new voxel button
    [SerializeField] public Button deleteButton; //delete voxel button
    [SerializeField] public TMP_InputField nameInput;
    [SerializeField] public TMP_InputField descriptionInput;

    [Header("References")]
    [SerializeField] private VoxelInventoryUI voxelInventoryUI;
    [SerializeField] private Cube3DUI cube3DUI; // 3D预览组件

    [Header("Face Color Settings")]
    [Tooltip("顺序: Right(+X), Left(-X), Up(+Y), Down(-Y), Front(+Z), Back(-Z)")]
    [SerializeField] private ColorPickerUI[] faceColorPickers = new ColorPickerUI[6]; // 6个面的颜色选择器（与VoxelDefinition一致）
    
    [Header("Surface Management")]
    [SerializeField] private IColorTextureCache colorTextureCache; // 颜色纹理缓存
    
    private bool _isInitialized = false;
    private int _currentSelectedFace = 4; // 当前选中的面索引 (0-5)，默认Front面
    public bool _isEditingMode = false;
    public ushort _editingVoxelId;
    private bool _isAddButtonSelected = false;
    
    // SurfaceSpec状态管理
    private SurfaceSpec[] _stableSpecs = new SurfaceSpec[6]; // 已确认/来自def的"稳定态"
    private SurfaceSpec[] _previewSpecs = new SurfaceSpec[6]; // 正在编辑的"暂态"
    
    // 按钮颜色
    [SerializeField] private Color selectedColor = new Color(0.055f, 0.9f, 0.21f, 1f);
    [SerializeField] private Color normalColor = new Color(0.23f, 0.23f, 0.23f, 1f);
    
    public event System.Action<string, string, Texture2D[], ushort> OnGlobalConfirmRequested; // 全局确认事件，传递(name, description, faceTextures, voxelId)
    public event System.Action<ushort> OnDeleteRequested; // 删除事件，传递voxelId

    private void Start()
    {
        if (_isInitialized) return;
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
            // 重新添加按钮监听器
            if (comfirmBtn != null)
            {
                comfirmBtn.onClick.RemoveAllListeners();
                comfirmBtn.onClick.AddListener(Confirm);
            }
            if (resetBtn != null)
            {
                resetBtn.onClick.RemoveAllListeners();
                resetBtn.onClick.AddListener(Reset);
            }
        }
        
        voxelEditingPanel.SetActive(true);
    }

    private void InitializeComponents()
    {
        // 初始化UI组件
        InitializeUIComponents();
        
        // 查找引用组件
        FindReferenceComponents();
        
        // 初始化SurfaceSpec系统
        InitializeSurfaceSpecs();

        // 设置按钮事件监听器
        SetupButtonListeners();

        // 初始化Cube3DUI和ColorPickerUI
        InitializeCube3DUIAndColorPickers();

        _isInitialized = true;
    }
    
    private void InitializeUIComponents()
    {
        if (comfirmBtn == null)
            comfirmBtn = voxelEditingPanel.transform.Find("ComfirmBtn").GetComponent<Button>();
        if (resetBtn == null)
            resetBtn = voxelEditingPanel.transform.Find("ResetBtn").GetComponent<Button>();
        if (addButton == null)
            addButton = voxelEditingPanel.transform.Find("AddButton").GetComponent<Button>();
        if (nameInput == null)
            nameInput = voxelEditingPanel.transform.Find("NameInput").GetComponent<TMP_InputField>();
        if (descriptionInput == null)
            descriptionInput = voxelEditingPanel.transform.Find("DescriptionInput").GetComponent<TMP_InputField>();
    }

    private void FindReferenceComponents()
    {
        // 查找VoxelInventoryUI引用
        if (voxelInventoryUI == null)
        {
            voxelInventoryUI = FindAnyObjectByType<VoxelInventoryUI>();
            if (voxelInventoryUI == null)
            {
                Debug.LogWarning("[VoxelEditingUI] VoxelInventoryUI not found!");
            }
        }
        
        // 查找Cube3DUI引用
        if (cube3DUI == null)
        {
            cube3DUI = FindAnyObjectByType<Cube3DUI>();
            if (cube3DUI == null)
            {
                Debug.LogWarning("[VoxelEditingUI] Cube3DUI not found!");
            }
        }
        
        // 无论cube3DUI是预先赋值还是动态找到的，都要订阅事件
        if (cube3DUI != null)
        {
            SubscribeToCube3DEvents();
        }
        
        // 订阅VoxelInventoryUI的事件
        if (voxelInventoryUI != null)
        {
            voxelInventoryUI.OnVoxelTypeSelected += OnVoxelTypeSelected;
        }
    }
    
    private void InitializeSurfaceSpecs()
    {
        // 初始化SurfaceSpec数组
        for (int i = 0; i < 6; i++)
        {
            _stableSpecs[i] = new SurfaceSpec();
            _previewSpecs[i] = new SurfaceSpec();
        }
        
        // 初始化颜色纹理缓存（如果未设置）
        if (colorTextureCache == null)
        {
            colorTextureCache = new SimpleColorTextureCache();
            Debug.Log("[VoxelEditingUI] Created default SimpleColorTextureCache");
        }
    }
    
    private void SetupButtonListeners()
    {
        // 先移除所有已有的监听器，防止重复添加
        if (comfirmBtn != null)
        {
            comfirmBtn.onClick.RemoveAllListeners();
            comfirmBtn.onClick.AddListener(Confirm);
        }
        if (resetBtn != null)
        {
            resetBtn.onClick.RemoveAllListeners();
            resetBtn.onClick.AddListener(Reset);
        }
        if (addButton != null)
        {
            addButton.onClick.RemoveAllListeners();
            addButton.onClick.AddListener(ToggleAddButton);
        }
        if (deleteButton != null)
        {
            deleteButton.onClick.RemoveAllListeners();
            deleteButton.onClick.AddListener(OnDeleteButtonClicked);
        }
    }
    
    private void InitializeCube3DUIAndColorPickers()
    {
        // 初始化面颜色选择器
        Debug.Log($"[VoxelEditingUI] Initializing {faceColorPickers.Length} ColorPickers");
        
        // 验证所有ColorPickerUI组件
        for (int i = 0; i < 6; i++)
        {
            if (faceColorPickers[i] == null)
            {
                Debug.LogError($"[VoxelEditingUI] ColorPickerUI at index {i} is null!");
                return;
            }
        }
            
        // 设置事件监听器
        SetupColorPickerEvents();
        
        // 初始化默认颜色
        InitializeDefaultColors();
        
        // 确保Cube3DUI连接
        EnsureCube3DUIConnection();
        
        // 应用初始状态到Cube3DUI
        ApplyPreviewToCube3D();
        
        // 同步ColorPickerUI到当前状态
        SyncColorPickersFromPreview();

        HideAllColorPickers();
        
        // 显式选择Front面（索引4）作为默认选中面
        SelectFace(4); // Front = 4
        
        Debug.Log("[VoxelEditingUI] ColorPickers and Cube3DUI initialized and synchronized");
    }

    
    private void SetupColorPickerEvents()
    {
        // 面颜色选择器事件
        for (int i = 0; i < 6; i++)
        {
            if (faceColorPickers[i] != null)
            {
                int faceIndex = i; // 捕获循环变量
                faceColorPickers[i].onColorChanged.AddListener((color) => {
                    OnColorChanged(faceIndex, color);
                });
                
                // 添加确认和重置按钮事件
                faceColorPickers[i].onColorConfirmed.AddListener(() => {
                    ConfirmCurrentFaceModification();
                });
                
                faceColorPickers[i].onColorReset.AddListener((_) => {
                    CancelCurrentFaceModification();
                });
            }
        }
    }
    
    private void InitializeDefaultColors()
    {
        // 设置默认颜色为灰色（避免纯白色导致HSV饱和度为0）
        Color32 defaultColor = new Color32(200, 200, 200, 255);
        
        for (int i = 0; i < 6; i++)
        {
            _stableSpecs[i].SetColorMode(defaultColor, SurfaceSpec.Origin.FromDef);
            _previewSpecs[i].SetColorMode(defaultColor, SurfaceSpec.Origin.FromDef);
        }
        
        // 更新UI（静默，不触发onColorChanged，避免预览误变）
        for (int i = 0; i < 6; i++)
        {
            if (faceColorPickers[i] != null)
                faceColorPickers[i].SetColorSilently(_previewSpecs[i].baseColor);
        }
    }
    
    // ========== 核心SurfaceSpec管理方法 ==========
    
    /// <summary>
    /// 载入/切换voxel，使用SurfaceSpec系统
    /// </summary>
    public void LoadVoxel(VoxelDefinition def)
    {
        if (def == null)
        {
            Debug.LogWarning("[VoxelEditingUI] VoxelDefinition is null, using default colors");
            InitializeDefaultColors();
            return;
        }
        
        // 使用转换器从VoxelDefinition创建稳定态SurfaceSpec
        _stableSpecs = VoxelSurfaceTranslator.FromDefinition(def);
        
        // 深拷贝稳定态到预览态
        _previewSpecs = VoxelSurfaceTranslator.DeepCopy(_stableSpecs);
        
        // 统一同步所有UI组件
        SyncAllUIComponents();
        
        Debug.Log($"[VoxelEditingUI] Loaded voxel: {def.displayName}");
    }
    
    /// <summary>
    /// 选择面并显示对应的颜色面板
    /// </summary>
    public void SelectFace(int faceIndex)
    {
        if (faceIndex < 0 || faceIndex >= 6)
        {
            Debug.LogWarning($"[VoxelEditingUI] Invalid face index: {faceIndex}");
            return;
        }
        
        // 如果之前有选中的面且该面有未确认的修改，回滚到稳定态
        if (_previewSpecs[_currentSelectedFace].isTemporary)
        {
            _previewSpecs[_currentSelectedFace] = _stableSpecs[_currentSelectedFace].DeepCopy();
            // 统一同步所有UI组件
            SyncAllUIComponents();
            Debug.Log($"[VoxelEditingUI] Rolled back unconfirmed changes for face {_currentSelectedFace}");
        }
        
        // 隐藏所有面板
        HideAllColorPickers();
        
        // 显示选中的面板
        if (faceColorPickers[faceIndex] != null)
        {
            faceColorPickers[faceIndex].SetPanelActive(true);
            _currentSelectedFace = faceIndex;
            
            // 同步颜色选择器到当前预览状态
            faceColorPickers[faceIndex].SetColorSilently(_previewSpecs[faceIndex].baseColor);
            
            Debug.Log($"[VoxelEditingUI] Selected face {faceIndex}, color set to {_previewSpecs[faceIndex].baseColor}");
        }
        else
        {
            Debug.LogError($"[VoxelEditingUI] ColorPicker for face {faceIndex} is null!");
        }
    }
    
    /// <summary>
    /// 颜色改变回调（临时状态）
    /// </summary>
    public void OnColorChanged(int faceIndex, Color32 color)
    {
        var spec = _previewSpecs[faceIndex];
        
        // 设置为纯色模式
        spec.SetColorMode(color, SurfaceSpec.Origin.FromTempColor);
        
        // 只更新Cube3DUI预览（ColorPickerUI不需要更新，因为它是事件源）
        ApplyPreviewToCube3D();
        
        Debug.Log($"[VoxelEditingUI] Face {faceIndex} color changed to {color} (temporary)");
    }
    
    /// <summary>
    /// 确认当前面的修改
    /// </summary>
    public void ConfirmCurrentFaceModification()
    {
        _stableSpecs[_currentSelectedFace] = _previewSpecs[_currentSelectedFace].DeepCopy();
        _stableSpecs[_currentSelectedFace].ResetToStable(); // 标记为稳定状态
        
        Debug.Log($"[VoxelEditingUI] Face {_currentSelectedFace} modification confirmed");
    }
    
    /// <summary>
    /// 取消当前面的修改
    /// </summary>
    public void CancelCurrentFaceModification()
    {
        _previewSpecs[_currentSelectedFace] = _stableSpecs[_currentSelectedFace].DeepCopy();
        
        // 统一同步所有UI组件
        SyncAllUIComponents();
        
        Debug.Log($"[VoxelEditingUI] Face {_currentSelectedFace} modification cancelled");
    }
    
    /// <summary>
    /// 将当前稳定态SurfaceSpec转换为VoxelSystemManager需要的参数
    /// </summary>
    private void PrepareVoxelSystemManagerParams(out string name, out string description, out Texture2D[] faceTextures)
    {
        name = nameInput != null ? nameInput.text : "NewVoxel";
        description = descriptionInput != null ? descriptionInput.text : "";
        
        // 创建6个面的纹理数组
        faceTextures = new Texture2D[6];
        
        for (int i = 0; i < 6; i++)
        {
            var spec = _stableSpecs[i];
            
            if (spec.mode == SurfaceMode.Color)
            {
                // 纯色模式：生成颜色纹理
                string colorName = GenerateColorName(spec.baseColor);
                faceTextures[i] = colorTextureCache.GetOrCreateColorTexture(colorName);
            }
            else if (spec.mode == SurfaceMode.Texture && spec.albedo != null)
            {
                // 贴图模式：直接使用贴图
                faceTextures[i] = spec.albedo;
            }
            else
            {
                // 无效状态：使用默认白色纹理
                faceTextures[i] = colorTextureCache.GetOrCreateColorTexture("255+255+255");
            }
        }
        
        Debug.Log($"[VoxelEditingUI] Prepared params for VoxelSystemManager: name='{name}', description='{description}', faceTextures count={faceTextures.Length}");
    }
    
    /// <summary>
    /// 生成颜色名称（RGB格式）
    /// </summary>
    private string GenerateColorName(Color32 color)
    {
        return $"{color.r}+{color.g}+{color.b}";
    }
    
    /// <summary>
    /// 同步ColorPickerUI到预览状态
    /// </summary>
    private void SyncColorPickersFromPreview()
    {
        for (int i = 0; i < 6; i++)
        {
            if (faceColorPickers[i] != null)
                faceColorPickers[i].SetColorSilently(_previewSpecs[i].baseColor);
        }
    }
    
    /// <summary>
    /// 统一同步方法：确保所有UI组件状态一致
    /// </summary>
    private void SyncAllUIComponents()
    {
        // 同步Cube3DUI
        ApplyPreviewToCube3D();
        
        // 同步ColorPickerUI
        SyncColorPickersFromPreview();
        
    }
    
    /// <summary>
    /// 隐藏所有颜色选择器面板
    /// </summary>
    public void HideAllColorPickers()
    {
        for (int i = 0; i < 6; i++)
        {
            if (faceColorPickers[i] != null)
            {
                faceColorPickers[i].SetPanelActive(false);
            }
        }
        _currentSelectedFace = 4; // 重置为默认面（Front）
    }

    private void OnDisable()
    {
        if (voxelEditingPanel != null)
        {
            voxelEditingPanel.SetActive(false);
        }
        
        // 在禁用时移除所有监听器
        if (comfirmBtn != null)
            comfirmBtn.onClick.RemoveAllListeners();
        if (resetBtn != null)
            resetBtn.onClick.RemoveAllListeners();
        if (addButton != null)
            addButton.onClick.RemoveAllListeners();
        if (deleteButton != null)
            deleteButton.onClick.RemoveAllListeners();
    }

    private void OnDestroy()
    {
        if (comfirmBtn != null)
            comfirmBtn.onClick.RemoveAllListeners();
        if (resetBtn != null)
            resetBtn.onClick.RemoveAllListeners();
        if (addButton != null)
            addButton.onClick.RemoveAllListeners();
        if (deleteButton != null)
            deleteButton.onClick.RemoveAllListeners();
            
        // 清理VoxelInventoryUI事件订阅
        if (voxelInventoryUI != null)
        {
            voxelInventoryUI.OnVoxelTypeSelected -= OnVoxelTypeSelected;
        }
        
        // 清理Cube3DUI事件订阅
        UnsubscribeFromCube3DEvents();
    }

    /// <summary>
    /// 设置为编辑模式，用于修改现有的voxel
    /// </summary>
    public void SetEditMode(ushort voxelId)
    {
        _isEditingMode = true;
        _editingVoxelId = voxelId;
        _isAddButtonSelected = false;

        // 更新Add按钮状态
        if (addButton != null)
        {
            addButton.image.color = normalColor;
        }

        //显示删除按钮
        if (deleteButton != null)
        {
            deleteButton.gameObject.SetActive(true);
        }

        // 获取现有voxel的信息
        var def = VoxelRegistry.GetDefinition(voxelId);
        if (def != null)
        {
            nameInput.text = def.name;
            descriptionInput.text = def.description;
            
            // 使用新的SurfaceSpec系统加载voxel
            LoadVoxel(def);
        }
        
        // 发出状态变化事件 不需要了
    }

    /// <summary>
    /// 设置为创建模式，用于创建新的voxel
    /// </summary>
    public void SetCreateMode()
    {
        _isEditingMode = false;
        _editingVoxelId = 0;
        _isAddButtonSelected = true;
        
        // 更新Add按钮状态
        if (addButton != null)
        {
            addButton.image.color = selectedColor;
        }
        
        nameInput.text = "";
        descriptionInput.text = "";

        //隐藏删除按钮
        if (deleteButton != null)
        {
            deleteButton.gameObject.SetActive(false);
        }
        
        // 重置为默认颜色
        InitializeDefaultColors();
        
        // 统一同步所有UI组件
        SyncAllUIComponents();
    }

    private void Confirm()
    {
        if (string.IsNullOrEmpty(nameInput.text))
        {
            Debug.LogWarning("[VoxelEditingUI] Please enter a name for the voxel!");
            return;
        }

        // 检查是否有任何面被修改且确认
        bool hasConfirmedChanges = false;
        for (int i = 0; i < 6; i++)
        {
            if (_previewSpecs[i].isTemporary)
            {
                hasConfirmedChanges = true;
                break;
            }
        }

        if (_isEditingMode && !hasConfirmedChanges)
        {
            Debug.LogWarning("[VoxelEditingUI] No face modifications confirmed! Please confirm at least one face change.");
            return;
        }

        // 根据模式进行后续处理
        if (_isEditingMode)
        {
            // 编辑模式：准备参数并发送事件，传递当前编辑的voxel ID
            PrepareVoxelSystemManagerParams(out string name, out string description, out Texture2D[] faceTextures);
            OnGlobalConfirmRequested?.Invoke(name, description, faceTextures, _editingVoxelId);
            SetCreateMode(); // 退出编辑模式
        }
        else
        {
            // 创建模式：准备参数并发送事件，传递0表示新创建
            PrepareVoxelSystemManagerParams(out string name, out string description, out Texture2D[] faceTextures);
            OnGlobalConfirmRequested?.Invoke(name, description, faceTextures, 0);
            Reset(); // 重置表单
        }
    }

    private void Reset()
    {
        if (_isEditingMode)
        {
            // 重置为原始值
            var def = VoxelRegistry.GetDefinition(_editingVoxelId);
            if (def != null)
            {
                nameInput.text = def.name;
                descriptionInput.text = def.description;
                LoadVoxel(def); // 重新加载原始状态
            }
        }
        else
        {
            // 清空所有字段
            nameInput.text = "";
            descriptionInput.text = "";
            InitializeDefaultColors(); // 重置为默认颜色
            SyncAllUIComponents(); // 统一同步所有UI组件
        }
    }
    
    /// <summary>
    /// 切换Add按钮状态
    /// </summary>
    private void ToggleAddButton()
    {
        _isAddButtonSelected = !_isAddButtonSelected;
        
        if (_isAddButtonSelected)
        {
            // 切换到创建模式
            SetCreateMode();
        }
        else
        {
            // 切换到编辑模式（如果有选中的voxel）
            UpdateEditingStateFromInventory();
        }
    }
    
    /// <summary>
    /// 删除按钮点击处理
    /// </summary>
    private void OnDeleteButtonClicked()
    {
        if (_isEditingMode && _editingVoxelId != 0)
        {
            // 发送删除事件
            OnDeleteRequested?.Invoke(_editingVoxelId);
            Debug.Log($"[VoxelEditingUI] Delete button clicked for voxel ID: {_editingVoxelId}");
        }
        else
        {
            Debug.LogWarning("[VoxelEditingUI] Cannot delete: not in editing mode or no voxel selected");
        }
    }
    
    /// <summary>
    /// Voxel类型选中回调
    /// </summary>
    private void OnVoxelTypeSelected(VoxelDefinition voxelDef)
    {
        Debug.Log($"[VoxelEditingUI] Voxel selected: {voxelDef.displayName}");
        UpdateEditingStateFromInventory();
    }
    
    /// <summary>
    /// 从VoxelInventoryUI获取当前选中状态并更新编辑模式
    /// </summary>
    public void UpdateEditingStateFromInventory()
    {
        if (voxelInventoryUI == null) return;
        
        // 检查是否有选中的slot
        var selectedSlot = voxelInventoryUI._selectedSlot;
        if (selectedSlot != null)
        {
            // 有选中项，设置为编辑模式
            SetEditMode(selectedSlot.slotId);
        }
        else
        {
            // 没有选中项，设置为创建模式
            SetCreateMode();
        }
    }

    // ========== 公共方法：供外部调用 ==========
    
    /// <summary>
    /// 获取体素名称（供RuntimeVoxelCreator调用）
    /// </summary>
    public string GetVoxelName()
    {
        return nameInput != null ? nameInput.text : "";
    }
    
    /// <summary>
    /// 获取体素描述（供RuntimeVoxelCreator调用）
    /// </summary>
    public string GetVoxelDescription()
    {
        return descriptionInput != null ? descriptionInput.text : "";
    }
    
    /// <summary>
    /// 获取是否处于编辑模式（供RuntimeVoxelCreator调用）
    /// </summary>
    public bool IsEditingMode()
    {
        return _isEditingMode;
    }
    
    /// <summary>
    /// 获取正在编辑的体素ID（供RuntimeVoxelCreator调用）
    /// </summary>
    public ushort GetEditingVoxelId()
    {
        return _editingVoxelId;
    }
    
    /// <summary>
    /// 获取当前6个面的颜色设置（供RuntimeVoxelCreator调用）
    /// </summary>
    public Color32[] GetCurrentFaceColors()
    {
        Color32[] colors = new Color32[6];
        for (int i = 0; i < 6; i++)
        {
            colors[i] = _stableSpecs[i].baseColor;
        }
        return colors;
    }
    
    /// <summary>
    /// 获取当前6个面的纹理设置（供RuntimeVoxelCreator调用）
    /// </summary>
    public Texture2D[] GetCurrentFaceTextures()
    {
        Texture2D[] textures = new Texture2D[6];
        for (int i = 0; i < 6; i++)
        {
            textures[i] = _stableSpecs[i].albedo;
        }
        return textures;
    }
    
    /// <summary>
    /// 获取当前6个面的模式（供RuntimeVoxelCreator调用）
    /// </summary>
    public SurfaceMode[] GetCurrentFaceModes()
    {
        SurfaceMode[] modes = new SurfaceMode[6];
        for (int i = 0; i < 6; i++)
        {
            modes[i] = _stableSpecs[i].mode;
        }
        return modes;
    }
    
    /// <summary>
    /// 应用颜色到Cube3DUI预览（VoxelEditingUI直接管理）
    /// </summary>
    private void ApplyPreviewToCube3D()
    {
        if (cube3DUI == null) return;
        
        for (int i = 0; i < 6; i++)
        {
            Cube3DUI.CubeFace face = (Cube3DUI.CubeFace)i;
            SurfaceSpec spec = _previewSpecs[i];
            
            if (spec.IsColorMode)
            {
                cube3DUI.SetFaceColor(face, spec.baseColor);
            }
            else if (spec.IsTextureMode && spec.albedo != null)
            {
                cube3DUI.SetFaceTexture(face, spec.albedo);
            }
        }
    }
    
    /// <summary>
    /// 确保Cube3DUI连接并订阅事件
    /// </summary>
    private void EnsureCube3DUIConnection()
    {
        if (cube3DUI == null)
        {
            Debug.LogWarning("[VoxelEditingUI] Cube3DUI is null, attempting to find it");
            cube3DUI = FindAnyObjectByType<Cube3DUI>();
        }
        
        if (cube3DUI != null)
        {
            SubscribeToCube3DEvents();
            Debug.Log("[VoxelEditingUI] Cube3DUI connection ensured");
        }
        else
        {
            Debug.LogError("[VoxelEditingUI] Failed to find Cube3DUI component!");
        }
    }
    
    /// <summary>
    /// 订阅Cube3DUI的面选择事件
    /// </summary>
    private void SubscribeToCube3DEvents()
    {
        if (cube3DUI != null)
        {
            // 先取消订阅，避免重复订阅
            cube3DUI.OnFaceSelected -= OnCube3DFaceSelected;
            cube3DUI.OnFaceSelected += OnCube3DFaceSelected;
            Debug.Log("[VoxelEditingUI] Subscribed to Cube3DUI face selection events");
        }
    }
    
    /// <summary>
    /// 取消订阅Cube3DUI的面选择事件
    /// </summary>
    private void UnsubscribeFromCube3DEvents()
    {
        if (cube3DUI != null)
        {
            cube3DUI.OnFaceSelected -= OnCube3DFaceSelected;
            Debug.Log("[VoxelEditingUI] Unsubscribed from Cube3DUI face selection events");
        }
    }
    
    /// <summary>
    /// Cube3DUI面选择事件回调
    /// </summary>
    private void OnCube3DFaceSelected(int faceIndex)
    {
        Debug.Log($"[VoxelEditingUI] Cube3DUI face selected: {faceIndex}");
        SelectFace(faceIndex);
    }

    }
} 