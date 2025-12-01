using UnityEngine;
using Voxels;
using System;
using System.Linq;

/// <summary>
/// DestroyBlock命令执行器 - 基于相对偏移与拓展方向删除方块
/// </summary>
public class DestroyBlockExecutor : MonoBehaviour, ICommandExecutor
{
    [Header("References")]
    [SerializeField] private RuntimeVoxelBuilding runtimeVoxelBuilding;
    [SerializeField] private Transform agentTransform;
    
    private bool _isExecuting = false;
    
    public string CommandType => "destroy_block";
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
                    Debug.LogError("DestroyBlockExecutor: RuntimeVoxelBuilding not found");
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
        }
    }
    
    public void Execute(string commandId, object paramsData, Action<bool, string> onComplete)
    {
        if (_isExecuting)
        {
            onComplete?.Invoke(false, "Another destruction is already in progress");
            return;
        }
        
        DestroyBlockParams @params = ParseParams(paramsData);
        if (@params == null)
        {
            onComplete?.Invoke(false, "Invalid DestroyBlockParams");
            return;
        }
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
            
            Vector3Int[] positions = CalculateDestructionPositions(@params, expandVector, blockCount);
            int destroyedCount = 0;
            
            // 获取WorldGrid用于检查体素类型（如果需要过滤）
            var worldGrid = runtimeVoxelBuilding != null ? 
                runtimeVoxelBuilding.GetComponentInChildren<WorldGrid>() ?? FindAnyObjectByType<WorldGrid>() : 
                FindAnyObjectByType<WorldGrid>();
            
            foreach (var pos in positions)
            {
                if (worldGrid != null)
                {
                    var voxel = worldGrid.GetVoxelWorld(pos);
                    if (voxel.IsAir)
                    {
                        continue;
                    }
                    
                    // 检查是否需要过滤特定类型
                    if (!ShouldDestroyVoxel(voxel, @params))
                    {
                        continue;
                    }
                }
                
                // 删除方块（通过RuntimeVoxelBuilding，会自动记录到事件系统）
                if (runtimeVoxelBuilding != null)
                {
                    runtimeVoxelBuilding.DestroyBlockAt(pos);
                    destroyedCount++;
                }
                else
                {
                    Debug.LogError("DestroyBlockExecutor: RuntimeVoxelBuilding is null");
                    _isExecuting = false;
                    onComplete?.Invoke(false, "RuntimeVoxelBuilding not available");
                    return;
                }
            }
            
            Debug.Log(
                $"DestroyBlockExecutor: Destroyed {destroyedCount} blocks " +
                $"(start offset: {@params.start_offset.x},{@params.start_offset.y},{@params.start_offset.z}; expand: {@params.expand_direction})");
            
            _isExecuting = false;
            onComplete?.Invoke(true, null);
        }
        catch (Exception e)
        {
            _isExecuting = false;
            onComplete?.Invoke(false, $"Exception during destruction: {e.Message}");
        }
    }
    
    private Vector3Int[] CalculateDestructionPositions(DestroyBlockParams @params, Vector3Int expandVector, int blockCount)
    {
        Vector3Int[] positions = new Vector3Int[blockCount];
        
        Vector3Int startWorldPos = Vector3Int.RoundToInt(agentTransform.position);
        Vector3Int startPos = startWorldPos + GetRelativeOffsetVector(@params.start_offset);
        
        for (int i = 0; i < blockCount; i++)
        {
            positions[i] = startPos + expandVector * i;
        }
        
        return positions;
    }
    
    private bool ShouldDestroyVoxel(Voxel voxel, DestroyBlockParams @params)
    {
        // 如果没有指定过滤条件，销毁所有
        if ((@params.voxel_names == null || @params.voxel_names.Length == 0) &&
            (@params.voxel_ids == null || @params.voxel_ids.Length == 0))
        {
            return true;
        }
        
        var def = VoxelRegistry.GetDefinition(voxel.TypeId);
        if (def == null)
        {
            return false;
        }
        
        // 检查名称匹配
        if (@params.voxel_names != null && @params.voxel_names.Length > 0)
        {
            if (@params.voxel_names.Contains(def.name) || @params.voxel_names.Contains(def.displayName))
            {
                return true;
            }
        }
        
        // 检查ID匹配
        if (@params.voxel_ids != null && @params.voxel_ids.Length > 0)
        {
            if (@params.voxel_ids.Contains(voxel.TypeId.ToString()))
            {
                return true;
            }
        }
        
        return false;
    }
    
    private Vector3Int DirectionToVector(string direction)
    {
        if (agentTransform == null)
        {
            Debug.LogError("DestroyBlockExecutor: Agent transform is null");
            return Vector3Int.zero;
        }
        
        if (string.IsNullOrEmpty(direction))
        {
            direction = "up";
        }
        
        Vector3 agentForward = agentTransform.forward;
        Vector3 agentRight = agentTransform.right;
        Vector3 agentUp = agentTransform.up;
        
        switch (direction.ToLowerInvariant())
        {
            case "front": return Vector3Int.RoundToInt(agentForward);
            case "back": return Vector3Int.RoundToInt(-agentForward);
            case "right": return Vector3Int.RoundToInt(agentRight);
            case "left": return Vector3Int.RoundToInt(-agentRight);
            case "up": return Vector3Int.RoundToInt(agentUp);
            case "down": return Vector3Int.RoundToInt(-agentUp);
            default: return Vector3Int.zero;
        }
    }
    
    private DestroyBlockParams ParseParams(object paramsData)
    {
        // 如果已经是正确类型，直接返回
        if (paramsData is DestroyBlockParams)
        {
            return (DestroyBlockParams)paramsData;
        }
        
        // 如果是字符串（JSON字符串），直接解析
        if (paramsData is string jsonString)
        {
            try
            {
                return JsonUtility.FromJson<DestroyBlockParams>(jsonString);
            }
            catch (Exception e)
            {
                Debug.LogError($"DestroyBlockExecutor: Failed to parse params from JSON string: {e.Message}");
                return null;
            }
        }
        
        // 尝试将对象转换为JSON再解析
        try
        {
            string json = JsonUtility.ToJson(paramsData);
            return JsonUtility.FromJson<DestroyBlockParams>(json);
        }
        catch (Exception e)
        {
            Debug.LogError($"DestroyBlockExecutor: Failed to parse params: {e.Message}");
            return null;
        }
    }
    
    public void Interrupt()
    {
        _isExecuting = false;
    }
    
    private void NormalizeParams(DestroyBlockParams @params)
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
            Debug.LogError("DestroyBlockExecutor: Agent transform is null");
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

