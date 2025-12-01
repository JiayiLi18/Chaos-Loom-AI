using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// EventBatch数据结构 - 对应API规范
/// </summary>
[System.Serializable]
public class EventBatch
{
    public string session_id;
    public List<EventData> events;
    public GameState game_state; 

    public EventBatch(string sessionId)
    {
        session_id = sessionId;
        events = new List<EventData>();
        game_state = null;
    }

    public void AddEvent(EventData eventData)
    {
        events.Add(eventData);
    }

    public void SetGameState(GameState state)
    {
        game_state = state;
    }

    /// <summary>
    /// 校验批次内容是否满足 API 最低要求
    /// </summary>
    public bool Validate(out string error)
    {
        if (string.IsNullOrEmpty(session_id))
        {
            error = "session_id is empty";
            return false;
        }
        if (events == null || events.Count == 0)
        {
            error = "events is empty";
            return false;
        }
        for (int i = 0; i < events.Count; i++)
        {
            var e = events[i];
            if (e == null)
            {
                error = $"event[{i}] is null";
                return false;
            }
            if (string.IsNullOrEmpty(e.timestamp))
            {
                error = $"event[{i}].timestamp is empty";
                return false;
            }
            if (string.IsNullOrEmpty(e.type))
            {
                error = $"event[{i}].type is empty";
                return false;
            }
            if (e.payload == null)
            {
                error = $"event[{i}].payload is null";
                return false;
            }
        }
        error = null;
        return true;
    }

    /// <summary>
    /// 构造与后端匹配的 JSON（避免 JsonUtility 丢失 object payload）
    /// </summary>
    public string ToJson()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append('{');
        sb.Append("\"session_id\":\"").Append(Escape(session_id)).Append("\",");

        // events
        sb.Append("\"events\":[");
        for (int i = 0; i < events.Count; i++)
        {
            var e = events[i];
            sb.Append('{');
            sb.Append("\"timestamp\":\"").Append(Escape(e.timestamp)).Append("\",");
            sb.Append("\"type\":\"").Append(Escape(e.type)).Append("\",");
            sb.Append("\"payload\":").Append(BuildPayloadJson(e));
            sb.Append('}');
            if (i < events.Count - 1) sb.Append(',');
        }
        sb.Append(']');

        // game_state（可选）
        if (game_state != null)
        {
            sb.Append(',');
            sb.Append("\"game_state\":").Append(BuildGameStateJson(game_state));
        }

        sb.Append('}');
        return sb.ToString();
    }

    /// <summary>
    /// 手动构建 game_state 的 JSON，正确处理 last_commands 中的 params_data
    /// 改为 internal 以便 PlanPermissionRequest 也能使用
    /// </summary>
    internal static string BuildGameStateJson(GameState gameState)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append('{');
        
        // timestamp
        sb.Append("\"timestamp\":\"").Append(Escape(gameState.timestamp ?? "")).Append("\",");
        
        // agent_position
        if (gameState.agent_position != null)
        {
            sb.Append("\"agent_position\":").Append(ToJsonOrNull(gameState.agent_position)).Append(",");
        }
        
        // player_position_rel
        if (gameState.player_position_rel != null)
        {
            sb.Append("\"player_position_rel\":").Append(ToJsonOrNull(gameState.player_position_rel)).Append(",");
        }
        
        // six_direction
        if (gameState.six_direction != null)
        {
            sb.Append("\"six_direction\":").Append(ToJsonOrNull(gameState.six_direction)).Append(",");
        }
        
        // nearby_voxels
        sb.Append("\"nearby_voxels\":");
        if (gameState.nearby_voxels != null && gameState.nearby_voxels.Count > 0)
        {
            sb.Append('[');
            for (int i = 0; i < gameState.nearby_voxels.Count; i++)
            {
                sb.Append(ToJsonOrNull(gameState.nearby_voxels[i]));
                if (i < gameState.nearby_voxels.Count - 1) sb.Append(',');
            }
            sb.Append(']');
        }
        else
        {
            sb.Append("[]");
        }
        sb.Append(',');
        
        // pending_plans
        sb.Append("\"pending_plans\":");
        if (gameState.pending_plans != null && gameState.pending_plans.Count > 0)
        {
            sb.Append('[');
            for (int i = 0; i < gameState.pending_plans.Count; i++)
            {
                sb.Append(ToJsonOrNull(gameState.pending_plans[i]));
                if (i < gameState.pending_plans.Count - 1) sb.Append(',');
            }
            sb.Append(']');
        }
        else
        {
            sb.Append("[]");
        }
        sb.Append(',');
        
        // last_commands（需要手动处理 params_data）
        sb.Append("\"last_commands\":");
        if (gameState.last_commands != null && gameState.last_commands.Count > 0)
        {
            sb.Append('[');
            for (int i = 0; i < gameState.last_commands.Count; i++)
            {
                var cmd = gameState.last_commands[i];
                sb.Append('{');
                sb.Append("\"id\":\"").Append(Escape(cmd.id ?? "")).Append("\",");
                sb.Append("\"goal_id\":\"").Append(Escape(cmd.goal_id ?? "")).Append("\",");
                sb.Append("\"goal_label\":\"").Append(Escape(cmd.goal_label ?? "")).Append("\",");
                sb.Append("\"type\":\"").Append(Escape(cmd.type ?? "")).Append("\",");
                
                // 手动序列化 params_data（确保总是包含 params 字段）
                // params_data 应该已经是 JSON 字符串（由 GameStateManager 规范化）
                string paramsJson = BuildCommandParamsJson(cmd.type, cmd.params_data);
                if (string.IsNullOrEmpty(paramsJson))
                {
                    paramsJson = "{}"; // 确保至少有一个空对象
                }
                
                sb.Append("\"params\":").Append(paramsJson);
                sb.Append(',');
                
                sb.Append("\"phase\":\"").Append(Escape(cmd.phase ?? "")).Append("\"");
                sb.Append('}');
                if (i < gameState.last_commands.Count - 1) sb.Append(',');
            }
            sb.Append(']');
        }
        else
        {
            sb.Append("[]");
        }
        sb.Append(',');
        
        // voxel_definitions
        sb.Append("\"voxel_definitions\":");
        if (gameState.voxel_definitions != null && gameState.voxel_definitions.Count > 0)
        {
            sb.Append('[');
            for (int i = 0; i < gameState.voxel_definitions.Count; i++)
            {
                sb.Append(ToJsonOrNull(gameState.voxel_definitions[i]));
                if (i < gameState.voxel_definitions.Count - 1) sb.Append(',');
            }
            sb.Append(']');
        }
        else
        {
            sb.Append("[]");
        }
        
        sb.Append('}');
        return sb.ToString();
    }

    /// <summary>
    /// 根据命令类型构建 params 的 JSON
    /// 优化：paramsData 现在应该是 string 类型（JSON 字符串），直接返回
    /// 改为 internal 以便 PlanPermissionRequest 也能使用
    /// </summary>
    internal static string BuildCommandParamsJson(string commandType, object paramsData)
    {
        // paramsData 现在应该是 string 类型（JSON 字符串）
        if (paramsData is string paramsJsonString)
        {
            // 验证是否是有效的 JSON 对象
            string trimmed = paramsJsonString.Trim();
            if (trimmed.StartsWith("{") && trimmed.EndsWith("}"))
            {
                return paramsJsonString;
            }
            else if (string.IsNullOrEmpty(trimmed))
            {
                return "{}";
            }
            else
            {
                Debug.LogWarning($"BuildCommandParamsJson: params_data string is not valid JSON object for {commandType}, using empty object. Value: {paramsJsonString}");
                return "{}";
            }
        }
        
        // 兼容处理：如果仍然是 object 类型（向后兼容）
        if (paramsData == null)
        {
            return "{}";
        }
        
        // 如果 paramsData 是 Dictionary（空值占位符），返回空对象
        if (paramsData is System.Collections.IDictionary dict && dict.Count == 0)
        {
            return "{}";
        }
        
        // 如果 paramsData 仍然是对象类型，尝试序列化（向后兼容）
        try
        {
            var json = UnityEngine.JsonUtility.ToJson(paramsData, false);
            return string.IsNullOrEmpty(json) ? "{}" : json;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"BuildCommandParamsJson: Failed to serialize params for {commandType}: {ex.Message}, using empty object");
            return "{}";
        }
    }

    private static string BuildPayloadJson(EventData e)
    {
        // 按事件类型将 payload 序列化为 JSON
        switch (e.type)
        {
            case "player_speak":
                return BuildPlayerSpeakPayloadJson(e.payload as PlayerSpeakPayload);
            case "player_build":
                return ToJsonOrNull(e.payload as PlayerBuildPayload);
            case "voxel_type_created":
                return ToJsonOrNull(e.payload as VoxelTypeCreatedPayload);
            case "voxel_type_updated":
                return ToJsonOrNull(e.payload as VoxelTypeUpdatedPayload);
            case "agent_continue_plan":
                return BuildAgentContinuePlanPayloadJson(e.payload as AgentContinuePlanPayload);
            case "agent_perception":
                return BuildAgentPerceptionPayloadJson(e.payload as AgentPerceptionPayload);
            default:
                // 兜底：尝试用 JsonUtility 序列化未知类型
                if (e.payload == null) return "null";
                var fallback = UnityEngine.JsonUtility.ToJson(e.payload, false);
                return string.IsNullOrEmpty(fallback) ? "null" : fallback;
        }
    }

    /// <summary>
    /// 手动构建 PlayerSpeakPayload 的 JSON（确保 ImageData 正确序列化）
    /// </summary>
    private static string BuildPlayerSpeakPayloadJson(PlayerSpeakPayload payload)
    {
        if (payload == null) return "null";
        
        var sb = new System.Text.StringBuilder();
        sb.Append('{');
        sb.Append("\"text\":\"").Append(Escape(payload.text ?? "")).Append("\",");
        sb.Append("\"image\":").Append(BuildImageDataJson(payload.image));
        sb.Append('}');
        return sb.ToString();
    }

    /// <summary>
    /// 手动构建 AgentContinuePlanPayload 的 JSON（确保 ImageData 列表正确序列化）
    /// 注意：后端期望的字段名是 "image" 而不是 "request_snapshot"
    /// </summary>
    private static string BuildAgentContinuePlanPayloadJson(AgentContinuePlanPayload payload)
    {
        if (payload == null) return "null";
        
        var sb = new System.Text.StringBuilder();
        sb.Append('{');
        sb.Append("\"current_summary\":\"").Append(Escape(payload.current_summary ?? "")).Append("\",");
        sb.Append("\"possible_next_steps\":\"").Append(Escape(payload.possible_next_steps ?? "")).Append("\",");
        // 后端期望字段名是 "image"，而不是 "request_snapshot"
        sb.Append("\"image\":");
        
        if (payload.request_snapshot != null && payload.request_snapshot.Count > 0)
        {
            sb.Append('[');
            for (int i = 0; i < payload.request_snapshot.Count; i++)
            {
                sb.Append(BuildImageDataJson(payload.request_snapshot[i]));
                if (i < payload.request_snapshot.Count - 1) sb.Append(',');
            }
            sb.Append(']');
        }
        else
        {
            sb.Append("[]");
        }
        
        sb.Append('}');
        return sb.ToString();
    }

    /// <summary>
    /// 手动构建 AgentPerceptionPayload 的 JSON（确保 ImageData 列表正确序列化）
    /// 注意：后端期望的字段名是 "image" 而不是 "images"
    /// </summary>
    private static string BuildAgentPerceptionPayloadJson(AgentPerceptionPayload payload)
    {
        if (payload == null) return "null";
        
        var sb = new System.Text.StringBuilder();
        sb.Append('{');
        // 后端期望字段名是 "image"，而不是 "images"
        sb.Append("\"image\":");
        
        if (payload.images != null && payload.images.Count > 0)
        {
            sb.Append('[');
            for (int i = 0; i < payload.images.Count; i++)
            {
                sb.Append(BuildImageDataJson(payload.images[i]));
                if (i < payload.images.Count - 1) sb.Append(',');
            }
            sb.Append(']');
        }
        else
        {
            sb.Append("[]");
        }
        
        sb.Append('}');
        return sb.ToString();
    }

    /// <summary>
    /// 手动构建 ImageData 的 JSON（确保 base64 等字段正确序列化）
    /// </summary>
    private static string BuildImageDataJson(ImageData imageData)
    {
        if (imageData == null) return "null";
        
        var sb = new System.Text.StringBuilder();
        sb.Append('{');
        
        bool hasField = false;
        
        if (!string.IsNullOrEmpty(imageData.file_name))
        {
            sb.Append("\"file_name\":\"").Append(Escape(imageData.file_name)).Append("\"");
            hasField = true;
        }
        
        if (!string.IsNullOrEmpty(imageData.base64))
        {
            if (hasField) sb.Append(',');
            sb.Append("\"base64\":\"").Append(Escape(imageData.base64)).Append("\"");
            hasField = true;
        }
        
        if (!string.IsNullOrEmpty(imageData.url))
        {
            if (hasField) sb.Append(',');
            sb.Append("\"url\":\"").Append(Escape(imageData.url)).Append("\"");
            hasField = true;
        }
        
        if (!string.IsNullOrEmpty(imageData.file_path))
        {
            if (hasField) sb.Append(',');
            sb.Append("\"file_path\":\"").Append(Escape(imageData.file_path)).Append("\"");
            hasField = true;
        }
        
        // 如果所有字段都为空，返回空对象而不是 null
        if (!hasField)
        {
            sb.Append("}");
            return sb.ToString();
        }
        
        sb.Append('}');
        return sb.ToString();
    }

    internal static string ToJsonOrNull(object obj)
    {
        if (obj == null) return "null";
        var json = UnityEngine.JsonUtility.ToJson(obj, false);
        return string.IsNullOrEmpty(json) ? "null" : json;
    }

    internal static string Escape(string s)
    {
        if (string.IsNullOrEmpty(s)) return s ?? string.Empty;
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}

/// <summary>
/// 单个事件数据结构
/// </summary>
[System.Serializable]
public class EventData
{
    public string timestamp; // hhmmss format
    public string type; // player_speak | player_build | voxel_type_created | voxel_type_updated | agent_continue_plan | agent_perception
    public object payload; // Will be cast to specific types based on 'type'

    public EventData(string timestamp, string type, object payload)
    {
        this.timestamp = timestamp;
        this.type = type;
        this.payload = payload;
    }
}

/// <summary>
/// 游戏状态快照
/// </summary>
[System.Serializable]
public class GameState
{
    public string timestamp;
    public Vector3Data agent_position;
    public Vector3Data player_position_rel;
    public SixDirectionData six_direction;
    public List<NearbyVoxelData> nearby_voxels;
    public List<PendingPlanData> pending_plans;
    public List<LastCommandData> last_commands;
    public List<VoxelDefinitionData> voxel_definitions;
}

/// <summary>
/// 3D向量数据
/// </summary>
[System.Serializable]
public class Vector3Data
{
    public float x;
    public float y;
    public float z;

    // 无参构造函数（JsonUtility 需要）
    public Vector3Data()
    {
        this.x = 0;
        this.y = 0;
        this.z = 0;
    }

    public Vector3Data(float x, float y, float z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }

    public Vector3Data(Vector3 vector)
    {
        this.x = vector.x;
        this.y = vector.y;
        this.z = vector.z;
    }
}

/// <summary>
/// 六个方向的数据
/// </summary>
[System.Serializable]
public class SixDirectionData
{
    public DirectionData up;
    public DirectionData down;
    public DirectionData front;
    public DirectionData back;
    public DirectionData left;
    public DirectionData right;
}

/// <summary>
/// 单个方向的数据
/// </summary>
[System.Serializable]
public class DirectionData
{
    public string name;//the voxel name
    public string id; //the closest voxel id    
    public int distance; //the distance to the closest voxel

    public DirectionData(string name, string id, int distance)
    {
        this.name = name;
        this.id = id;
        this.distance = distance;
    }
}

/// <summary>
/// 图像数据
/// </summary>
[System.Serializable]
public class ImageData
{
    public string file_name;
    public string base64;
    public string url;
    public string file_path;

    public ImageData(string fileName = null, string base64 = null, string url = null, string filePath = null)
    {
        this.file_name = fileName;
        this.base64 = base64;
        this.url = url;
        this.file_path = filePath;
    }
}

/// <summary>
/// 待执行计划数据
/// </summary>
[System.Serializable]
public class PendingPlanData
{
    public string id;
    public string goal_id;
    public string goal_label;
    public string action_type;
    public string description;
    public string[] depends_on;

    public PendingPlanData(string id, string goalId, string goalLabel, string actionType, string description, string[] dependsOn = null)
    {
        this.id = id;
        this.goal_id = goalId;
        this.goal_label = goalLabel;
        this.action_type = actionType;
        this.description = description;
        this.depends_on = dependsOn;
    }
    
    // 无参构造函数（Unity Inspector 需要）
    public PendingPlanData()
    {
        this.id = "";
        this.goal_id = "";
        this.goal_label = "";
        this.action_type = "";
        this.description = "";
        this.depends_on = null;
    }
}

/// <summary>
/// 最后执行的命令数据
/// </summary>
[System.Serializable]
public class LastCommandData
{
    public string id;
    public string goal_id;
    public string goal_label;
    public string type;
    [SerializeField, TextArea(3, 10)] public string params_data; // JSON 字符串格式，用于存储命令参数
    public string phase;

    public LastCommandData(string id, string goalId, string goalLabel, string type, object paramsData, string phase)
    {
        this.id = id;
        this.goal_id = goalId;
        this.goal_label = goalLabel;
        this.type = type;
        // 如果 paramsData 是字符串，直接使用；否则序列化为 JSON
        this.params_data = paramsData is string jsonStr ? jsonStr : (paramsData != null ? UnityEngine.JsonUtility.ToJson(paramsData, false) : "{}");
        this.phase = phase;
    }
    
    // 无参构造函数（Unity Inspector 需要）
    public LastCommandData()
    {
        this.id = "";
        this.goal_id = "";
        this.goal_label = "";
        this.type = "";
        this.params_data = "{}";
        this.phase = "";
    }
}

// ========== Event Payload Types ==========

/// <summary>
/// player_speak事件载荷
/// </summary>
[System.Serializable]
public class PlayerSpeakPayload
{
    public string text;
    public ImageData image;

    public PlayerSpeakPayload(string text, ImageData image = null)
    {
        this.text = text;
        this.image = image;
    }
}

/// <summary>
/// player_build事件载荷
/// </summary>
[System.Serializable]
public class PlayerBuildPayload
{
    public List<VoxelInstanceData> voxel_instances;

    // 无参构造函数（JsonUtility 需要）
    public PlayerBuildPayload()
    {
        this.voxel_instances = new List<VoxelInstanceData>();
    }

    public PlayerBuildPayload(List<VoxelInstanceData> voxelInstances)
    {
        // 始终拷贝一份，避免外部清空原列表导致这里变空
        this.voxel_instances = voxelInstances != null
            ? new List<VoxelInstanceData>(voxelInstances)
            : new List<VoxelInstanceData>();
    }
}

/// <summary>
/// 体素实例数据
/// </summary>
[System.Serializable]
public class VoxelInstanceData
{
    public string voxel_id;
    public string voxel_name;
    public Vector3Data position;

    // 无参构造函数（JsonUtility 需要）
    public VoxelInstanceData()
    {
        voxel_id = "";
        voxel_name = "";
        position = new Vector3Data(0, 0, 0);
    }

    public VoxelInstanceData(string voxelId, string voxelName, Vector3Data position)
    {
        this.voxel_id = voxelId;
        this.voxel_name = voxelName;
        this.position = position;
    }
}

/// <summary>
/// 附近体素数据（用于game_state.nearby_voxels）
/// </summary>
[System.Serializable]
public class NearbyVoxelData
{
    public Vector3Data position;
    public string voxel_name;
    public string voxel_id;

    // 无参构造函数（JsonUtility 需要）
    public NearbyVoxelData()
    {
        position = new Vector3Data(0, 0, 0);
        voxel_name = "";
        voxel_id = "";
    }

    public NearbyVoxelData(Vector3Data position, string voxelName, string voxelId)
    {
        this.position = position;
        this.voxel_name = voxelName;
        this.voxel_id = voxelId;
    }
}

/// <summary>
/// 体素定义数据（用于game_state.voxel_definitions）
/// 对应Python端的Dict[str, Any]格式
/// </summary>
[System.Serializable]
public class VoxelDefinitionData
{
    public int id;
    public string name;
    public string[] face_textures;
    public string description;

    // 无参构造函数（JsonUtility 需要）
    public VoxelDefinitionData()
    {
        id = 0;
        name = "";
        face_textures = new string[6];
        description = "";
    }

    public VoxelDefinitionData(int id, string name, string[] faceTextures, string description)
    {
        this.id = id;
        this.name = name ?? "";
        this.face_textures = faceTextures ?? new string[6];
        this.description = description ?? "";
    }
}

/// <summary>
/// voxel_type_created事件载荷
/// </summary>
[System.Serializable]
public class VoxelTypeCreatedPayload
{
    public VoxelTypeData voxel_type;
    public string initiator; // player | npc | system

    public VoxelTypeCreatedPayload(VoxelTypeData voxelType, string initiator = null)
    {
        this.voxel_type = voxelType;
        this.initiator = initiator;
    }
}

/// <summary>
/// voxel_type_updated事件载荷
/// </summary>
[System.Serializable]
public class VoxelTypeUpdatedPayload
{
    public string voxel_id;
    public VoxelTypeData old_voxel_type;
    public VoxelTypeData new_voxel_type;

		public VoxelTypeUpdatedPayload(string voxelId, VoxelTypeData oldVoxelType, VoxelTypeData newVoxelType = null)
    {
        this.voxel_id = voxelId;
        this.old_voxel_type = oldVoxelType;
        this.new_voxel_type = newVoxelType;
    }

		// 便捷构造：删除场景（new_voxel_type = null）
		public VoxelTypeUpdatedPayload(string voxelId, VoxelTypeData oldVoxelType)
			: this(voxelId, oldVoxelType, null)
		{
		}
}

/// <summary>
/// 体素类型数据
/// </summary>
[System.Serializable]
public class VoxelTypeData
{
    public string id;
    public string name;
    public string description;
    public string[] face_textures; // [right, left, top, bottom, front, back]

    public VoxelTypeData(string id, string name, string description, string[] faceTextures = null)
    {
        this.id = id;
        this.name = name;
        this.description = description;
        this.face_textures = faceTextures ?? new string[6] { "", "", "", "", "", "" };
    }
}

/// <summary>
/// agent_continue_plan事件载荷
/// </summary>
[System.Serializable]
public class AgentContinuePlanPayload
{
    public string current_summary;
    public string possible_next_steps;
    public List<ImageData> request_snapshot; //4 images of front, back, left, right of the agent

    public AgentContinuePlanPayload(string currentSummary, string possibleNextSteps, List<ImageData> requestSnapshot = null)
    {
        this.current_summary = currentSummary;
        this.possible_next_steps = possibleNextSteps;
        this.request_snapshot = requestSnapshot ?? new List<ImageData>();
    }
}

/// <summary>
/// agent_perception事件载荷
/// </summary>
[System.Serializable]
public class AgentPerceptionPayload
{
    public List<ImageData> images; //4 images of front, back, left, right of the agent

    public AgentPerceptionPayload(List<ImageData> images)
    {
        this.images = images;
    }
}

/// <summary>
/// 权限请求数据结构 (暂时定义，后续完善)
/// </summary>
[System.Serializable]
public class PermissionRequest
{
    public string session_id;
    public string action_type;
    public object parameters;

    public PermissionRequest(string sessionId, string actionType, object parameters = null)
    {
        this.session_id = sessionId;
        this.action_type = actionType;
        this.parameters = parameters;
    }
}

/// <summary>
/// 游戏时间管理器 - 从游戏启动开始计时
/// </summary>
public static class GameTime
{
    private static float gameStartTime;
    private static bool isInitialized = false;

    /// <summary>
    /// 初始化游戏时间（在游戏启动时调用）
    /// </summary>
    public static void Initialize()
    {
        gameStartTime = Time.time;
        isInitialized = true;
    }

    /// <summary>
    /// 获取游戏运行时间（秒）
    /// </summary>
    public static float GetGameTime()
    {
        if (!isInitialized)
        {
            Initialize();
        }
        return Time.time - gameStartTime;
    }

    /// <summary>
    /// 生成hhmmss格式的游戏时间戳
    /// </summary>
    /// <returns>格式化的游戏时间戳字符串</returns>
    public static string GenerateTimestamp()
    {
        float gameTime = GetGameTime();
        
        // 转换为小时、分钟、秒
        int totalSeconds = Mathf.FloorToInt(gameTime);
        int hours = totalSeconds / 3600;
        int minutes = (totalSeconds % 3600) / 60;
        int seconds = totalSeconds % 60;
        
        // 确保时间不超过24小时，循环显示
        hours = hours % 24;
        
        return $"{hours:D2}{minutes:D2}{seconds:D2}";
    }
}

/// <summary>
/// 时间戳工具类
/// </summary>
public static class TimestampUtils
{
    /// <summary>
    /// 生成hhmmss格式的时间戳（使用游戏时间）
    /// </summary>
    /// <returns>格式化的时间戳字符串</returns>
    public static string GenerateTimestamp()
    {
        return GameTime.GenerateTimestamp();
    }

    /// <summary>
    /// 从Unity Vector3转换为Vector3Data
    /// </summary>
    public static Vector3Data ToVector3Data(Vector3 vector)
    {
        return new Vector3Data(vector);
    }

    /// <summary>
    /// 从Vector3Data转换为Unity Vector3
    /// </summary>
    public static Vector3 ToVector3(Vector3Data data)
    {
        return new Vector3(data.x, data.y, data.z);
    }
}