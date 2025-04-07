using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PaintTool : Tool
{
    [SerializeField] private DrawingController drawingController; // Reference to the DrawingController

    public override void UseTool()
    {
    }

    public override void ActivateTool()
    {
        base.ActivateTool(); // 调用父类的 ActivateTool 方法
        // 这里可以添加 PaintTool 特有的激活逻辑
        if (drawingController != null)
        {
            drawingController.canDraw=true; // Call the method to start drawing
            drawingController.penObject = toolObject;

            PlayerMove.canLook = false;// 禁用相机旋转
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
        if (drawingController != null)
        {
            drawingController.canDraw = false; 
            PlayerMove.canLook = true;
        }
        else
        {
            Debug.LogError("DrawingController reference is not set!");
        }
    }
}