using UnityEngine;
using Voxels;

/// <summary>
/// 体素创建工具，负责工具的激活/禁用状态和UI管理
/// </summary>
public class VoxelCreationTool : Tool
{
    [SerializeField] private RuntimeVoxelBuilding runtimeVoxelBuilding;

    void OnEnable()
    {
        if (runtimeVoxelBuilding == null)
        {
            Debug.LogError("找不到RuntimeVoxelBuilding组件，请确保场景中存在该组件");
        }
    }

    public override void ActivateTool()
    {
        base.ActivateTool();
        if (runtimeVoxelBuilding != null)
        {
            runtimeVoxelBuilding.enabled = true;
            runtimeVoxelBuilding.EnterCreationMode();
        }
    }

    public override void DeactivateTool()
    {
        base.DeactivateTool();
        if (runtimeVoxelBuilding != null)
        {
            runtimeVoxelBuilding.enabled = false;
        }
    }
} 