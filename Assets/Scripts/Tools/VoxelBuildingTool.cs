using UnityEngine;
using Voxels;

/// <summary>
/// 体素建造工具，只负责工具的激活/禁用状态
/// </summary>
public class VoxelBuildingTool : Tool
{
    private VoxelInventoryUI voxelInventoryUI;
    private RuntimeVoxelBuilding runtimeVoxelBuilding;

    void OnEnable()
    {
        if (voxelInventoryUI == null)
        {
            voxelInventoryUI = FindAnyObjectByType<VoxelInventoryUI>();
        }
        if (runtimeVoxelBuilding == null)
        {
            runtimeVoxelBuilding = RuntimeVoxelBuilding.Instance;
        }
    }

    void OnDisable()
    {
        if (voxelInventoryUI != null)
        {
            voxelInventoryUI.enabled = false;
        }
        if (runtimeVoxelBuilding != null)
        {
            runtimeVoxelBuilding.enabled = false;
        }
    }

    public override void ActivateTool()
    {
        base.ActivateTool();
        
        if (voxelInventoryUI != null)
        {
            voxelInventoryUI.gameObject.SetActive(true);
            voxelInventoryUI.enabled = true;
            voxelInventoryUI.SetAddButtonState(false); // 确保building模式下add button是关闭的
        }
        if (runtimeVoxelBuilding != null)
        {
            runtimeVoxelBuilding.enabled = true;
        }
    }

    public override void DeactivateTool()
    {
        base.DeactivateTool();
        
        if (voxelInventoryUI != null)
        {
            voxelInventoryUI.enabled = false;
        }
        if (runtimeVoxelBuilding != null)
        {
            runtimeVoxelBuilding.enabled = false;
        }
    }
} 