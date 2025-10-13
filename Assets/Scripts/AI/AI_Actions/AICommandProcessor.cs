using System.Text.RegularExpressions;
using UnityEngine;
using Voxels;

/// <summary>
/// Receives raw ChatGPT text, extracts JSON commands and executes them.
/// </summary>
public class AICommandProcessor : MonoBehaviour
{
    [SerializeField] private ChatUI chatUIReference;
    [SerializeField] private VoxelJsonDB voxelDBReference;

    void OnEnable()
    {
        if (chatUIReference == null)
        {
            chatUIReference = FindAnyObjectByType<ChatUI>();
        }
    }

    // --------------------------------- Public Entry ---------------------------------
    public void ProcessPlannerResponse(ParsedMessage message)
    {
        // TODO: 重新实现命令处理逻辑
        Debug.Log("AICommandProcessor - ProcessResponse called (placeholder)");
    }

    // --------------------------------- Command Processing ---------------------------------
    // TODO: 重新实现命令处理逻辑
    public void ProcessCommandResponse(ParsedMessage message)
    {
        // TODO: 重新实现命令处理逻辑
        Debug.Log("AICommandProcessor - ProcessCommandResponse called (placeholder)");
    }
}