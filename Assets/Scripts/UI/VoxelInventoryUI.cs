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

    [Header("Dependencies")]
    [SerializeField] private VoxelSystemManager voxelSystem;

    private List<VoxelInventorySlot> _slots = new List<VoxelInventorySlot>();
    public VoxelInventorySlot _selectedSlot;
    private bool _isInitialized = false;

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