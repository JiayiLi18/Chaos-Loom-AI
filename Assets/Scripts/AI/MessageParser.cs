using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public struct PlanBatch
{
    public string session_id;
    public string goal_id;
    public string goal_label;
    public string talk_to_player;
    public PlanItem[] plan;
}

[System.Serializable]
public struct PlanItem
{
    public string id;
    public string action_type;
    public string description;
    public string[] depends_on;
}

/// <summary>
/// Plan Permission Request - 用于发送批准的计划给API
/// </summary>
[System.Serializable]
public class PlanPermissionRequest
{
    public string session_id;
    public string goal_id;
    public string goal_label;
    public string additional_info;
    public PlanItem[] approved_plans;
    public GameState game_state;

    public PlanPermissionRequest(string sessionId, string goalId, string goalLabel, string additionalInfo, PlanItem[] approvedPlans, GameState gameState)
    {
        this.session_id = sessionId;
        this.goal_id = goalId;
        this.goal_label = goalLabel;
        this.additional_info = additionalInfo;
        this.approved_plans = approvedPlans;
        this.game_state = gameState;
    }

    /// <summary>
    /// 构造与后端匹配的 JSON（类似 EventBatch.ToJson）
    /// </summary>
    public string ToJson()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append('{');
        
        // session_id
        sb.Append("\"session_id\":\"").Append(Escape(session_id)).Append("\",");
        
        // goal_id
        sb.Append("\"goal_id\":\"").Append(Escape(goal_id)).Append("\",");
        
        // goal_label
        sb.Append("\"goal_label\":\"").Append(Escape(goal_label)).Append("\",");
        
        // additional_info
        if (!string.IsNullOrEmpty(additional_info))
        {
            sb.Append("\"additional_info\":\"").Append(Escape(additional_info)).Append("\",");
        }
        
        // approved_plans
        sb.Append("\"approved_plans\":[");
        if (approved_plans != null && approved_plans.Length > 0)
        {
            for (int i = 0; i < approved_plans.Length; i++)
            {
                var plan = approved_plans[i];
                sb.Append('{');
                sb.Append("\"id\":\"").Append(Escape(plan.id)).Append("\",");
                sb.Append("\"action_type\":\"").Append(Escape(plan.action_type)).Append("\",");
                sb.Append("\"description\":\"").Append(Escape(plan.description)).Append("\",");
                
                // depends_on
                sb.Append("\"depends_on\":");
                if (plan.depends_on != null && plan.depends_on.Length > 0)
                {
                    sb.Append('[');
                    for (int j = 0; j < plan.depends_on.Length; j++)
                    {
                        sb.Append("\"").Append(Escape(plan.depends_on[j])).Append("\"");
                        if (j < plan.depends_on.Length - 1) sb.Append(',');
                    }
                    sb.Append(']');
                }
                else
                {
                    sb.Append("null");
                }
                
                sb.Append('}');
                if (i < approved_plans.Length - 1) sb.Append(',');
            }
        }
        sb.Append(']');
        
        // game_state（可选）
        if (game_state != null)
        {
            sb.Append(',');
            // 使用 EventBatch.BuildGameStateJson 确保正确序列化 params 字段（而不是 params_data）
            sb.Append("\"game_state\":").Append(EventBatch.BuildGameStateJson(game_state));
        }
        
        sb.Append('}');
        return sb.ToString();
    }
    
    private static string Escape(string s)
    {
        if (string.IsNullOrEmpty(s)) return s ?? string.Empty;
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}

public static class MessageParser 
{
    public static PlanBatch ParsePlanBatch(string jsonString)
    {
        if (string.IsNullOrEmpty(jsonString))
        {
            return new PlanBatch();
        }

        try 
        {
            // 先检查JSON是否包含PlanBatch特有的字段（plan 或 talk_to_player）
            // 这样可以区分 PlanBatch 和 CommandBatch
            if (!jsonString.Contains("\"plan\"") && !jsonString.Contains("\"talk_to_player\""))
            {
                // 不包含PlanBatch特有字段，可能不是PlanBatch
                return new PlanBatch();
            }

            PlanBatch planBatch = JsonUtility.FromJson<PlanBatch>(jsonString);
            
            if (planBatch.session_id == null || planBatch.goal_id == null)
            {
                // 可能不是PlanBatch格式，返回空结构
                return new PlanBatch();
            }

            if (string.IsNullOrEmpty(planBatch.session_id) || string.IsNullOrEmpty(planBatch.goal_id))
            {
                // 缺少必需字段，可能不是PlanBatch
                return new PlanBatch();
            }

            Debug.Log($"Parsed PlanBatch: session_id={planBatch.session_id}, goal_id={planBatch.goal_id}, plan count={planBatch.plan?.Length ?? 0}");
            return planBatch;
        }
        catch (System.Exception e)
        {
            // 解析失败不一定是错误，可能是其他格式
            Debug.Log($"ParsePlanBatch: Failed to parse (may not be PlanBatch): {e.Message}");
            return new PlanBatch();
        }
    }
    
    public static CommandBatch ParseCommandBatch(string jsonString)
    {
        if (string.IsNullOrEmpty(jsonString))
        {
            return new CommandBatch("", "");
        }

        try 
        {
            // 先检查JSON是否包含CommandBatch特有的字段（commands）
            // 这样可以区分 CommandBatch 和 PlanBatch
            if (!jsonString.Contains("\"commands\""))
            {
                // 不包含commands字段，可能不是CommandBatch
                return new CommandBatch("", "");
            }

            // API 返回的 JSON 使用 "params" 字段，但 CommandData 使用 "params_data"
            // 需要先替换字段名以便 JsonUtility 正确解析
            string modifiedJson = jsonString.Replace("\"params\"", "\"params_data\"");
            
            // 使用修改后的 JSON 解析基本结构
            CommandBatch commandBatch = JsonUtility.FromJson<CommandBatch>(modifiedJson);
            
            // 验证必需字段
            if (commandBatch == null)
            {
                Debug.LogWarning("ParseCommandBatch: JsonUtility returned null, attempting manual parse");
                return ParseCommandBatchManually(jsonString);
            }

            if (string.IsNullOrEmpty(commandBatch.session_id) || string.IsNullOrEmpty(commandBatch.goal_id))
            {
                // 这不是错误，可能不是CommandBatch格式
                return new CommandBatch("", "");
            }

            // 验证commands列表（可以为空，但必须存在）
            if (commandBatch.commands == null)
            {
                commandBatch.commands = new System.Collections.Generic.List<CommandData>();
            }

            // 尝试解析每个命令的 params_data
            // 如果 JsonUtility 没有正确解析（例如 params_data 仍然是字典或字符串），需要进一步处理
            ParseCommandParams(commandBatch.commands, jsonString);

            Debug.Log($"Parsed CommandBatch: session_id={commandBatch.session_id}, goal_id={commandBatch.goal_id}, command count={commandBatch.commands.Count}");
            return commandBatch;
        }
        catch (System.Exception e)
        {
            // 解析失败不一定是错误，可能是其他格式的响应
            Debug.Log($"ParseCommandBatch: Failed to parse (may not be CommandBatch): {e.Message}");
            // 尝试手动解析
            try
            {
                return ParseCommandBatchManually(jsonString);
            }
            catch
            {
                return new CommandBatch("", "");
            }
        }
    }
    
    /// <summary>
    /// 手动解析 CommandBatch（当 JsonUtility 失败时使用）
    /// </summary>
    private static CommandBatch ParseCommandBatchManually(string jsonString)
    {
        // 简单的手动解析实现（可以后续使用 SimpleJSON 或 Newtonsoft.Json 替代）
        // 目前返回空，让上层处理
        Debug.LogWarning("ParseCommandBatchManually: Manual parsing not fully implemented, JsonUtility should handle basic cases");
        return new CommandBatch("", "");
    }
    
    /// <summary>
    /// 解析命令参数（处理 JsonUtility 可能无法正确解析的 object params_data）
    /// </summary>
    private static void ParseCommandParams(List<CommandData> commands, string originalJson)
    {
        if (commands == null || commands.Count == 0)
        {
            return;
        }

        // 尝试从原始 JSON 中手动解析每个命令的 params 字段
        // 因为 JsonUtility 可能无法正确解析 object 类型的 params_data
        foreach (var command in commands)
        {
            if (command == null)
            {
                Debug.LogWarning("ParseCommandParams: Found null command in list");
                continue;
            }

            // 如果 params_data 已经是对象，尝试从原始 JSON 中提取 params 字段
            if (command.params_data == null)
            {
                // 尝试从原始 JSON 中提取该命令的 params 字段
                string paramsJson = ExtractParamsFromJson(originalJson, command.id);
                if (!string.IsNullOrEmpty(paramsJson))
                {
                    // 将 params JSON 字符串存储为 params_data
                    // 执行器会负责将字符串解析为具体类型
                    command.params_data = paramsJson;
                    Debug.Log($"ParseCommandParams: Extracted params for command {command.id}: {paramsJson}");
                }
                else
                {
                    Debug.LogWarning($"ParseCommandParams: Command {command.id} (type: {command.type}) has null params_data, will be parsed by executor");
                }
            }
        }
    }
    
    /// <summary>
    /// 从原始 JSON 中提取指定命令的 params 字段
    /// </summary>
    private static string ExtractParamsFromJson(string json, string commandId)
    {
        try
        {
            // 简单的方法：查找命令 ID 并提取紧随其后的 "params" 对象
            int commandIndex = json.IndexOf($"\"id\":\"{commandId}\"");
            if (commandIndex == -1)
            {
                return null;
            }
            
            // 查找 "params" 字段
            int paramsIndex = json.IndexOf("\"params\"", commandIndex);
            if (paramsIndex == -1)
            {
                // 如果字段名已经被替换为 params_data，也尝试查找
                paramsIndex = json.IndexOf("\"params_data\"", commandIndex);
            }
            
            if (paramsIndex == -1)
            {
                return null;
            }
            
            // 找到 params 值的开始位置（跳过 "params": 或 "params_data":）
            int valueStart = json.IndexOf('{', paramsIndex);
            if (valueStart == -1)
            {
                return null;
            }
            
            // 找到匹配的闭合大括号
            int braceCount = 0;
            int valueEnd = valueStart;
            for (int i = valueStart; i < json.Length; i++)
            {
                if (json[i] == '{')
                {
                    braceCount++;
                }
                else if (json[i] == '}')
                {
                    braceCount--;
                    if (braceCount == 0)
                    {
                        valueEnd = i + 1;
                        break;
                    }
                }
            }
            
            if (valueEnd > valueStart)
            {
                return json.Substring(valueStart, valueEnd - valueStart);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"ExtractParamsFromJson failed for command {commandId}: {e.Message}");
        }
        
        return null;
    }
}