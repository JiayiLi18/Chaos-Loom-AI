using UnityEngine;
using Voxels;

/// <summary>
/// 体素创建工具，只负责工具的激活/禁用状态
/// </summary>
public class VoxelCreationTool : Tool
{
    private VoxelInventoryUI voxelInventoryUI;

    void OnEnable()
    {
        if (voxelInventoryUI == null)
        {
            voxelInventoryUI = FindAnyObjectByType<VoxelInventoryUI>();
        }
    }

    void OnDisable()
    {
        if (voxelInventoryUI != null)
        {
            voxelInventoryUI.enabled = false;
        }
    }

    public override void ActivateTool()
    {
        base.ActivateTool();
        
        if (voxelInventoryUI != null)
        {
            voxelInventoryUI.gameObject.SetActive(true);
            voxelInventoryUI.enabled = true;
            voxelInventoryUI.SetAddButtonState(true); // 自动激活add button
        }
    }

    public override void DeactivateTool()
    {
        base.DeactivateTool();
        
        if (voxelInventoryUI != null)
        {
            voxelInventoryUI.enabled = false;
            voxelInventoryUI.SetAddButtonState(false); // 取消add button状态
        }
    }
} 