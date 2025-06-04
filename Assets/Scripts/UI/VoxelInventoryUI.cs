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
    [SerializeField] private VoxelEditingUI voxelEditingUI;

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
        
        // 订阅VoxelSystemManager的事件
        if (voxelSystem != null)
        {
            voxelSystem.onVoxelCreated.AddListener(HandleVoxelCreated);
            voxelSystem.onVoxelDeleted.AddListener(HandleVoxelDeleted);
            voxelSystem.onVoxelModified.AddListener(HandleVoxelModified);
        }

        // 启用建造功能
        if (RuntimeVoxelBuilding.Instance != null)
        {
            RuntimeVoxelBuilding.Instance.enabled = true;
        }

        // 初始化时构建一次
        RebuildInventory();

        SetupButtons();
    }

    private void SetupButtons()
    {
        // 添加add button的点击事件
        if (addButton != null)
        {
            addButton.onClick.RemoveAllListeners();
            addButton.onClick.AddListener(() => {
                SetAddButtonState(!_isAddButtonSelected);
            });
        }

        if (editButton != null)
        {
            editButton.onClick.RemoveAllListeners();
            editButton.onClick.AddListener(() => {
                if (_selectedSlot == null) return;
                _isEditButtonSelected = !_isEditButtonSelected;
                editButton.image.color = _isEditButtonSelected ? selectedColor : normalColor;
                if (voxelEditingUI != null && _isEditButtonSelected)
                {
                    voxelEditingUI.gameObject.SetActive(true);
                    voxelEditingUI.enabled = true;
                    voxelEditingUI.SetEditMode(_selectedSlot.slotId);
                }
                else if (voxelEditingUI != null)
                {
                    voxelEditingUI.gameObject.SetActive(false);
                    voxelEditingUI.enabled = false;
                }
            });
        }

        if (deleteButton != null)
        {
            deleteButton.onClick.RemoveAllListeners();
            deleteButton.onClick.AddListener(() => {
                if (_selectedSlot == null) return;
                if (voxelSystem != null)
                {
                    voxelSystem.DeleteVoxelType(_selectedSlot.slotId);
                }
            });
        }

        // 初始化按钮颜色
        if (addButton != null) addButton.image.color = normalColor;
        if (editButton != null) editButton.image.color = normalColor;
    }

    /// <summary>
    /// 设置add button的状态
    /// </summary>
    /// <param name="state">true为选中状态，false为未选中状态</param>
    public void SetAddButtonState(bool state)
    {
        _isAddButtonSelected = state;
        if (addButton != null)
        {
            addButton.image.color = _isAddButtonSelected ? selectedColor : normalColor;
        }
        if (voxelEditingUI != null)
        {
            // 只在创建模式下显示编辑UI
            if (_isAddButtonSelected)
            {
                voxelEditingUI.gameObject.SetActive(true);
                voxelEditingUI.enabled = true;
                voxelEditingUI.SetCreateMode(); // 设置为创建模式
            }
            else
            {
                voxelEditingUI.gameObject.SetActive(false);
                voxelEditingUI.enabled = false;
            }
        }
    }

    private void OnDisable()
    {
        if (voxelInventoryPanel != null)
        {
            voxelInventoryPanel.SetActive(false);
        }
        VoxelRegistry.OnRegistryChanged -= HandleRegistryChanged;
        
        // 取消订阅VoxelSystemManager的事件
        if (voxelSystem != null)
        {
            voxelSystem.onVoxelCreated.RemoveListener(HandleVoxelCreated);
            voxelSystem.onVoxelDeleted.RemoveListener(HandleVoxelDeleted);
            voxelSystem.onVoxelModified.RemoveListener(HandleVoxelModified);
        }

        // 禁用建造功能
        if (RuntimeVoxelBuilding.Instance != null)
        {
            RuntimeVoxelBuilding.Instance.enabled = false;
        }

        if (voxelEditingUI != null)
        {
            voxelEditingUI.enabled = false;
        }

        _isAddButtonSelected = false;
        _isEditButtonSelected = false;
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
                enabled = false;
                return;
            }
        }

        _isInitialized = true;
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
            
            Debug.Log($"[VoxelInventoryUI] Creating slot for {def.name} (ID: {def.typeId})");
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
        
        // 通知RuntimeVoxelBuilding选中了新的方块类型
        if (RuntimeVoxelBuilding.Instance != null)
        {
            RuntimeVoxelBuilding.Instance.SetSelectedVoxelType((byte)slot.VoxelDef.typeId);
        }
        
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