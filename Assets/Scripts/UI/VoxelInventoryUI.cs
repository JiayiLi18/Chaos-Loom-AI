using UnityEngine;
using System.Collections.Generic;
using Voxels;
using UnityEngine.Events;
using UnityEngine.UI;

public class VoxelInventoryUI : MonoBehaviour
{
    [Header("UI Source")]
    [SerializeField] private GameObject voxelInventoryPanel;
    private Transform slotsContainer;
    [SerializeField] private VoxelInventorySlot slotPrefab;
    public VoxelEditingUI voxelEditingUI;

    [SerializeField] private Button addButton;
    [SerializeField] private Button editButton;
    [SerializeField] private Button deleteButton;

    [Header("Dependencies")]
    [SerializeField] private VoxelSystemManager voxelSystem;

    [SerializeField] private Color selectedColor = new Color(0.055f, 0.9f, 0.21f, 1f);
    [SerializeField] private Color normalColor = new Color(0.23f, 0.23f, 0.23f, 1f);

    private List<VoxelInventorySlot> _slots = new List<VoxelInventorySlot>();
    private bool _isAddButtonSelected = false;
    private bool _isEditButtonSelected = false;
    public VoxelInventorySlot _selectedSlot;
    private bool _isInitialized = false;

    // 事件系统
    public event System.Action<VoxelDefinition> OnVoxelTypeSelected;
    public event System.Action<ushort> OnDeleteButtonClicked;

    private void OnEnable()
    {
        if (!_isInitialized)
        {
            InitializeComponents();
        }
        
        // 简单直接地激活UI面板
        if (voxelInventoryPanel != null)
        {
            voxelInventoryPanel.SetActive(true);
        }
        
        // 刷新库存显示
        RefreshInventory();
    }

    private void SetupButtons()
    {
        // Add按钮 - 直接控制VoxelEditingUI的创建模式
        if (addButton != null)
        {
            addButton.onClick.RemoveAllListeners();
            addButton.onClick.AddListener(() => {
                ToggleAddButton();
            });
        }

        // Edit按钮 - 直接控制VoxelEditingUI的编辑模式
        if (editButton != null)
        {
            editButton.onClick.RemoveAllListeners();
            editButton.onClick.AddListener(() => {
                if (_selectedSlot == null) return;
                ToggleEditButton();
            });
        }

        // Delete按钮 - 删除选中的voxel
        if (deleteButton != null)
        {
            deleteButton.onClick.RemoveAllListeners();
            deleteButton.onClick.AddListener(() => {
                if (_selectedSlot == null) return;
                DeleteSelectedVoxel();
            });
        }

        // 初始化按钮颜色
        if (addButton != null) addButton.image.color = normalColor;
        if (editButton != null) editButton.image.color = normalColor;
    }
    
    /// <summary>
    /// 切换Add按钮状态，直接控制VoxelEditingUI
    /// </summary>
    private void ToggleAddButton()
    {
        _isAddButtonSelected = !_isAddButtonSelected;
        addButton.image.color = _isAddButtonSelected ? selectedColor : normalColor;
        
        if (voxelEditingUI != null)
        {
            if (_isAddButtonSelected)
            {
                // 打开编辑UI并设置为创建模式
                voxelEditingUI.gameObject.SetActive(true);
                voxelEditingUI.enabled = true;
                voxelEditingUI.SetCreateMode();
            }
            else
            {
                // 关闭编辑UI
                voxelEditingUI.enabled = false;
            }
        }
        
    }
    
    /// <summary>
    /// 切换Edit按钮状态，直接控制VoxelEditingUI
    /// </summary>
    private void ToggleEditButton()
    {
        _isEditButtonSelected = !_isEditButtonSelected;
        editButton.image.color = _isEditButtonSelected ? selectedColor : normalColor;
        
        if (voxelEditingUI != null)
        {
            if (_isEditButtonSelected)
            {
                // 打开编辑UI并设置为编辑模式
                voxelEditingUI.gameObject.SetActive(true);
                voxelEditingUI.enabled = true;
                voxelEditingUI.SetEditMode(_selectedSlot.slotId);
            }
            else
            {
                // 关闭编辑UI
                voxelEditingUI.enabled = false;
            }
        }
        
    }
    
    /// <summary>
    /// 删除选中的voxel
    /// </summary>
    private void DeleteSelectedVoxel()
    {
        if (_selectedSlot == null || voxelSystem == null) return;
        
        // 发出事件供Runtime组件监听
        OnDeleteButtonClicked?.Invoke(_selectedSlot.slotId);
        
        // 直接删除
        voxelSystem.DeleteVoxelType(_selectedSlot.slotId);
    }

    /// <summary>
    /// 刷新库存显示
    /// </summary>
    private void RefreshInventory()
    {
        if (_isInitialized)
        {
            RebuildInventory();
        }
    }
    
    /// <summary>
    /// 公共方法：设置Add按钮状态（供RuntimeVoxelBuilding调用）
    /// </summary>
    /// <param name="enable">true为打开Add按钮（创建模式），false为关闭</param>
    public void SetAddButtonState(bool enable)
    {
        if (addButton == null) return;
        
        if (enable && !_isAddButtonSelected)
        {
            // 如果要求打开但当前未打开，则点击按钮
            addButton.onClick.Invoke();
        }
        else if (!enable && _isAddButtonSelected)
        {
            // 如果要求关闭但当前已打开，则点击按钮
            addButton.onClick.Invoke();
        }
    }
    
    /// <summary>
    /// 公共方法：获取Add按钮当前状态
    /// </summary>
    public bool IsAddButtonSelected()
    {
        return _isAddButtonSelected;
    }



    private void OnDisable()
    {
        // 简单直接地停用UI面板
        if (voxelInventoryPanel != null)
        {
            voxelInventoryPanel.SetActive(false);
        }
        
        // 停用编辑UI
        if (voxelEditingUI != null)
        {
            voxelEditingUI.enabled = false;
        }
    }
    
    private void OnDestroy()
    {
        // 清理事件订阅
        VoxelRegistry.OnRegistryChanged -= HandleRegistryChanged;
        
        if (voxelSystem != null)
        {
            voxelSystem.onVoxelCreated.RemoveListener(HandleVoxelCreated);
            voxelSystem.onVoxelDeleted.RemoveListener(HandleVoxelDeleted);
            voxelSystem.onVoxelModified.RemoveListener(HandleVoxelModified);
        }
    }

    private void InitializeComponents()
    {
        if (_isInitialized) return;
        
        // 查找必要的组件
        if (slotsContainer == null)
        {
            slotsContainer = voxelInventoryPanel.transform.Find("VoxelView/Viewport/SlotsContainer");
            if (slotsContainer == null)
            {
                Debug.LogError("[VoxelInventoryUI] Slots container is not assigned!");
                return;
            }
        }

        if (slotPrefab == null)
        {
            Debug.LogError("[VoxelInventoryUI] Slot prefab is not assigned!");
            return;
        }

        if (voxelEditingUI == null)
        {
            voxelEditingUI = FindAnyObjectByType<VoxelEditingUI>();
            if (voxelEditingUI == null)
            {
                Debug.LogWarning("[VoxelInventoryUI] VoxelEditingUI not found!");
            }
        }

        if (voxelSystem == null)
        {
            voxelSystem = FindAnyObjectByType<VoxelSystemManager>();
            if (voxelSystem == null)
            {
                Debug.LogError("[VoxelInventoryUI] VoxelSystemManager not found!");
                return;
            }
        }

        // 设置按钮事件
        SetupButtons();
        
        // 订阅事件
        SubscribeToEvents();

        _isInitialized = true;
    }
    
    /// <summary>
    /// 订阅必要的事件
    /// </summary>
    private void SubscribeToEvents()
    {
        VoxelRegistry.OnRegistryChanged += HandleRegistryChanged;
        
        if (voxelSystem != null)
        {
            voxelSystem.onVoxelCreated.AddListener(HandleVoxelCreated);
            voxelSystem.onVoxelDeleted.AddListener(HandleVoxelDeleted);
            voxelSystem.onVoxelModified.AddListener(HandleVoxelModified);
        }
    }

    private void HandleRegistryChanged()
    {
        RebuildInventory();
    }

    private void HandleVoxelCreated(VoxelDefinition def)
    {
        RebuildInventory();
    }

    private void HandleVoxelDeleted(ushort typeId)
    {
        RebuildInventory();
    }

    private void HandleVoxelModified(VoxelDefinition def)
    {
        RebuildInventory();
    }

    private void RebuildInventory()
    {
        // 清理现有slots
        foreach (var slot in _slots)
        {
            if (slot != null)
            {
                Destroy(slot.gameObject);
            }
        }
        _slots.Clear();
        _selectedSlot = null;

        // 获取所有VoxelDefinition
        var definitions = VoxelRegistry.GetAllDefinitions();

        // 为每个definition创建一个slot，除了空气definition
        foreach (var def in definitions)
        {
            if (def == null || def.name == "Air")
            {
                continue;
            }
            
            //Debug.Log($"[VoxelInventoryUI] Creating slot for {def.name} (ID: {def.typeId})");
            var slot = Instantiate(slotPrefab, slotsContainer);
            slot.SetVoxel(def);
            
            var slotBtn = slot.GetComponent<Button>();
            if (slotBtn == null)
            {
                slotBtn = slot.gameObject.AddComponent<Button>();
            }

            var capturedSlot = slot;
            slotBtn.onClick.AddListener(() => OnSlotClicked(capturedSlot));

            _slots.Add(slot);
        }

        // 如果有一个及以上槽位，默认选择第一个
        if (_slots.Count > 0)
        {
            OnSlotClicked(_slots[0]);
        }
    }

    private void OnSlotClicked(VoxelInventorySlot slot)
    {
        if (_selectedSlot != null)
        {
            _selectedSlot.SetSelected(false);
        }

        _selectedSlot = slot;
        slot.SetSelected(true);
        UnselectOtherSlots(slot);
        OnVoxelTypeSelected?.Invoke(slot.VoxelDef);
        
        // 通过事件通知选中了新的方块类型
        // RuntimeVoxelBuilding会监听OnVoxelTypeSelected事件
        
        Debug.Log($"Selected voxel: {slot.VoxelDef.displayName}");
    }

    private void UnselectOtherSlots(VoxelInventorySlot slot)
    {
        foreach (var s in _slots)
        {
            if (s != slot)
            {
                s.SetSelected(false);
            }
        }
    }
}