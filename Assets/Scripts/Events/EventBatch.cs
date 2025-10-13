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
            // 直接拼接 JsonUtility 生成的 JSON 片段
            var gsJson = UnityEngine.JsonUtility.ToJson(game_state, false);
            sb.Append("\"game_state\":").Append(gsJson);
        }

        sb.Append('}');
        return sb.ToString();
    }

    private static string BuildPayloadJson(EventData e)
    {
        // 按事件类型将 payload 序列化为 JSON
        switch (e.type)
        {
            case "player_speak":
                return ToJsonOrNull(e.payload as PlayerSpeakPayload);
            case "player_build":
                return ToJsonOrNull(e.payload as PlayerBuildPayload);
            case "voxel_type_created":
                return ToJsonOrNull(e.payload as VoxelTypeCreatedPayload);
            case "voxel_type_updated":
                return ToJsonOrNull(e.payload as VoxelTypeUpdatedPayload);
            case "agent_continue_plan":
                return ToJsonOrNull(e.payload as AgentContinuePlanPayload);
            case "agent_perception":
                return ToJsonOrNull(e.payload as AgentPerceptionPayload);
            default:
                // 兜底：尝试用 JsonUtility 序列化未知类型
                if (e.payload == null) return "null";
                var fallback = UnityEngine.JsonUtility.ToJson(e.payload, false);
                return string.IsNullOrEmpty(fallback) ? "null" : fallback;
        }
    }

    private static string ToJsonOrNull(object obj)
    {
        if (obj == null) return "null";
        var json = UnityEngine.JsonUtility.ToJson(obj, false);
        return string.IsNullOrEmpty(json) ? "null" : json;
    }

    private static string Escape(string s)
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
    public string nearby_voxels;
    public List<PendingPlanData> pending_plans;
    public List<LastCommandData> last_commands;
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
    public string base64;
    public string url;
    public string file_path;

    public ImageData(string base64 = null, string url = null, string filePath = null)
    {
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
    public string plan_id;
    public string plan_label;
    public string type;
    public object params_data; // Will be cast to specific parameter types
    public string phase;

    public LastCommandData(string id, string goalId, string goalLabel, string planId, string planLabel, string type, object paramsData, string phase)
    {
        this.id = id;
        this.goal_id = goalId;
        this.goal_label = goalLabel;
        this.plan_id = planId;
        this.plan_label = planLabel;
        this.type = type;
        this.params_data = paramsData;
        this.phase = phase;
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
    public VoxelInstanceData voxel_instance;

    public PlayerBuildPayload(VoxelInstanceData voxelInstance)
    {
        this.voxel_instance = voxelInstance;
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

    public VoxelInstanceData(string voxelId, string voxelName, Vector3Data position)
    {
        this.voxel_id = voxelId;
        this.voxel_name = voxelName;
        this.position = position;
    }
}

/// <summary>
/// voxel_type_created事件载荷
/// </summary>
[System.Serializable]
public class VoxelTypeCreatedPayload
{
    public VoxelTypeData voxel_type;

    public VoxelTypeCreatedPayload(VoxelTypeData voxelType)
    {
        this.voxel_type = voxelType;
    }
}

/// <summary>
/// voxel_type_updated事件载荷
/// </summary>
[System.Serializable]
public class VoxelTypeUpdatedPayload
{
    public string voxel_id;
    public VoxelTypeData new_voxel_type;

    public VoxelTypeUpdatedPayload(string voxelId, VoxelTypeData newVoxelType)
    {
        this.voxel_id = voxelId;
        this.new_voxel_type = newVoxelType;
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
    public string texture;
    public string[] face_textures; // [top, bottom, front, back, left, right]

    public VoxelTypeData(string id, string name, string description, string texture = "", string[] faceTextures = null)
    {
        this.id = id;
        this.name = name;
        this.description = description;
        this.texture = texture;
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
    public bool request_snapshot;

    public AgentContinuePlanPayload(string currentSummary, string possibleNextSteps, bool requestSnapshot = false)
    {
        this.current_summary = currentSummary;
        this.possible_next_steps = possibleNextSteps;
        this.request_snapshot = requestSnapshot;
    }
}

/// <summary>
/// agent_perception事件载荷
/// </summary>
[System.Serializable]
public class AgentPerceptionPayload
{
    public List<ImageData> images;

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
