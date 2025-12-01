using UnityEngine;
using Voxels;
using System;

/// <summary>
/// PlaceBlock命令执行器 - 基于相对偏移与拓展方向放置方块
/// </summary>
public class PlaceBlockExecutor : MonoBehaviour, ICommandExecutor
{
    [Header("References")]
    [SerializeField] private RuntimeVoxelBuilding runtimeVoxelBuilding;
    [SerializeField] private Transform agentTransform; // Agent的位置和旋转参考
    
    private bool _isExecuting = false;
    
    public string CommandType => "place_block";
    public bool CanInterrupt => true;
    
    private void OnEnable()
    {
        if (runtimeVoxelBuilding == null)
        {
            runtimeVoxelBuilding = RuntimeVoxelBuilding.Instance;
            if (runtimeVoxelBuilding == null)
            {
                runtimeVoxelBuilding = FindAnyObjectByType<RuntimeVoxelBuilding>();
                if (runtimeVoxelBuilding == null)
                {
                    Debug.LogError("PlaceBlockExecutor: RuntimeVoxelBuilding not found");
                }
            }
        }
        
        if (agentTransform == null)
        {
            GameObject agent = GameObject.FindGameObjectWithTag("Agent");
            if (agent != null)
            {
                agentTransform = agent.transform;
            }
            else
            {
                Debug.LogError("PlaceBlockExecutor: Agent Transform not found");
            }
        }
    }
    
    public void Execute(string commandId, object paramsData, Action<bool, string> onComplete)
    {
        if (_isExecuting)
        {
            onComplete?.Invoke(false, "Another placement is already in progress");
            return;
        }
        
        // 解析参数
        if (!(paramsData is PlaceBlockParams))
        {
            // 尝试从字典或JsonUtility转换
            PlaceBlockParams parsedParams = ParseParams(paramsData);
            if (parsedParams == null)
            {
                onComplete?.Invoke(false, "Invalid PlaceBlockParams");
                return;
            }
            paramsData = parsedParams;
        }
        
        PlaceBlockParams @params = (PlaceBlockParams)paramsData;
        NormalizeParams(@params);
        _isExecuting = true;
        
        try
        {
            int blockCount = Mathf.Max(1, @params.count);
            Vector3Int expandVector = DirectionToVector(@params.expand_direction);
            
            if (blockCount > 1 && expandVector == Vector3Int.zero)
            {
                _isExecuting = false;
                onComplete?.Invoke(false, $"Invalid expand_direction: {@params.expand_direction}");
                return;
            }
            
            // 计算放置位置
            Vector3Int[] positions = CalculatePlacementPositions(@params, expandVector, blockCount);
            
            // 获取voxel类型ID
            ushort voxelTypeId = GetVoxelTypeId(@params.voxel_id, @params.voxel_name);
            if (voxelTypeId == 0)
            {
                _isExecuting = false;
                onComplete?.Invoke(false, $"Voxel type not found: {@params.voxel_name} (id: {@params.voxel_id})");
                return;
            }
            
            // 放置方块（通过RuntimeVoxelBuilding，会自动记录到事件系统）
            foreach (var pos in positions)
            {
                if (runtimeVoxelBuilding != null)
                {
                    runtimeVoxelBuilding.PlaceBlockAt(pos, voxelTypeId);
                }
                else
                {
                    Debug.LogError("PlaceBlockExecutor: RuntimeVoxelBuilding is null");
                    _isExecuting = false;
                    onComplete?.Invoke(false, "RuntimeVoxelBuilding not available");
                    return;
                }
            }
            
            Debug.Log(
                $"PlaceBlockExecutor: Placed {positions.Length} blocks of type {@params.voxel_name} " +
                $"(start offset: {@params.start_offset.x},{@params.start_offset.y},{@params.start_offset.z}; expand: {@params.expand_direction})");
            
            _isExecuting = false;
            onComplete?.Invoke(true, null);
        }
        catch (Exception e)
        {
            _isExecuting = false;
            onComplete?.Invoke(false, $"Exception during placement: {e.Message}");
        }
    }
    
    private Vector3Int[] CalculatePlacementPositions(PlaceBlockParams @params, Vector3Int expandVector, int blockCount)
    {
        Vector3Int[] positions = new Vector3Int[blockCount];
        
        Vector3Int startWorldPos = Vector3Int.RoundToInt(agentTransform.position);
        Vector3Int startPos = startWorldPos + GetRelativeOffsetVector(@params.start_offset);
        
        // 计算所有位置
        for (int i = 0; i < blockCount; i++)
        {
            positions[i] = startPos + expandVector * i;
        }
        
        return positions;
    }
    
    private Vector3Int DirectionToVector(string direction)
    {
        if (agentTransform == null)
        {
            Debug.LogError("PlaceBlockExecutor: Agent transform is null");
            return Vector3Int.zero;
        }
        
        if (string.IsNullOrEmpty(direction))
        {
            direction = "up";
        }
        
        // Agent的forward对应front，right对应right等
        Vector3 agentForward = agentTransform.forward;
        Vector3 agentRight = agentTransform.right;
        Vector3 agentUp = agentTransform.up;
        
        switch (direction.ToLowerInvariant())
        {
            case "front":
                return Vector3Int.RoundToInt(agentForward);
            case "back":
                return Vector3Int.RoundToInt(-agentForward);
            case "right":
                return Vector3Int.RoundToInt(agentRight);
            case "left":
                return Vector3Int.RoundToInt(-agentRight);
            case "up":
                return Vector3Int.RoundToInt(agentUp);
            case "down":
                return Vector3Int.RoundToInt(-agentUp);
            default:
                Debug.LogError($"PlaceBlockExecutor: Unknown direction: {direction}");
                return Vector3Int.zero;
        }
    }
    
    private ushort GetVoxelTypeId(string voxelId, string voxelName)
    {
        // 优先通过ID查找
        if (!string.IsNullOrEmpty(voxelId) && ushort.TryParse(voxelId, out ushort id))
        {
            var def = VoxelRegistry.GetDefinition(id);
            if (def != null)
            {
                return id;
            }
        }
        
        // 通过名称查找
        if (!string.IsNullOrEmpty(voxelName))
        {
            var allDefs = VoxelRegistry.GetAllDefinitions();
            foreach (var def in allDefs)
            {
                if (def != null && (def.name == voxelName || def.displayName == voxelName))
                {
                    return def.typeId;
                }
            }
        }
        
        return 0;
    }
    
    private PlaceBlockParams ParseParams(object paramsData)
    {
        // 如果已经是正确类型，直接返回
        if (paramsData is PlaceBlockParams)
        {
            return (PlaceBlockParams)paramsData;
        }
        
        // 如果是字符串（JSON字符串），直接解析
        if (paramsData is string jsonString)
        {
            try
            {
                return JsonUtility.FromJson<PlaceBlockParams>(jsonString);
            }
            catch (Exception e)
            {
                Debug.LogError($"PlaceBlockExecutor: Failed to parse params from JSON string: {e.Message}");
                return null;
            }
        }
        
        // 尝试将对象转换为JSON再解析
        try
        {
            string json = JsonUtility.ToJson(paramsData);
            return JsonUtility.FromJson<PlaceBlockParams>(json);
        }
        catch (Exception e)
        {
            Debug.LogError($"PlaceBlockExecutor: Failed to parse params: {e.Message}");
            return null;
        }
    }
    
    public void Interrupt()
    {
        _isExecuting = false;
    }
    
    private void NormalizeParams(PlaceBlockParams @params)
    {
        if (@params.start_offset == null)
        {
            @params.start_offset = new Vector3Data();
        }
        
        if (string.IsNullOrEmpty(@params.expand_direction))
        {
            @params.expand_direction = "up";
        }
        
        if (@params.count <= 0)
        {
            @params.count = 1;
        }
    }
    
    private Vector3Int GetRelativeOffsetVector(Vector3Data offsetData)
    {
        if (agentTransform == null)
        {
            Debug.LogError("PlaceBlockExecutor: Agent transform is null");
            return Vector3Int.zero;
        }
        
        if (offsetData == null)
        {
            return Vector3Int.zero;
        }
        
        Vector3 offset =
            agentTransform.right * offsetData.x +
            agentTransform.up * offsetData.y +
            agentTransform.forward * offsetData.z;
        
        return Vector3Int.RoundToInt(offset);
    }
}

