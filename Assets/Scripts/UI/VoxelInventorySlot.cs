using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Voxels;
using UnityEngine.Events;
using System.Runtime.CompilerServices;

public class VoxelInventorySlot : MonoBehaviour
{
    [SerializeField] private RawImage iconImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private Image frameImage;
    [SerializeField] private Button editButton;
    [SerializeField] private Color selectedColor = new Color(0.055f, 0.9f, 0.21f, 1f);
    [SerializeField] private Color normalColor = new Color(0.23f, 0.23f, 0.23f, 1f);
    
    private VoxelDefinition _voxelDef;
    private bool _isSelected;
    
    public VoxelDefinition VoxelDef => _voxelDef;
    public ushort slotId; //slot的id就是voxel的唯一typeId

    public event UnityAction<ushort> OnEditClicked;


    private void OnEnable()
    {
        // 确保组件引用正确
        if (iconImage == null)
        {
            Debug.LogError($"[VoxelInventorySlot] RawImage component not assigned on {gameObject.name}");
        }

        // 设置编辑按钮点击事件
        if (editButton != null)
        {
            editButton.onClick.AddListener(() => OnEditClicked?.Invoke(slotId));
        }
    }

    private void OnDestroy()
    {
        if (editButton != null)
        {
            editButton.onClick.RemoveAllListeners();
        }
    }
    
    public void SetVoxel(VoxelDefinition def)
    {
        _voxelDef = def;
        if (def != null)
        {
            slotId = def.typeId;
            // 使用体素的贴图作为图标
            if (def.texture != null)
            {
                Debug.Log($"[VoxelInventorySlot] Setting texture for {def.displayName}: {def.texture.name}, size: {def.texture.width}x{def.texture.height}, format: {def.texture.format}");
                iconImage.texture = def.texture;
                iconImage.color = Color.white; // 重置为白色以显示原始贴图颜色
                
                // 设置UV以确保贴图正确显示
                iconImage.uvRect = new Rect(0, 0, 1, 1);
            }
            else
            {
                Debug.LogWarning($"[VoxelInventorySlot] No texture found for {def.displayName}, using color instead");
                iconImage.texture = null;
                iconImage.color = def.baseColor;
            }
            
            nameText.text = def.displayName;
            gameObject.SetActive(true);

            // 如果是Air voxel，隐藏编辑按钮
            if (editButton != null)
            {
                editButton.gameObject.SetActive(def.name != "Air");
            }
        }
        else
        {
            Debug.LogWarning("[VoxelInventorySlot] Null VoxelDefinition provided");
            iconImage.texture = null;
            iconImage.color = Color.white;
            if (editButton != null)
            {
                editButton.gameObject.SetActive(false);
            }
            gameObject.SetActive(false);
        }
    }
    
    public void SetSelected(bool selected)
    {
        _isSelected = selected;
        frameImage.color = selected ? selectedColor : normalColor;
    }
} 