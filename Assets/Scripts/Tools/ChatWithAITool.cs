using UnityEngine;

/// <summary>
/// AI聊天工具，负责工具的激活/禁用状态
/// </summary>
public class ChatWithAITool : Tool
{
    private RuntimeAIChat _aiChat;

    private void Start()
    {
        _aiChat = FindAnyObjectByType<RuntimeAIChat>();
        if (_aiChat == null)
        {
            Debug.LogError("找不到RuntimeAIChat组件，请确保场景中存在该组件");
            enabled = false;
        }
    }

    public override void ActivateTool()
    {
        base.ActivateTool();
        if (_aiChat != null)
        {
            _aiChat.enabled = true;
        }
    }

    public override void DeactivateTool()
    {
        base.DeactivateTool();
        if (_aiChat != null)
        {
            _aiChat.enabled = false;
        }
    }
} 