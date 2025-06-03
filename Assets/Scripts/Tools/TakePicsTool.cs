using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TakePicsTool : Tool
{
    private RuntimePhotoTaker _photoTaker;

    void OnEnable()
    {
        if (_photoTaker == null)
        {
            _photoTaker = FindAnyObjectByType<RuntimePhotoTaker>();
        }
    }

    void OnDisable()
    {
        if (_photoTaker != null)
        {
            _photoTaker.enabled = false;
        }
    }
    
    public override void ActivateTool()
    {
        base.ActivateTool();
        if (_photoTaker != null)
        {
            _photoTaker.enabled = true;
        }
    }

    public override void DeactivateTool()
    {
        base.DeactivateTool();
        if (_photoTaker != null)
        {
            _photoTaker.enabled = false;
        }
    }
}
