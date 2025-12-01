using UnityEngine;
using Voxels;
using System;

/// <summary>
/// UpdateVoxelType命令执行器 - 更新现有的体素类型
/// </summary>
public class UpdateVoxelTypeExecutor : MonoBehaviour, ICommandExecutor
{
    [Header("References")]
    [SerializeField] private RuntimeVoxelCreator runtimeVoxelCreator;
    
    private bool _isExecuting = false;
    
    public string CommandType => "update_voxel_type";
    public bool CanInterrupt => false;
    
    private void OnEnable()
    {
        if (runtimeVoxelCreator == null)
        {
            runtimeVoxelCreator = RuntimeVoxelCreator.Instance;
            if (runtimeVoxelCreator == null)
            {
                runtimeVoxelCreator = FindAnyObjectByType<RuntimeVoxelCreator>();
                if (runtimeVoxelCreator == null)
                {
                    Debug.LogError("UpdateVoxelTypeExecutor: RuntimeVoxelCreator not found");
                }
            }
        }
    }
    
    public void Execute(string commandId, object paramsData, Action<bool, string> onComplete)
    {
        if (_isExecuting)
        {
            onComplete?.Invoke(false, "Another update is already in progress");
            return;
        }
        
        UpdateVoxelTypeParams @params = ParseParams(paramsData);
        if (@params == null || @params.new_voxel_type == null)
        {
            onComplete?.Invoke(false, "Invalid UpdateVoxelTypeParams");
            return;
        }
        
        if (!ushort.TryParse(@params.voxel_id, out ushort voxelTypeId))
        {
            onComplete?.Invoke(false, $"Invalid voxel_id: {@params.voxel_id}");
            return;
        }
        
        _isExecuting = true;
        
        try
        {
            VoxelTypeData newVoxelType = @params.new_voxel_type;
            
            if (runtimeVoxelCreator == null)
            {
                _isExecuting = false;
                onComplete?.Invoke(false, "RuntimeVoxelCreator not available");
                return;
            }
            
            // 直接传递字符串数组，由RuntimeVoxelCreator统一处理纹理转换
            runtimeVoxelCreator.ModifyVoxelTypeForAgent(
                voxelTypeId,
                newVoxelType.name,
                newVoxelType.description,
                newVoxelType.face_textures
            );
            
            Debug.Log($"UpdateVoxelTypeExecutor: Updated voxel type ID {voxelTypeId} to '{newVoxelType.name}'");
            
            _isExecuting = false;
            onComplete?.Invoke(true, null);
        }
        catch (Exception e)
        {
            _isExecuting = false;
            onComplete?.Invoke(false, $"Exception during voxel type update: {e.Message}");
        }
    }
    
    
    private UpdateVoxelTypeParams ParseParams(object paramsData)
    {
        // 如果已经是正确类型，直接返回
        if (paramsData is UpdateVoxelTypeParams)
        {
            return (UpdateVoxelTypeParams)paramsData;
        }
        
        // 如果是字符串（JSON字符串），直接解析
        if (paramsData is string jsonString)
        {
            try
            {
                return JsonUtility.FromJson<UpdateVoxelTypeParams>(jsonString);
            }
            catch (Exception e)
            {
                Debug.LogError($"UpdateVoxelTypeExecutor: Failed to parse params from JSON string: {e.Message}");
                return null;
            }
        }
        
        // 尝试将对象转换为JSON再解析
        try
        {
            string json = JsonUtility.ToJson(paramsData);
            return JsonUtility.FromJson<UpdateVoxelTypeParams>(json);
        }
        catch (Exception e)
        {
            Debug.LogError($"UpdateVoxelTypeExecutor: Failed to parse params: {e.Message}");
            return null;
        }
    }
    
    public void Interrupt()
    {
        _isExecuting = false;
    }
}

