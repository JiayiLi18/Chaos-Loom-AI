using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class Tool : MonoBehaviour
{
    // 工具名称（可用于UI显示）
    public string toolName;
    
    // 工具的主要功能：可以通过这个方法触发工具的行为
    public abstract void UseTool();

    // 激活工具时的处理
    public virtual void ActivateTool()
    {
        // 在此处执行工具激活时的通用行为（例如，显示UI，播放音效等）
        Debug.Log(toolName + " activated");
    }

    // 禁用工具时的处理
    public virtual void DeactivateTool()
    {
        // 在此处执行工具禁用时的通用行为（例如，隐藏UI，停止音效等）
        Debug.Log(toolName + " deactivated");
    }
}
