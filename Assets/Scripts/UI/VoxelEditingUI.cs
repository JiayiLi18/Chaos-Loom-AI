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

    [Header("Dependencies")]
    [SerializeField] private VoxelSystemManager voxelSystem;
    [SerializeField] private RenderTexture paintingRenderTexture;
    [SerializeField] private PaintingToolUI paintingToolUI;
    private bool _isInitialized = false;
    private bool _isEditingMode = false;
    private ushort _editingVoxelId;

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
        if (paintingToolUI != null)
            paintingToolUI.enabled = true;
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

        if (voxelSystem == null)
        {
            voxelSystem = FindAnyObjectByType<VoxelSystemManager>();
            if (voxelSystem == null)
            {
                Debug.LogError("[VoxelEditingUI] VoxelSystemManager not found!");
                enabled = false;
                return;
            }
        }
        if (paintingToolUI == null)
        {
            paintingToolUI = FindAnyObjectByType<PaintingToolUI>();
            if (paintingToolUI == null)
                Debug.LogError("[VoxelEditingUI] PaintingToolUI not found!");
        }

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

    private void OnDisable()
    {
        if (voxelEditingPanel != null)
        {
            voxelEditingPanel.SetActive(false);
        }
        if (paintingToolUI != null)
            paintingToolUI.enabled = false;
        
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
        if (voxelSystem == null)
        {
            Debug.LogError("[VoxelEditingUI] VoxelSystemManager not found!");
            return;
        }

        if (string.IsNullOrEmpty(nameInput.text))
        {
            Debug.LogWarning("[VoxelEditingUI] Please enter a name for the voxel!");
            return;
        }

        // 从RenderTexture创建Texture2D
        Texture2D texture = voxelSystem.CreateTextureFromRenderTexture(paintingRenderTexture);
        if (texture == null)
        {
            Debug.LogError("[VoxelEditingUI] Failed to create texture from RenderTexture!");
            return;
        }

        if (_isEditingMode)
        {
            // 修改现有的voxel
            voxelSystem.ModifyVoxelType(_editingVoxelId, nameInput.text, descriptionInput.text, texture);
        }
        else
        {
            // 创建新的voxel
            voxelSystem.CreateVoxelType(nameInput.text, descriptionInput.text, texture);
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
        }
    }
} 