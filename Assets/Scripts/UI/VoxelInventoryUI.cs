using UnityEngine;
using System.Collections.Generic;
using Voxels;
using UnityEngine.Events;

public class VoxelInventoryUI : MonoBehaviour
{
    [Header("UI Source")]
    [SerializeField] private GameObject voxelInventoryPanel;
    private Transform slotsContainer;
    [SerializeField] private VoxelInventorySlot slotPrefab;
    
    private List<VoxelInventorySlot> _slots = new List<VoxelInventorySlot>();
    public VoxelInventorySlot _selectedSlot;
    private bool _isInitialized = false;

    // 添加选择事件
    public event System.Action<VoxelDefinition> OnVoxelTypeSelected;
    
    private void OnEnable()
    {
        if (!_isInitialized)
        {
            InitializeComponents();
        }
        voxelInventoryPanel.SetActive(true);
        VoxelRegistry.OnRegistryChanged += HandleRegistryChanged;
        // 初始化时构建一次
        RebuildInventory();
    }
    
    private void OnDisable()
    {
        if (voxelInventoryPanel != null)
        {
            voxelInventoryPanel.SetActive(false);
        }
        VoxelRegistry.OnRegistryChanged -= HandleRegistryChanged;
    }

    private void InitializeComponents()
    {
        if (slotsContainer == null)
        {
            slotsContainer = voxelInventoryPanel.transform.Find("VoxelView/Viewport/SlotsContainer");
            if (slotsContainer == null)
            {
                Debug.LogError("[VoxelInventoryUI] Slots container is not assigned!");
                enabled = false;
                return;
            }
        }

        if (slotPrefab == null)
        {
            Debug.LogError("[VoxelInventoryUI] Slot prefab is not assigned!");
            enabled = false;
            return;
        }

        _isInitialized = true;
    }
    
    private void HandleRegistryChanged()
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
            if (def.name == "Air")
            {
                continue;
            }
            Debug.Log($"[VoxelInventoryUI] Creating slot for {def.name}");
            var slot = Instantiate(slotPrefab, slotsContainer);
            slot.SetVoxel(def);
            //确保slotPrefab有button组件
            if (slot.GetComponent<UnityEngine.UI.Button>() == null)
            {
                slot.gameObject.AddComponent<UnityEngine.UI.Button>();
            }
            
            // 添加点击事件
            var slotBtn = slot.GetComponent<UnityEngine.UI.Button>();
            if (slotBtn != null)
            {
                var capturedSlot = slot; // 捕获当前slot的引用
                slotBtn.onClick.AddListener(() => OnSlotClicked(capturedSlot));
            }
            
            _slots.Add(slot);
        }

        // 如果有一个及以上槽位，默认选择第一个
        if (_slots.Count > 0)
        {
            OnSlotClicked(_slots[0]);
        }
        else if(_slots.Count == 0)
        {
            Debug.LogWarning("[VoxelInventoryUI] No slots found");
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
        // 触发选中事件
        OnVoxelTypeSelected?.Invoke(slot.VoxelDef);
        Debug.Log($"Selected voxel: {slot.VoxelDef.displayName}");
    }

    //一次只能选中一个，其他的取消选中
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