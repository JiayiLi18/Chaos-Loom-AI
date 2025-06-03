using UnityEngine;
using Voxels;

/// <summary>
/// 体素建造工具，只负责工具的激活/禁用状态
/// </summary>
public class VoxelBuildingTool : Tool
{
    private RuntimeVoxelBuilding building;


    void OnEnable()
    {
        if (building == null)
        {
            building = FindAnyObjectByType<RuntimeVoxelBuilding>();
        }
    }

    void OnDisable()
    {
        if (building != null)
        {
            building.enabled = false;
        }
    }

    public override void ActivateTool()
    {
        base.ActivateTool();
        building.enabled = true;
    }

    public override void DeactivateTool()
    {
        base.DeactivateTool();
        building.enabled = false;
    }
} 