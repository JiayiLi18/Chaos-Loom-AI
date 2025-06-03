using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HandTool : Tool
{

    public override void ActivateTool()
    {
        base.ActivateTool();
        Debug.Log("Using hand tool!");
    }

    public override void DeactivateTool()
    {
        base.DeactivateTool();
        Debug.Log("Deactivating hand tool!");
    }
}
