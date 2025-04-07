using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public abstract class Tool : MonoBehaviour
{
    // 工具名称（可用于UI显示）
    public string toolName;
    [SerializeField] private Button uiButton; // 引用UI按钮
    [SerializeField] Color selectedColor;
    [SerializeField] Color normalColor;

    public GameObject toolObject;

    // 工具的主要功能：可以通过这个方法触发工具的行为
    public abstract void UseTool();

    // 激活工具时的处理
    public virtual void ActivateTool()
    {
        // 在此处执行工具激活时的通用行为（例如，显示UI，播放音效等）
        Debug.Log(toolName + " activated");
        if (uiButton != null)
        {
            uiButton.interactable = false;

            ColorBlock colors = uiButton.colors;
            colors.normalColor = selectedColor;

            uiButton.colors = colors;
        }
        if (toolObject != null)
        {
            toolObject.SetActive(true);
        }
    }

    // 禁用工具时的处理
    public virtual void DeactivateTool()
    {
        // 在此处执行工具禁用时的通用行为（例如，隐藏UI，停止音效等）
        Debug.Log(toolName + " deactivated");
        if (uiButton != null)
        {
            uiButton.interactable = true;

            ColorBlock colors = uiButton.colors;
            colors.normalColor = normalColor;

            uiButton.colors = colors;
        }
        if (toolObject != null)
        {
            toolObject.SetActive(false);
        }
    }
}
