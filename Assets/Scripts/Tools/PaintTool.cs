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
        // 实现画笔工具的功能
        Debug.Log("Using paintbrush!");

        // Start drawing when the paint tool is used
        if (drawingController != null)
        {
            drawingController.canDraw=true; // Call the method to start drawing
            PlayerMove.canLook = false;// 禁用相机旋转
        }
        
        else
        {
            Debug.LogError("DrawingController reference is not set!");
        }
    }

    public override void DeactivateTool()
    {
        if (drawingController != null)
        {
            drawingController.canDraw=false; 
            PlayerMove.canLook = true;
        }
        else
        {
            Debug.LogError("DrawingController reference is not set!");
        }
    }
}