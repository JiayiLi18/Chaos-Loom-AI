using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// CommandBatch数据结构 - 对应API规范
/// </summary>
[System.Serializable]
public class CommandBatch
{
    public string session_id;
    public string goal_id;
    public List<CommandData> commands;

    public CommandBatch(string sessionId, string goalId)
    {
        session_id = sessionId;
        goal_id = goalId;
        commands = new List<CommandData>();
    }
}

/// <summary>
/// 单个命令数据结构
/// </summary>
[System.Serializable]
public class CommandData
{
    public string id;
    public string type; // create_voxel_type | update_voxel_type | place_block | destroy_block | move_to | continue_plan
    public object params_data; // Will be cast to specific types based on 'type'
    
    // Optional fields for tracking (from plan/command mapping)
    public string goal_id;
    public string goal_label;
    public string plan_id;
    public string plan_label;
    public string phase; // pending | done | failed | cancelled
}

/// <summary>
/// 命令参数数据结构
/// </summary>
[System.Serializable]
public class CreateVoxelTypeParams
{
    public VoxelTypeData voxel_type;
}

[System.Serializable]
public class UpdateVoxelTypeParams
{
    public string voxel_id;
    public VoxelTypeData new_voxel_type;
}

[System.Serializable]
public class PlaceBlockParams
{
    public Vector3Data start_offset = new Vector3Data(); // relative to agent (x: right, y: up, z: front)
    public string expand_direction = "up"; // Direction to extend placement
    public int count = 1;
    public string voxel_name;
    public string voxel_id;
}

[System.Serializable]
public class DestroyBlockParams
{
    public Vector3Data start_offset = new Vector3Data();
    public string expand_direction = "up";
    public int count = 1;
    public string[] voxel_names;
    public string[] voxel_ids;
}

[System.Serializable]
public class MoveToParams
{
    public Vector3Data target_pos; // relative to agent
}

[System.Serializable]
public class ContinuePlanParams
{
    public string current_summary;
    public string[] possible_next_steps;
    public bool request_snapshot;
}

/// <summary>
/// 命令状态枚举
/// </summary>
public enum CommandStatus
{
    Pending,    // 等待执行
    Done,       // 执行完成
    Failed,     // 执行失败
    Cancelled  // 已取消
}

