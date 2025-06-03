using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public abstract class Tool : MonoBehaviour
{
    // 工具名称（可用于UI显示）
    public string toolName;
    [SerializeField] private Button uiButton; // 引用UI按钮
    public Button UIButton => uiButton; // 公开访问UI按钮的属性
    [SerializeField] Color selectedColor = new Color(0.055f, 0.9f, 0.21f, 1f);
    [SerializeField] Color normalColor = new Color(0.23f, 0.23f, 0.23f, 1f);

    [Header("Gameplay State Settings")]
    [SerializeField] protected bool shouldChangeGameplayState = false; // 是否在激活时改变游戏状态
    [SerializeField] protected bool desiredGameplayState = true; // 激活时想要的游戏状态

    public GameObject toolObject;//暂时不用


    // 激活工具时的处理
    public virtual void ActivateTool()
    {
        // 在此处执行工具激活时的通用行为（例如，显示UI，播放音效等）
        Debug.Log(toolName + " activated");
        if (uiButton != null)
        {
            uiButton.interactable = false;
            uiButton.image.color = selectedColor;
        }
        if (toolObject != null)
        {
            toolObject.SetActive(true);
        }

        // 如果工具配置为改变游戏状态，则执行改变
        if (shouldChangeGameplayState)
        {
            UIStateManager.SetGameplayState(desiredGameplayState);
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
            uiButton.image.color = normalColor;
        }
        if (toolObject != null)
        {
            toolObject.SetActive(false);
        }
    }
}
