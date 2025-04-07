using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PaintSurfTool : Tool
{
    [SerializeField] MousePainter mousePainter;

    public override void UseTool()
    {
    }

    public override void ActivateTool()
    {
        base.ActivateTool(); // 调用父类的 ActivateTool 方法
        if(mousePainter != null)
        {
            mousePainter.canDraw = true; // Call the method to start drawing
            PlayerMove.canLook = false; // 禁用相机旋转
        }
        else
        {
            Debug.LogError("MousePainter reference is not set!");
        }
    }

    public override void DeactivateTool()
    {
        base.DeactivateTool(); // 调用父类的 DeactivateTool 方法
        if(mousePainter != null)
        {
            mousePainter.canDraw = false; // Call the method to stop drawing
            PlayerMove.canLook = true; // 启用相机旋转
        }
        else
        {
            Debug.LogError("MousePainter reference is not set!");
        }
    }
}