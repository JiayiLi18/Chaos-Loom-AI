using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 可能会用到但现在没有使用
/// </summary>
public class Painting3DTool : Tool
{
    [SerializeField] private Painting3DController painting3DController; // Reference to the DrawingController

    public override void ActivateTool()
    {
        base.ActivateTool(); // 调用父类的 ActivateTool 方法
        // 这里可以添加 PaintTool 特有的激活逻辑
        if (painting3DController != null)
        {
            painting3DController.canDraw=true; // Call the method to start drawing
            painting3DController.penObject = toolObject;
        }
        else
        {
            Debug.LogError("DrawingController reference is not set!");
        }
    }

    public override void DeactivateTool()
    {
        base.DeactivateTool(); // 调用父类的 DeactivateTool 方法
        // 这里可以添加 PaintTool 特有的禁用逻辑
        if (painting3DController != null)
        {
            painting3DController.canDraw = false; 
        }
        else
        {
            Debug.LogError("DrawingController reference is not set!");
        }
    }
}