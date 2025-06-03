using UnityEngine;
using Voxels;

/// <summary>
/// 体素创建工具，只负责工具的激活/禁用状态
/// </summary>
public class VoxelCreationTool : Tool
{
    private RuntimeVoxelTypeCreator voxelTypeCreator;

    void OnEnable()
    {
        if (voxelTypeCreator == null)
        {
            voxelTypeCreator = FindAnyObjectByType<RuntimeVoxelTypeCreator>();
        }
    }

    void OnDisable()
    {
        if (voxelTypeCreator != null)
        {
            voxelTypeCreator.enabled = false;
        }
    }

    public override void ActivateTool()
    {
        base.ActivateTool();
        
        // 激活顺序很重要：先激活RuntimeVoxelTypeCreator，它会负责激活和初始化PaintingToolUI
        if (voxelTypeCreator != null)
        {
            voxelTypeCreator.gameObject.SetActive(true);
            voxelTypeCreator.enabled = true;
        }
    }

    public override void DeactivateTool()
    {
        base.DeactivateTool();
        
        // 只禁用脚本组件
        if (voxelTypeCreator != null)
        {
            voxelTypeCreator.enabled = false;
        }
    }
} 