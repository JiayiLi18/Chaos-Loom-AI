using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HandTool : Tool
{
    public override void UseTool()
    {
        // 实现空手工具的功能
        Debug.Log("Using hand tool!");
    }

    public override void ActivateTool()
    {
        base.ActivateTool();
    }

    public override void DeactivateTool()
    {
        base.DeactivateTool();
    }
}
