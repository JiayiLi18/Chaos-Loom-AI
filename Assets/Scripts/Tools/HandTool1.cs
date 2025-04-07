using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TakePicsTool : Tool
{

    [SerializeField] private GameObject TakePicsUI;
    public override void UseTool()
    {
        Debug.Log("Using take pics tool!");
    }

    public override void ActivateTool()
    {
        base.ActivateTool();
        TakePicsUI.SetActive(true);
    }

    public override void DeactivateTool()
    {
        base.DeactivateTool();
        TakePicsUI.SetActive(false);
    }
}
