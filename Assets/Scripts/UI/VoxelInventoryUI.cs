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
    [SerializeField] private GameObject addButtonPrefab; // Add button prefab（类似PhotoInventoryUI的openCamButtonPrefab）
    [SerializeField] private bool useAddButton = false; // 是否使用add button（每个实例可单独配置）

    [Header("Dependencies")]
    [SerializeField] private VoxelSystemManager voxelSystem;

    private List<VoxelInventorySlot> _slots = new List<VoxelInventorySlot>();
    public VoxelInventorySlot _selectedSlot;
    private bool _isInitialized = false;
    private ushort? _pendingSelectVoxelId = null; // 待选择的voxel ID（在重建inventory后选择）
    private GameObject _addButton; // 保存add button引用，避免被销毁

    // 事件系统
    public event System.Action<VoxelDefinition> OnVoxelTypeSelected;

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
    
    private void OnDisable()
    {
        // 简单直接地停用UI面板
        if (voxelInventoryPanel != null)
        {
            voxelInventoryPanel.SetActive(false);
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
        
        // 清理add button引用
        CleanupAddButton();
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

        if (voxelSystem == null)
        {
            voxelSystem = FindAnyObjectByType<VoxelSystemManager>();
            if (voxelSystem == null)
            {
                Debug.LogError("[VoxelInventoryUI] VoxelSystemManager not found!");
                return;
            }
        }
        
        // 订阅事件
        SubscribeToEvents();

        _isInitialized = true;

        Debug.Log("[VoxelInventoryUI] Initialized successfully");
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
        // 记录要选择的voxel ID
        if (def != null && def.typeId > 0)
        {
            _pendingSelectVoxelId = def.typeId;
        }
        RebuildInventory();
    }

    private void HandleVoxelDeleted(ushort typeId)
    {
        RebuildInventory();
    }

    private void HandleVoxelModified(VoxelDefinition def)
    {
        // 记录要选择的voxel ID
        if (def != null && def.typeId > 0)
        {
            _pendingSelectVoxelId = def.typeId;
        }
        RebuildInventory();
    }

    private void RebuildInventory()
    {
        // 清理现有slots（跳过add button）
        foreach (var slot in _slots)
        {
            if (slot != null)
            {
                Destroy(slot.gameObject);
            }
        }
        _slots.Clear();
        _selectedSlot = null;

        // 根据配置决定是否显示add button
        if (useAddButton)
        {
            // 确保add button在第一个位置（类似PhotoInventoryUI的处理方式）
            EnsureAddButton();
        }
        else
        {
            // 如果不需要add button，清理已存在的
            CleanupAddButton();
        }

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

        // 如果有待选择的voxel ID，选择它；否则默认选择第一个
        if (_pendingSelectVoxelId.HasValue)
        {
            bool selected = SelectVoxelById(_pendingSelectVoxelId.Value);
            _pendingSelectVoxelId = null; // 清除待选择标记
            if (!selected && _slots.Count > 0)
            {
                // 如果没找到对应的slot，默认选择第一个
                OnSlotClicked(_slots[0]);
            }
        }
        else if (_slots.Count > 0)
        {
            // 如果没有待选择的voxel，默认选择第一个
            OnSlotClicked(_slots[0]);
        }
    }
    
    /// <summary>
    /// 确保add button在第一个位置（类似PhotoInventoryUI的openCamButtonPrefab处理）
    /// </summary>
    private void EnsureAddButton()
    {
        if (addButtonPrefab == null || slotsContainer == null) return;
        
        // 如果add button不存在，创建它
        if (_addButton == null)
        {
            _addButton = Instantiate(addButtonPrefab, slotsContainer);
            _addButton.name = "AddVoxelButton"; // 给一个标识名称
            
            // 设置按钮点击事件
            Button btn = _addButton.GetComponentInChildren<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(OnAddButtonClicked);
            }
        }
        
        // 确保add button在第一个位置（设置sibling index为0）
        _addButton.transform.SetSiblingIndex(0);
    }
    
    /// <summary>
    /// 清理add button（当不需要时）
    /// </summary>
    private void CleanupAddButton()
    {
        if (_addButton != null)
        {
            Destroy(_addButton);
            _addButton = null;
        }
    }
    
    /// <summary>
    /// 获取add button的引用（供VoxelEditingUI调用）
    /// </summary>
    public Button GetAddButton()
    {
        if (useAddButton && _addButton != null)
        {
            return _addButton.GetComponentInChildren<Button>();
        }
        return null;
    }
    
    /// <summary>
    /// Add button点击事件处理
    /// </summary>
    private void OnAddButtonClicked()
    {
        // 查找VoxelEditingUI并调用ToggleAddButton
        var editingUI = FindAnyObjectByType<Voxels.VoxelEditingUI>();
        if (editingUI != null)
        {
            editingUI.ToggleAddButton();
        }
        else
        {
            Debug.LogWarning("[VoxelInventoryUI] VoxelEditingUI not found!");
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
    
    /// <summary>
    /// 根据voxel ID选择对应的slot（公共方法，供外部调用）
    /// </summary>
    /// <param name="voxelId">要选择的voxel ID</param>
    /// <returns>是否成功找到并选择了对应的slot</returns>
    public bool SelectVoxelById(ushort voxelId)
    {
        if (voxelId == 0)
        {
            return false;
        }
        
        // 查找对应的slot
        foreach (var slot in _slots)
        {
            if (slot != null && slot.slotId == voxelId)
            {
                OnSlotClicked(slot);
                return true;
            }
        }
        
        Debug.LogWarning($"[VoxelInventoryUI] Cannot find slot for voxel ID {voxelId}");
        return false;
    }
}