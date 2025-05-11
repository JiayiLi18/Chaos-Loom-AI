using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// 接收 ChatGPT 原始文本，提取其中的 JSON 指令并执行。
/// </summary>
public class AICommandProcessor : MonoBehaviour
{
    [SerializeField] private ChatManager chatManagerReference;

    // --------------------------------- 对外入口 ---------------------------------
    public void ProcessResponse(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            Debug.LogWarning("AICommandProcessor - 收到空响应。");
            return;
        }

        chatManagerReference.OnReceiveMessage(rawText);//之后再仔细处理

        // ① 用正则捕获第一段 {...}（最常见的场景足够）
        Match m = Regex.Match(rawText, @"\{[\s\S]*?\}");
        if (!m.Success)
        {
            Debug.LogError("AICommandProcessor - 未找到 JSON 指令段。");
            return;
        }

        string json = m.Value;

        // ② 先只解析 command 字段，决定走哪条分支
        BaseCommand baseCmd;
        try
        {
            baseCmd = JsonUtility.FromJson<BaseCommand>(json);
        }
        catch
        {
            Debug.LogError($"AICommandProcessor - JsonUtility 解析失败：\n{json}");
            return;
        }

        switch (baseCmd.command)
        {
            case "create_voxel_type":
                CreateVoxelTypeData data = JsonUtility.FromJson<CreateVoxelTypeData>(json);
                HandleCreateVoxelType(data);//之后这一步以前还可以加一个是否确认执行的按钮
                break;

            default:
                Debug.LogWarning($"AICommandProcessor - 未识别的指令：{baseCmd.command}");
                break;
        }
    }

    // --------------------------------- 指令处理 ---------------------------------
    private static void HandleCreateVoxelType(CreateVoxelTypeData d)
    {
        if (!ColorUtility.TryParseHtmlString(d.baseColor, out var color))
        {
            Debug.LogError($"create_voxel_type - 颜色解析失败：{d.baseColor}");
            return;
        }

        ushort newId = RuntimeVoxelTypeCreator.CreateVoxelType(d.displayName, color);
        //此处可以在聊天框显示“successfully created voxel type XXX”
    }

    // --------------------------------- 数据结构 ---------------------------------
    [System.Serializable] private struct BaseCommand
    {
        public string command;
    }

    [System.Serializable] private struct CreateVoxelTypeData
    {
        public string command;
        public string displayName;
        public string baseColor;
        public string description;
    }
}