using UnityEngine;
using UnityEngine.UI;
using Voxels;
using TMPro;

/// <summary>
/// 体素编辑UI管理器，负责协调各个编辑功能
/// </summary>
public class VoxelEditingUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject voxelEditingPanel;
    [SerializeField] public Button comfirmBtn; //comfirm and create voxel type
    [SerializeField] public Button resetBtn; //cancel current operation and reset to default
    [SerializeField] public TMP_InputField nameInput;
    [SerializeField] public TMP_InputField descriptionInput;

    [Header("Face Color Settings")]
    [SerializeField] private ColorPickerUI[] faceColorPickers = new ColorPickerUI[6]; // 6个面的颜色选择器
    [SerializeField] private string[] faceNames = { "+X", "-X", "+Y", "-Y", "+Z", "-Z" }; // 面名称
    private bool _isInitialized = false;
    public bool _isEditingMode = false;
    public ushort _editingVoxelId;
    
    // 颜色状态
    private Color32[] _faceColors = new Color32[6];

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
        if (comfirmBtn == null)
            comfirmBtn = voxelEditingPanel.transform.Find("ComfirmBtn").GetComponent<Button>();
        if (resetBtn == null)
            resetBtn = voxelEditingPanel.transform.Find("ResetBtn").GetComponent<Button>();
        if (nameInput == null)
            nameInput = voxelEditingPanel.transform.Find("NameInput").GetComponent<TMP_InputField>();
        if (descriptionInput == null)
            descriptionInput = voxelEditingPanel.transform.Find("DescriptionInput").GetComponent<TMP_InputField>();

        
        // 初始化颜色选择器
        InitializeColorPickers();

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

        _isInitialized = true;
    }
    
    private void InitializeColorPickers()
    {
        // 初始化面颜色选择器
        for (int i = 0; i < 6; i++)
        {
            if (faceColorPickers[i] == null)
            {
                string facePickerPath = $"FaceColorPickers/{faceNames[i]}ColorPicker";
                faceColorPickers[i] = voxelEditingPanel.transform.Find(facePickerPath)?.GetComponent<ColorPickerUI>();
            }
        }
            
        // 设置事件监听器
        SetupColorPickerEvents();
        
        // 初始化默认颜色
        InitializeDefaultColors();
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
                    _faceColors[faceIndex] = color;
                });
            }
        }
    }
    
    private void InitializeDefaultColors()
    {
        // 设置默认颜色为白色
        for (int i = 0; i < 6; i++)
        {
            _faceColors[i] = Color.white;
        }
        
        // 更新UI
        for (int i = 0; i < 6; i++)
        {
            if (faceColorPickers[i] != null)
                faceColorPickers[i].SetColor(_faceColors[i]);
        }
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
    }

    private void OnDestroy()
    {
        if (comfirmBtn != null)
            comfirmBtn.onClick.RemoveAllListeners();
        if (resetBtn != null)
            resetBtn.onClick.RemoveAllListeners();
    }

    /// <summary>
    /// 设置为编辑模式，用于修改现有的voxel
    /// </summary>
    public void SetEditMode(ushort voxelId)
    {
        _isEditingMode = true;
        _editingVoxelId = voxelId;

        // 获取现有voxel的信息
        var def = VoxelRegistry.GetDefinition(voxelId);
        if (def != null)
        {
            nameInput.text = def.name;
            descriptionInput.text = def.description;
        }
    }

    /// <summary>
    /// 设置为创建模式，用于创建新的voxel
    /// </summary>
    public void SetCreateMode()
    {
        _isEditingMode = false;
        _editingVoxelId = 0;
        nameInput.text = "";
        descriptionInput.text = "";
    }

    private void Confirm()
    {
        if (string.IsNullOrEmpty(nameInput.text))
        {
            Debug.LogWarning("[VoxelEditingUI] Please enter a name for the voxel!");
            return;
        }

        // UI只负责提供数据，不直接调用Runtime组件
        // RuntimeVoxelCreator会监听确认事件或主动获取数据
        Debug.Log($"[VoxelEditingUI] Confirm button clicked - data ready for collection");
        
        // 根据模式进行后续处理
        if (_isEditingMode)
        {
            SetCreateMode(); // 退出编辑模式
        }
        else
        {
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
            }
        }
        else
        {
            // 清空所有字段
            nameInput.text = "";
            descriptionInput.text = "";
            ResetColors();
        }
    }
    
    
    
    
    /// <summary>
    /// 重置所有颜色设置
    /// </summary>
    private void ResetColors()
    {
        // 重置所有面颜色为白色
        for (int i = 0; i < 6; i++)
        {
            _faceColors[i] = Color.white;
        }
        
        // 更新UI
        for (int i = 0; i < 6; i++)
        {
            if (faceColorPickers[i] != null)
                faceColorPickers[i].SetColor(_faceColors[i]);
        }
    }
    
    // ========== 公共方法：供RuntimeVoxelCreator调用 ==========
    
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
    /// 获取当前6个面的颜色设置（供RuntimeVoxelCreator调用）
    /// </summary>
    public Color32[] GetCurrentFaceColors()
    {
        Color32[] colors = new Color32[6];
        for (int i = 0; i < 6; i++)
        {
            colors[i] = _faceColors[i];
        }
        return colors;
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
} 